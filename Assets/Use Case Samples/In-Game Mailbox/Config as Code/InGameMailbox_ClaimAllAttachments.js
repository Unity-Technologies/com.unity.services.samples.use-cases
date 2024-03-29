const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.2");
const { PurchasesApi } = require("@unity-services/economy-2.3");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, accessToken} = context;
        const cloudSaveApi = new DataApi({ accessToken });
        const purchasesApi = new PurchasesApi({ accessToken });

        let instance = { projectId, playerId, cloudSaveApi, purchasesApi, logger };

        let cloudSaveInboxState = await getCloudSaveData(instance);

        const messagesToClaim = getClaimableMessagesFromInboxState(cloudSaveInboxState);

        const processedTransactions = await claimAttachments(instance, messagesToClaim);

        // messagesToClaim was passed by reference from getClaimableMessagesFromInboxState and therefore 
        // changes made to the messages included in it are saved when cloudSaveInboxState is saved.
        await updateInboxStateInCloudSave(instance, cloudSaveInboxState);
    
        return { processedTransactions };
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

async function getCloudSaveData(instance) {
    const getItemsResponse = await instance.cloudSaveApi.getItems(
        instance.projectId,
        instance.playerId,
        [
            "MESSAGES_INBOX_STATE"
        ]
    );

    if (getItemsResponse === undefined ||
        getItemsResponse.data === undefined ||
        getItemsResponse.data.results[0] === undefined) {
        throw new MissingCloudSaveDataError("Cloud Save couldn't find the key \"MESSAGES_INBOX_STATE\".");
    }

    return JSON.parse(getItemsResponse.data.results[0].value);
}

function getClaimableMessagesFromInboxState(cloudSaveInboxState) {
    const messages = cloudSaveInboxState.messages.filter(
        currentMessage => currentMessage.metadata.hasUnclaimedAttachment);

    if (messages === undefined || messages.length <= 0) {
        throw new NoClaimableAttachmentsError("There are no messages with unclaimed attachments in the inbox.");
    }

    return messages;
}

async function claimAttachments(instance, messagesToClaim) {
    const processedTransactions = [];
    messagesToClaim.forEach(message => {
        processEconomyPurchase(instance, message.messageInfo.attachment);
        processedTransactions.push(message.messageInfo.attachment);
        message.metadata.hasUnclaimedAttachment = false;
        message.metadata.isRead = true;
    });
    
    return processedTransactions;
}

async function processEconomyPurchase(instance, purchaseId) {
    try {
        const projectId = instance.projectId;
        const playerId = instance.playerId;
        const playerPurchaseVirtualRequest = { id: purchaseId };

        await instance.purchasesApi.makeVirtualPurchase({ projectId, playerId, playerPurchaseVirtualRequest });
    } catch (e) {
        const message = "Virtual purchase failed";

        if (e.response !== undefined && e.response !== null) {
            var exceptionData = e.response.data;
            var exceptionHeaders = e.response.headers;
            const statusCode = exceptionData.code ? exceptionData.code : exceptionData.status;

            if (e.response.status === tooManyRequestsError) {
                const retryAfter = exceptionHeaders['retry-after'] ? exceptionHeaders['retry-after'] : null;

                throw new EconomyRateLimitError(message, exceptionData.detail,
                    exceptionData.title, statusCode, retryAfter);
            } else if (e.response.status === badRequestError) {
                let details = [];
                _.forEach(exceptionData.errors, error => {
                    details = _.concat(details, error.messages);
                });

                throw new EconomyValidationError(message, exceptionData.detail,
                    exceptionData.title, statusCode, details);
            } else {
                throw new EconomyProcessingError(message, exceptionData.detail,
                    exceptionData.title, statusCode)
            }
        } else {
            throw new EconomyError(message);
        }
    }
}

async function updateInboxStateInCloudSave(instance, cloudSaveInboxState) {
    const setItemBody = {
        key: "MESSAGES_INBOX_STATE",
        value: JSON.stringify(cloudSaveInboxState)
    };

    await instance.cloudSaveApi.setItem(
        instance.projectId,
        instance.playerId,
        setItemBody
    );
}

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
function transformAndThrowCaughtError(error) {
    let result = {
        status: 0,
        name: "",
        message: "",
        retryAfter: null,
        details: ""
    };

    if (error.response) {
        result.status = error.response.data.status ? error.response.data.status : 0;
        result.name = error.response.data.title ? error.response.data.title : "Unknown Error";
        result.message = error.response.data.detail ? error.response.data.detail : error.response.data;

        if (error.response.status === tooManyRequestsError) {
            result.retryAfter = error.response.headers['retry-after'];
        } else if (error.response.status === badRequestError) {
            let arr = [];
            _.forEach(error.response.data.errors, error => {
                arr = _.concat(arr, error.messages);
            });
            result.details = arr;
        }
    }
    else {
        if (error instanceof EconomyError) {
            result.status = error.status;
            result.retryAfter = error.retryAfter;
            result.details = error.details;
        } else if (error instanceof CloudCodeCustomError) {
            result.status = error.status;
        }
        result.name = error.name;
        result.message = error.message;
    }

    throw new Error(JSON.stringify(result));
}

class CloudCodeCustomError extends Error {
    constructor(message) {
        super(message);
        this.name = "CloudCodeCustomError";
        this.status = 1;
    }
}

class EconomyError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "EconomyError";
        this.status = 2;
        this.retryAfter = null;
        this.details = "";
    }
}

class EconomyProcessingError extends EconomyError {
    constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus) {
        super(message + ": " + innerExceptionMessage);
        this.name = "EconomyError: " + innerExceptionName;
        this.status = innerExceptionStatus;
    }
}

class EconomyRateLimitError extends EconomyProcessingError {
    constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus, retryAfter) {
        super(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus);
        this.retryAfter = retryAfter
    }
}

class EconomyValidationError extends EconomyProcessingError {
    constructor(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus, details) {
        super(message, innerExceptionMessage, innerExceptionName, innerExceptionStatus);
        this.details = details;
    }
}

class MissingCloudSaveDataError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "MissingCloudSaveDataError";
        this.status = 3;
    }
}

class NoClaimableAttachmentsError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "NoClaimableAttachmentsError";
        this.status = 6;
    }
}

module.exports.params = {};
