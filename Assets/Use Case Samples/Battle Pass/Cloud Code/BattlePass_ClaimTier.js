// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.2");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { InventoryApi } = require("@unity-services/economy-2.3");

const badRequestError = 400;
const tooManyRequestsError = 429;

const tierState = { Locked: 0, Unlocked: 1, Claimed: 2 };
const seasonTierStatesDefault = [ 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 ];
const playerStateDefault = {
    battlePassSeasonXp                      : 0,
    battlePassSeasonTierStates              : seasonTierStatesDefault,
    battlePassLatestSeasonActivityTimestamp : 0,
    battlePassLatestSeasonActivityEventKey  : "",
    battlePassPurchasedTimestamp            : 0,
    battlePassPurchasedSeason               : "",
}

const cloudSaveKeySeasonXP = "BATTLE_PASS_SEASON_XP";
const cloudSaveKeySeasonTierStates = "BATTLE_PASS_SEASON_TIER_STATES";
const cloudSaveKeyLatestSeasonActivityTimestamp = "BATTLE_PASS_LATEST_SEASON_ACTIVITY_TIMESTAMP";
const cloudSaveKeyLatestSeasonActivityEventKey = "BATTLE_PASS_LATEST_SEASON_ACTIVITY_EVENT_KEY";
const cloudSaveKeyBattlePassPurchasedTimestamp = "BATTLE_PASS_PURCHASED_TIMESTAMP";
const cloudSaveKeyBattlePassPurchasedSeason = "BATTLE_PASS_PURCHASED_SEASON";

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, environmentId, playerId, accessToken } = context;
        const cloudSaveApi = new DataApi({ accessToken });
        const remoteConfigApi = new SettingsApi();
        const economyCurrencyApi = new CurrenciesApi({ accessToken });
        const economyInventoryApi = new InventoryApi({ accessToken });

        const timestamp = _.now();
        const timestampMinutes = getTimestampMinutes(timestamp);

        let returnObject = {};

        const remoteConfigData = await getRemoteConfigData(remoteConfigApi, projectId, environmentId, playerId, timestampMinutes);

        let playerState = await getCloudSaveData(cloudSaveApi, projectId, playerId);

        if (shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp)) {
            playerState = _.clone(playerStateDefault);
        }

        validateClaim(playerState, params.tierIndex);

        returnObject.grantedRewards = getRewardsFromRemoteConfigData(remoteConfigData, playerState, params.tierIndex);

        await grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, returnObject.grantedRewards);

        playerState.battlePassSeasonTierStates = playerState.battlePassSeasonTierStates;
        playerState.battlePassSeasonTierStates[params.tierIndex] = tierState.Claimed;

        returnObject.seasonTierStates = playerState.battlePassSeasonTierStates;

        await setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp);

        return returnObject;
    } catch (error) {
        transformAndThrowCaughtException(error);
    }
};

