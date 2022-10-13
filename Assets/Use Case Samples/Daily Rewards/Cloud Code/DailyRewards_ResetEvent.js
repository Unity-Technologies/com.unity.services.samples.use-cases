// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ context, logger }) => {
    try {
        const { projectId, playerId, environmentId, accessToken } = context;
        const cloudSave = new DataApi({ accessToken });

        const services = { projectId, playerId, environmentId, cloudSave, logger };

        const epochTime = _.now();
        logger.info("Current epochTime: " + epochTime);

        const promiseResponses = await Promise.all([
            setEventStartEpochTimeForDemonstrating(services, epochTime),
            clearPlayerStatus(services)
        ]);

        logger.info("Successfully reset Daily Rewards event.");
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

// Setup start epoch time. This is ONLY needed to demonstrate this Use Case Sample. 
// Normally this value would be set to the first day of the month and stored in Remote Config to control the event for all
// players, but here it's set in Cloud Save for this Use Case Sample to facilitate testing.
async function setEventStartEpochTimeForDemonstrating(services, epochTime) {
    await services.cloudSave.setItem(services.projectId, services.playerId, { key: "DAILY_REWARDS_START_EPOCH_TIME", value: epochTime } );
}

// Clear the players event data to simulate the first time a player visits the Daily Rewards event.
async function clearPlayerStatus(services) {
    try {
        await services.cloudSave.deleteItem("DAILY_REWARDS_STATUS", services.projectId, services.playerId);
    } catch (error) {
        // Cloud Save throws when the key does not exist. Check if it's the 'not found' error 404.
        if (error.response.status === 404)
        {
            services.logger.info("Player record did not exist so it did not need to be deleted.");
            return;
        }

        // Rethrow this unexpected error so we return failure to client.
        throw error;
    }
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
    } else {
        result.name = error.name;
        result.message = error.message;
    }

    throw new Error(JSON.stringify(result));
}
