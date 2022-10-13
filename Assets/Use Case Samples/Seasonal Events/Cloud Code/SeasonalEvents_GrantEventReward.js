// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

const cloudSaveKeyLastCompletedEvent = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT";
const cloudSaveKeyLastCompletedEventTimestamp = "SEASONAL_EVENTS_LAST_COMPLETED_EVENT_TIMESTAMP";

module.exports = async ({ context }) => {
    try {
        const { projectId, environmentId, playerId, accessToken } = context;
        const economy = new CurrenciesApi({ accessToken });
        const remoteConfig = new SettingsApi();
        const cloudSave = new DataApi({ accessToken });

        const timestamp = _.now();

        const timestampMinutes = getTimestampMinutes(timestamp);
        const { remoteConfigData, cloudSaveData } = await GetData(remoteConfig, cloudSave, projectId, environmentId, playerId, timestampMinutes);

        const grantedRewards = await grantRewards(economy, projectId, playerId, remoteConfigData, cloudSaveData, timestamp);
        await saveEventCompleted(cloudSave, projectId, playerId, timestamp, remoteConfigData);

        const eventKey = remoteConfigData["EVENT_KEY"];

        return { grantedRewards, eventKey, timestamp, timestampMinutes: parseInt(timestampMinutes) };
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

function getTimestampMinutes(timestamp) {
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function GetData(remoteConfig, cloudSave, projectId, environmentId, playerId, timestampMinutes) {
    return await Promise.all([
        getRemoteConfigData(remoteConfig, projectId, environmentId, playerId, timestampMinutes),
        getCloudSaveData(cloudSave, projectId, playerId)
    ]).then(function(promiseResponses) {
        return {
            'remoteConfigData': promiseResponses[0],
            'cloudSaveData': promiseResponses[1]
        }
    });
}

async function getRemoteConfigData(remoteConfig, projectId, environmentId, playerId, timestampMinutes) {
    const result = await remoteConfig.assignSettings({
        projectId,
        environmentId,
        "userId": playerId,
        "attributes": {
            "unity": {},
            "app": {},
            "user": {
                "timestampMinutes": timestampMinutes
            }
        }
    });

    return result.data.configs.settings;
}

async function getCloudSaveData(cloudSave, projectId, playerId) {
    const getItemsResponse = await cloudSave.getItems(
        projectId,
        playerId,
        [cloudSaveKeyLastCompletedEvent, cloudSaveKeyLastCompletedEventTimestamp]
    );

    return cloudSaveResponseToObject(getItemsResponse);
}

function cloudSaveResponseToObject(getItemsResponse) {
    let returnObject = {
        lastCompletedEvent: "",
        lastCompletedEventTimestamp: 0,
    };

    getItemsResponse.data.results.forEach(item => {
        if (item.key === cloudSaveKeyLastCompletedEvent) {
            returnObject.lastCompletedEvent = item.value;
        } else if (item.key === cloudSaveKeyLastCompletedEventTimestamp) {
            returnObject.lastCompletedEventTimestamp = item.value;
        }
    });

    return returnObject;
}

async function grantRewards(economy, projectId, playerId, remoteConfigData, cloudSaveData, timestamp) {
    throwIfRewardDistributionNotAllowed(remoteConfigData, cloudSaveData, timestamp);

    const rewards = getRewardsFromRemoteConfigData(remoteConfigData);

    const incrementedRewards = [];

    for (let i = 0; i < rewards.length; i++) {
        let currencyId = rewards[i]["id"];
        let amount = rewards[i]["quantity"];
        if (currencyId != null && amount != null) {
            const currencyModifyBalanceRequest = { currencyId, amount };
            const requestParameters = { projectId, playerId, currencyId, currencyModifyBalanceRequest };
            let currencyBalance = await economy.incrementPlayerCurrencyBalance(requestParameters);

            let rewardBalance = {
                "id": currencyBalance.data.currencyId,
                "quantity": currencyBalance.data.balance,
                "spriteAddress": rewards[i].spriteAddress
            };
            incrementedRewards.push(rewardBalance);
        }
    }

    return incrementedRewards;
}

function throwIfRewardDistributionNotAllowed(remoteConfigData, cloudSaveData, timestamp) {
    const activeEventKey = remoteConfigData["EVENT_KEY"];
    const activeEventDurationMinutes = remoteConfigData["EVENT_TOTAL_DURATION_MINUTES"];
    const lastCompletedEvent = cloudSaveData.lastCompletedEvent;
    const lastCompletedEventTimestamp = cloudSaveData.lastCompletedEventTimestamp;

    // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
    // current season's key is the same as the key of the season that was active the last time the event was completed.
    if (activeEventKey === lastCompletedEvent) {
        // Because the key of the season that was active the last time the event was completed is the same as the 
        // current season's key, we now need to check whether the timestamp of the last time the event was completed 
        // is so old that it couldn't possibly be from the current iteration of this season.
        //
        // We do these cyclical seasons for ease of demonstration in the sample project, however in a real world
        // implementation (where seasonal events last longer than a few minutes) you would likely create a new 
        // override in remote config each time an event period was starting.
        throwIfLastCompletedEventTimestampIsFromCurrentEvent(timestamp, activeEventDurationMinutes, lastCompletedEventTimestamp);
    }
}

function throwIfLastCompletedEventTimestampIsFromCurrentEvent(currentTime, activeEventDurationMinutes, lastCompletedEventTimestamp) {
    const millisecondsInAMinute = 60000;
    const eventDurationMilliseconds = activeEventDurationMinutes * millisecondsInAMinute;
    const earliestPotentialStartForActiveEvent = currentTime - eventDurationMilliseconds;

    // Greater than ( > ) when talking about timestamps means more recent
    if (lastCompletedEventTimestamp >= earliestPotentialStartForActiveEvent) {
        throw new InvalidRewardDistributionAttemptError("The rewards for this season have already been claimed.")
    }
}

function getRewardsFromRemoteConfigData(remoteConfigData) {
    let eventRewards = [];

    const rewardResults = remoteConfigData["SEASONAL_EVENTS_CHALLENGE_REWARD"];

    if (rewardResults != null && rewardResults["rewards"] != null) {

        eventRewards = rewardResults["rewards"];
    }

    return eventRewards;
}

async function saveEventCompleted(cloudSave, projectId, playerId, timestamp, remoteConfigData) {
    await cloudSave.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: cloudSaveKeyLastCompletedEvent, value: remoteConfigData["EVENT_KEY"] },
                { key: cloudSaveKeyLastCompletedEventTimestamp, value: timestamp },
            ]
        }
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
    } else {
        if (error instanceof CloudCodeCustomError) {
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

class InvalidRewardDistributionAttemptError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "InvalidRewardDistributionAttemptError";
        this.status = 2;
    }
}
