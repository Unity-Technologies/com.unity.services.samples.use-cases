// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.2");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { PurchasesApi } = require("@unity-services/economy-2.3");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { InventoryApi } = require("@unity-services/economy-2.3");

const badRequestError = 400;
const unprocessableEntityError = 422;
const tooManyRequestsError = 429;

const tierState = { Locked: 0, Unlocked: 1, Claimed: 2 };
const seasonTierStatesDefault = [ 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 ];
const playerStateDefault = {
    seasonXp                     : 0,
    seasonTierStates             : seasonTierStatesDefault,
    latestSeasonActivityTimestamp: 0,
    latestSeasonActivityEventKey : "",
    battlePassPurchasedTimestamp : 0,
    battlePassPurchasedEventKey  : "",
}

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, accessToken } = context;
        const cloudSaveApi = new DataApi({ accessToken });
        const remoteConfigApi = new SettingsApi();
        const purchasesApi = new PurchasesApi({ accessToken });
        const economyCurrencyApi = new CurrenciesApi({ accessToken });
        const economyInventoryApi = new InventoryApi({ accessToken });

        const timestamp = _.now();
        const timestampMinutes = getTimestampMinutes(timestamp);

        let returnObject = {};

        const remoteConfigData = await getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes);
        let playerState = await getCloudSaveData(cloudSaveApi, projectId, playerId);

        if (shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp)) {
            playerState = _.clone(playerStateDefault);
        }

        await purchaseBattlePass(purchasesApi, projectId, playerId, remoteConfigData, playerState, timestamp);

        returnObject.grantedRewards = await grantPastClaimedBattlePassRewards(
            projectId, playerId, economyCurrencyApi, economyInventoryApi, remoteConfigData, playerState);

        returnObject.seasonTierStates = playerState.seasonTierStates;

        await setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp);

        return returnObject;
    }
    catch (error) {
        transformAndThrowCaughtException(error);
    }
};

function getTimestampMinutes(timestamp) {
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes) {
    // get the current season configuration
    const result = await remoteConfigApi.assignSettings({
        projectId,
        "userId": playerId,
        // associate the current timestamp with the user in Remote Config to affect which season Game Override we get
        "attributes": {
            "unity": {},
            "app": {},
            "user": {
                "timestampMinutes": timestampMinutes
            },
        },
        "key": ["EVENT_KEY", "BATTLE_PASS_"]
    });

    // the returned configuration contains all the tier rewards for the current season
    return result.data.configs.settings;
}

async function getCloudSaveData(cloudSaveApi, projectId, playerId) {
    const getItemsResponse = await cloudSaveApi.getItems(
        projectId,
        playerId,
        [
            "SEASON_XP",
            "SEASON_TIER_STATES",
            "LATEST_SEASON_ACTIVITY_TIMESTAMP",
            "LATEST_SEASON_ACTIVITY_EVENT_KEY",
            "BATTLE_PASS_PURCHASED_TIMESTAMP",
            "BATTLE_PASS_PURCHASED_EVENT_KEY",
        ]
    );

    const getItemsResponseObject = cloudSaveResponseToObject(getItemsResponse);

    let returnObject = {};

    _.merge(returnObject, playerStateDefault, getItemsResponseObject);

    return returnObject;
}

function cloudSaveResponseToObject(getItemsResponse) {
    let returnObject = {};

    getItemsResponse.data.results.forEach(item => {
        const key = _.camelCase(item.key);
        returnObject[key] = item.value;
    });

    return returnObject;
}

function shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp) {
    // If the progress object is empty, then it might be the first time this player has ever used this function.
    // Resetting will create a fresh object.
    if (!playerState.seasonTierStates) {
        return true;
    }

    // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
    // current season's key is the same as the key of the season that was active the last time the event was completed.
    if (remoteConfigData.EVENT_KEY !== playerState.latestSeasonActivityEventKey) {
        return true;
    }

    // Because the key of the season that was active the last time the event was completed is the same as the 
    // current season's key, we now need to check whether the timestamp of the last time the event was completed 
    // is so old that it couldn't possibly be from the current iteration of this season.
    //
    // We do these cyclical seasons for ease of demonstration in the sample project, however in a real world
    // implementation (where seasonal events last longer than a few minutes) you would likely create a new 
    // override in remote config each time an event period was starting.
    const currentEventDurationMinutes = remoteConfigData.EVENT_TOTAL_DURATION_MINUTES;
    const millisecondsPerMinute = 60000;
    const eventDurationMilliseconds = currentEventDurationMinutes * millisecondsPerMinute;
    const currentSeasonEarliestPotentialStartTimestamp = timestamp - eventDurationMilliseconds;

    if (playerState.latestSeasonActivityTimestamp < currentSeasonEarliestPotentialStartTimestamp) {
        return true;
    }

    return false;
}

async function purchaseBattlePass(purchasesApi, projectId, playerId, remoteConfigData, playerState, timestamp) {
    if (playerState.battlePassPurchasedEventKey === remoteConfigData.EVENT_KEY) {
        throw new Error("Cannot purchase Battle Pass because you already own it for the current season.");
    }

    try {
        const playerPurchaseVirtualRequest = { id: "BATTLE_PASS" };
        const requestParameters = { projectId, playerId, playerPurchaseVirtualRequest };
        await purchasesApi.makeVirtualPurchase(requestParameters);

        playerState.battlePassPurchasedTimestamp = timestamp;
        playerState.battlePassPurchasedEventKey = remoteConfigData.EVENT_KEY;
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

async function grantPastClaimedBattlePassRewards(
    projectId, playerId, economyCurrencyApi, economyInventoryApi, remoteConfigData, playerState) {
    let returnRewards = [];

    for (let i = 0; i < playerState.seasonTierStates.length; i++) {
        const tierStateToTest = playerState.seasonTierStates[i];

        if (tierStateToTest !== tierState.Claimed) {
            continue;
        }

        const claimTierKey = "BATTLE_PASS_TIER_" + (i + 1);

        tierRewards = getBattlePassRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey)

        await grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, tierRewards);

        returnRewards = returnRewards.concat(tierRewards);
    }

    return returnRewards;
}

function getBattlePassRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey) {
    let returnRewards = [];

    const tierRewards = remoteConfigData[claimTierKey];

    if (tierRewards != null) {
        // this method trusts that the Battle Pass is owned
        returnRewards.push(tierRewards.battlePassReward);
    }

    return returnRewards;
}

async function grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, rewardsToGrant) {
    for (const reward of rewardsToGrant) {
        switch (reward.service) {
            case "currency":
                await grantCurrency(economyCurrencyApi, projectId, playerId, reward.id, reward.quantity);
                break;

            case "inventory":
                grantInventoryItem(economyInventoryApi, projectId, playerId, reward.id, reward.quantity);
                break;
        }
    }
}

async function grantCurrency(economyCurrencyApi, projectId, playerId, currencyId, amount) {
    const currencyModifyBalanceRequest = { currencyId, amount };
    await economyCurrencyApi.incrementPlayerCurrencyBalance({ projectId, playerId, currencyId, currencyModifyBalanceRequest});
}

async function grantInventoryItem(economyInventoryApi, projectId, playerId, inventoryItemId, amount) {
    for (let i = 0; i < amount; i++) {
        const addInventoryRequest = { inventoryItemId: inventoryItemId };
        const requestParameters = { projectId, playerId, addInventoryRequest };
        await economyInventoryApi.addInventoryItem(requestParameters);
    }
}

// this should only be executed if the purchase was a success
async function setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp) {
    await cloudSaveApi.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: "SEASON_XP", value: playerState.seasonXp },
                { key: "SEASON_TIER_STATES", value: playerState.seasonTierStates },
                { key: "LATEST_SEASON_ACTIVITY_TIMESTAMP", value: timestamp },
                { key: "LATEST_SEASON_ACTIVITY_EVENT_KEY", value: remoteConfigData.EVENT_KEY },
                { key: "BATTLE_PASS_PURCHASED_TIMESTAMP", value: playerState.battlePassPurchasedTimestamp },
                { key: "BATTLE_PASS_PURCHASED_EVENT_KEY", value: playerState.battlePassPurchasedEventKey },
            ]
        }
    );
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
