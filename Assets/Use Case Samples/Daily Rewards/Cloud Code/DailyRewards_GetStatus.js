// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

module.exports = async ({ context, logger }) => {
    try {
        const { projectId, playerId, environmentId, accessToken } = context;
        const remoteConfig = new SettingsApi({ accessToken });
        const cloudSave = new DataApi({ accessToken });

        const services = { projectId, playerId, environmentId, remoteConfig, cloudSave, logger };

        const epochTime = _.now();
        logger.info("Current epochTime: " + epochTime);

        let eventState = { 
            epochTime,
            result: { 
                success: true, 
                firstVisit: false,
                daysRemaining: 0,
                secondsTillClaimable: 0,
                secondsTillNextDay: 0
            }
        };

        await readInitialState(services, eventState);

        updateState(services, eventState);

        eventState.result.daysClaimed = eventState.playerStatus.daysClaimed;

        logger.info("Result: " + JSON.stringify(eventState.result));
    
        return eventState.result;
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

async function readInitialState(services, eventState) {
    const promiseResponses = await Promise.all([
        getRemoteConfigData(services),
        getEventStartEpochTime(services),
        getPlayerStatus(services)
    ]);

    eventState.configData = promiseResponses[0];
    services.logger.info("Initial configData: " + JSON.stringify(eventState.configData));

    eventState.startEpochTime = promiseResponses[1];
    services.logger.info("Initial startEpochTime: " + JSON.stringify(eventState.startEpochTime));

    eventState.playerStatus = promiseResponses[2];
    services.logger.info("Initial playerStatus: " + JSON.stringify(eventState.playerStatus));

    // Setup start epoch time. This is ONLY needed to demonstrate this use case sample. 
    // Normally this value would be set on Remote Config for all players to the start of the month.
    if (eventState.startEpochTime === undefined) {
        eventState.startEpochTime = await setEventStartEpochTimeForDemonstrating(services, eventState);
    }

    // Detect that this player has NOT visited the Daily Rewards feature yet OR that the event has started a new month
    if (eventState.playerStatus === undefined || eventState.playerStatus.startEpochTime !== eventState.startEpochTime) {
        eventState.playerStatus = await startEventForPlayer(services, eventState);
        eventState.result.firstVisit = true;
    }

    Object.assign(eventState.result, eventState.configData);
}

function updateState(services, eventState) {
    // Calculate the total time of the event in seconds so we can easily check if the current duration in seconds is past the end of the event.
    eventState.eventTotalSeconds = eventState.configData.totalDays * eventState.configData.secondsPerDay;

    // Convert epoch time (milliseconds) to seconds.
    eventState.eventSecondsPassed = (eventState.epochTime - eventState.startEpochTime) / 1000;
    eventState.result.isStarted = eventState.eventSecondsPassed >= 0;
    eventState.result.isEnded = eventState.eventSecondsPassed >= eventState.eventTotalSeconds;

    if (eventState.result.isStarted && !eventState.result.isEnded) {
        eventState.eventDay = Math.floor((eventState.epochTime - eventState.startEpochTime) / eventState.configData.secondsPerDay / 1000);
        eventState.lastDayClaimed = Math.floor((eventState.playerStatus.lastClaimTime - eventState.startEpochTime) / eventState.configData.secondsPerDay / 1000);

        // Calculate seconds remaining in the current day by calculating the seconds offset for the next day and subracting the seconds passed in event so far
        eventState.result.secondsTillNextDay = (eventState.eventDay + 1) * eventState.configData.secondsPerDay - eventState.eventSecondsPassed;

        if (eventState.eventDay > eventState.lastDayClaimed) {
            eventState.result.secondsTillClaimable = 0;
            eventState.result.daysRemaining = eventState.configData.totalDays - eventState.eventDay;
        } else {
            eventState.result.secondsTillClaimable = eventState.result.secondsTillNextDay;
            eventState.result.daysRemaining = eventState.configData.totalDays - eventState.eventDay - 1;
        }

        // If the player has claimed the last day of the month, end the event now to signal that further claims are not possible.
        if (eventState.result.daysRemaining <= 0) {
            eventState.result.isEnded = true;
        }
    }

    eventState.result.daysClaimed = eventState.playerStatus.daysClaimed;
}

async function getRemoteConfigData(services) {
    const response = await services.remoteConfig.assignSettingsGet(
        services.projectId,
        services.environmentId,
        'settings',
        ["DAILY_REWARDS_CONFIG"]
    );

    if (response.data.configs &&
        response.data.configs.settings &&
        response.data.configs.settings.DAILY_REWARDS_CONFIG) {
        return response.data.configs.settings.DAILY_REWARDS_CONFIG;
    }
    
    throw new RemoteConfigKeyMissingError("Failed to get DAILY_REWARDS_CONFIG.");
}

// Retrieve the epoch time for the start of the event.
// Important: this value would NORMALLY be stored in Remote Config, but, to facilitate testing, we are using
//            Cloud Save so each user can start the event correctly when they first enter this Use Case Sample.
async function getEventStartEpochTime(services) {
    return await getCloudSaveResult(services, "DAILY_REWARDS_START_EPOCH_TIME");
}

async function getPlayerStatus(services) {
    const results = await getCloudSaveResult(services, "DAILY_REWARDS_STATUS");
    if (results) {
        return JSON.parse(results);
    }

    return undefined;
}

async function getCloudSaveResult(services, key) {
    const response = await services.cloudSave.getItems(services.projectId, services.playerId, [ key ]);

    if (response.data.results &&
        response.data.results.length > 0 &&
        response.data.results[0]) {
        return response.data.results[0].value;
    }

    return undefined;
}

// Setup start epoch time. This is ONLY needed to demonstrate this Use Case Sample. 
// Normally this value would be set to the first day of the month and stored in Remote Config to control the event for all
// players, but here it's set in Cloud Save for this Use Case Sample to facilitate testing.
async function setEventStartEpochTimeForDemonstrating(services, eventState) {
    services.logger.info("Setting start event time in Cloud Save. This would normally set in Remote Config to the first day of the month.");

    await services.cloudSave.setItem(services.projectId, services.playerId, { key: "DAILY_REWARDS_START_EPOCH_TIME", value: eventState.epochTime } );

    return eventState.epochTime;
}

// Setup event for this player for the first time player visits the Daily Rewards event for the current month
async function startEventForPlayer(services, eventState) {
    const playerStatus = {
        startEpochTime: eventState.startEpochTime,
        daysClaimed: 0,
        lastClaimTime: 0
    }

    await services.cloudSave.setItem(services.projectId, services.playerId, { key: "DAILY_REWARDS_STATUS", value: JSON.stringify(playerStatus) } );

    services.logger.info("New player status: " + JSON.stringify(playerStatus));

    return playerStatus;
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

class RemoteConfigKeyMissingError extends CloudCodeCustomError {
  constructor(message) {
    super(message);
    this.name = "RemoteConfigKeyMissingError";
    this.status = 2;
  }
}