function getTimestampMinutes(timestamp) {
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function getRemoteConfigData(remoteConfigApi, projectId, environmentId, playerId, timestampMinutes) {
    // get the current season configuration
    const result = await remoteConfigApi.assignSettings({
        projectId,
        environmentId,
        "userId": playerId,
        // associate the current timestamp with the user in Remote Config to affect which season Game Override we get
        "attributes": {
            "unity": {},
            "app": {},
            "user": {
                "timestampMinutes": timestampMinutes
            },
        },
        "key": ["EVENT_KEY", "BATTLE_PASS_REWARDS_FREE", "BATTLE_PASS_REWARDS_PREMIUM"]
    });

    // the returned configuration contains all the tier rewards for the current season
    return result.data.configs.settings;
}

async function getCloudSaveData(cloudSaveApi, projectId, playerId) {
    const getItemsResponse = await cloudSaveApi.getItems(
        projectId,
        playerId,
        [
            cloudSaveKeySeasonXP,
            cloudSaveKeySeasonTierStates,
            cloudSaveKeyLatestSeasonActivityTimestamp,
            cloudSaveKeyLatestSeasonActivityEventKey,
            cloudSaveKeyBattlePassPurchasedTimestamp,
            cloudSaveKeyBattlePassPurchasedSeason,
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
    if (!playerState.battlePassSeasonTierStates) {
        return true;
    }

    // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
    // current season's key is the same as the key of the season that was active the last time the event was completed.
    if (remoteConfigData.EVENT_KEY !== playerState.battlePassLatestSeasonActivityEventKey) {
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

    if (playerState.battlePassLatestSeasonActivityTimestamp < currentSeasonEarliestPotentialStartTimestamp) {
        return true;
    }

    return false;
}

function validateClaim(playerState, tierToClaimArrayIndex) {
    if (tierToClaimArrayIndex < 0 ||
        tierToClaimArrayIndex >= seasonTierStatesDefault.length) {
        throw new Error("The given index doesn't fall within the tier list's bounds.");
    }

    if (playerState.battlePassSeasonTierStates[tierToClaimArrayIndex] === tierState.Claimed) {
        throw new Error("Player has already claimed this tier.");
    }

    if (playerState.battlePassSeasonTierStates[tierToClaimArrayIndex] === tierState.Locked) {
        throw new Error("The requested tier cannot be claimed because it's locked.");
    }
}

function getRewardsFromRemoteConfigData(remoteConfigData, playerState, claimTierIndex) {
    let returnRewards = [];

    const freeReward = remoteConfigData["BATTLE_PASS_REWARDS_FREE"][claimTierIndex];
    const premiumReward = remoteConfigData["BATTLE_PASS_REWARDS_PREMIUM"][claimTierIndex];

    if (freeReward !== null) {
        returnRewards.push(freeReward);
    }

    const ownsBattlePass = playerState.battlePassPurchasedSeason === remoteConfigData.EVENT_KEY;

    if (ownsBattlePass && premiumReward !== null) {
        returnRewards.push(premiumReward);
    }

    return returnRewards;
}

async function grantRewards(currencyApi, inventoryApi, projectId, playerId, rewardsToGrant) {
    for (const reward of rewardsToGrant) {
        switch (reward.service) {
            case "currency":
                await grantCurrency(currencyApi, projectId, playerId, reward.id, reward.quantity);
                break;

            case "inventory":
                await grantInventoryItem(inventoryApi, projectId, playerId, reward.id, reward.quantity);
                break;
        }
    }
}

async function grantCurrency(currencyApi, projectId, playerId, currencyId, amount) {
    const currencyModifyBalanceRequest = { currencyId, amount };
    const requestParameters = { projectId, playerId, currencyId, currencyModifyBalanceRequest };
    await currencyApi.incrementPlayerCurrencyBalance(requestParameters);
}

async function grantInventoryItem(inventoryApi, projectId, playerId, inventoryItemId, amount) {
    for (let i = 0; i < amount; i++) {
        const addInventoryRequest = { inventoryItemId: inventoryItemId };
        const requestParameters = { projectId, playerId, addInventoryRequest };
        await inventoryApi.addInventoryItem(requestParameters);
    }
}

async function setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp) {
    await cloudSaveApi.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: cloudSaveKeySeasonXP, value: playerState.battlePassSeasonXp },
                { key: cloudSaveKeySeasonTierStates, value: playerState.battlePassSeasonTierStates },
                { key: cloudSaveKeyLatestSeasonActivityTimestamp, value: timestamp },
                { key: cloudSaveKeyLatestSeasonActivityEventKey, value: remoteConfigData.EVENT_KEY },
                { key: cloudSaveKeyBattlePassPurchasedTimestamp, value: playerState.battlePassPurchasedTimestamp },
                { key: cloudSaveKeyBattlePassPurchasedSeason, value: playerState.battlePassPurchasedSeason },
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
    } else {
        result.name = error.name;
        result.message = error.message;
    }

    throw new Error(JSON.stringify(result));
}
