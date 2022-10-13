// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

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
        const message = getMessageFromInboxState(cloudSaveInboxState, params.messageId);

        if (messageHasUnclaimedAttachment(message)) {
            await processEconomyPurchase(instance, message.messageInfo.attachment);

            message.metadata.hasUnclaimedAttachment = false;

            // message was passed by reference from getMessageFromInboxState and therefore 
            // changes made to its properties are saved when cloudSaveInboxState is saved.
            await updateInboxStateInCloudSave(instance, cloudSaveInboxState);
        }
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

function getMessageFromInboxState(cloudSaveInboxState, desiredMessageId) {
    const message = cloudSaveInboxState.messages.find(
        currentMessage => currentMessage.messageId === desiredMessageId);

    if (message === undefined) {
        throw new InvalidArgumentError("The message whose attachment is attempting to be claimed can't be found.");
    }

    return message;
}

function messageHasUnclaimedAttachment(message) {
    if (message.messageInfo.attachment === "") {
        throw new InvalidArgumentError("Message does not have an attachment to claim.");
    } else if (!message.metadata.hasUnclaimedAttachment) {
        throw new AttachmentAlreadyClaimedError("This message's attachment has already been claimed.");
    }

    return true;
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

class InvalidArgumentError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "InvalidArgumentError";
        this.status = 4;
    }
}

class AttachmentAlreadyClaimedError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "AttachmentAlreadyClaimedError";
        this.status = 5;
    }
}
