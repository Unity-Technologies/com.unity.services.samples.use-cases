// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const badRequestError = 400;
const unprocessableEntityError = 422;
const tooManyRequestsError = 429;

const { PurchasesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");

module.exports = async ({ params, context, logger }) =>
{
    const { projectId, playerId, accessToken } = context;
    const purchasesApi = new PurchasesApi({ accessToken });
    const cloudSaveApi = new DataApi({ accessToken });

    let returnObject;

    try
    {
        const getItemsResponse = await cloudSaveApi.getItems(projectId, playerId, [ "STARTER_PACK_STATUS" ]);

        // Prevent the purchase if Cloud Save confirms the player already claimed a Starter Pack.

        if (getItemsResponse.data.results &&
            getItemsResponse.data.results.length > 0 &&
            getItemsResponse.data.results[0] &&
            getItemsResponse.data.results[0].value)
        {
            if (getItemsResponse.data.results[0].value.claimed &&
                getItemsResponse.data.results[0].value.claimed === true)
            {
                logger.error("The Starter Pack has already been claimed by this player.");
                throw Error("The Starter Pack has already been claimed by this player.");
            }
        }

        // Call Economy to make the purchase. An error will be thrown if the player can't afford it.

        const purchaseResult = await purchaseStarterPack(purchasesApi, projectId, playerId);

        returnObject = purchaseResult.data;

        // Let Cloud Save know that the Starter Pack has been claimed by this player.

        await cloudSaveApi.setItem(projectId, playerId, { key: "STARTER_PACK_STATUS", value: { claimed: true } });
    }
    catch (error)
    {
        transformAndThrowCaughtException(error);
    }

    return returnObject;
};

async function purchaseStarterPack(purchasesApi, projectId, playerId)
{
    try
    {
        return await purchasesApi.makeVirtualPurchase(projectId, playerId, { id: "STARTER_PACK" });
    }
    catch
    {
        throw new CantAffordStarterPackError("Not enough gems to purchase Starter Pack.");
    }
}

// this standardizes our outgoing errors to make them easier to parse in the client
function transformAndThrowCaughtException(error)
{
    let result = {
        status: 0,
        title: "",
        message: "",
        retryAfter: null,
        additionalDetails: ""
    };

    if (error.response)
    {
        result.status = error.response.data.status ? error.response.data.status : 0;
        result.title = error.response.data.title ? error.response.data.title : "Unknown Error";
        result.message = error.response.data.detail ? error.response.data.detail : error.response.data;

        if (error.response.status === tooManyRequestsError)
        {
            result.retryAfter = error.response.headers['retry-after'];
        }

        if (error.response.status === badRequestError)
        {
            let arr = [];
            _.forEach(error.response.data.errors, error => {
                arr = _.concat(arr, error.messages);
            });
            result.additionalDetails = arr;
        }
    }
    else
    {
        if (error instanceof CloudCodeCustomError)
        {
            result.status = error.status;
        }
        result.title = error.name;
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

class CantAffordStarterPackError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "CantAffordStarterPackError";
        this.status = 3;
    }
}
