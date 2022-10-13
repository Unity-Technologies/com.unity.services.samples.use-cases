// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { PurchasesApi } = require("@unity-services/economy-2.3");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, accessToken } = context;
        const purchasesApi = new PurchasesApi({ accessToken });
        const cloudSaveApi = new DataApi({ accessToken });

        const getItemsResponse = await cloudSaveApi.getItems(projectId, playerId, [ "STARTER_PACK_STATUS" ]);

        // Prevent the purchase if Cloud Save confirms the player already claimed a Starter Pack.

        if (getItemsResponse.data.results &&
            getItemsResponse.data.results.length > 0 &&
            getItemsResponse.data.results[0] &&
            getItemsResponse.data.results[0].value) {
            if (getItemsResponse.data.results[0].value.claimed &&
                getItemsResponse.data.results[0].value.claimed === true) {
                logger.error("The Starter Pack has already been claimed by this player.");
                throw Error("The Starter Pack has already been claimed by this player.");
            }
        }

        // Call Economy to make the purchase. An error will be thrown if the player can't afford it.

        const purchaseResult = await purchaseStarterPack(purchasesApi, projectId, playerId);

        const returnObject = purchaseResult.data;

        // Let Cloud Save know that the Starter Pack has been claimed by this player.

        await cloudSaveApi.setItem(projectId, playerId, { key: "STARTER_PACK_STATUS", value: { claimed: true } });

        return returnObject;
    } catch (error) {
        transformAndThrowCaughtException(error);
    }
};

async function purchaseStarterPack(purchasesApi, projectId, playerId) {
    try {
        const playerPurchaseVirtualRequest = { id: "STARTER_PACK_PURCHASE" };
        const requestParameters = { projectId, playerId, playerPurchaseVirtualRequest };

        return await purchasesApi.makeVirtualPurchase(requestParameters);

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

// this standardizes our outgoing errors to make them easier to parse in the client
function transformAndThrowCaughtException(error) {
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
    } else {
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
