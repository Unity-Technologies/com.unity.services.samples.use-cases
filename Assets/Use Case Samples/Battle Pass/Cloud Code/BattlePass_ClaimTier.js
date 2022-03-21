// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { InventoryApi } = require("@unity-services/economy-2.0");

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

    const claimTierKey = "BATTLE_PASS_TIER_" + (params.tierIndex + 1);

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const remoteConfigApi = new SettingsApi();
    const economyCurrencyApi = new CurrenciesApi({ accessToken });
    const economyInventoryApi = new InventoryApi({ accessToken });

    const timestamp = _.now();
    const timestampMinutes = getTimestampMinutes(timestamp);

    let returnObject = {};

    try {
        const remoteConfigData = await getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes, claimTierKey);

        let playerState = await getCloudSaveData(cloudSaveApi, projectId, playerId);

        if (shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp)) {
            playerState = _.clone(playerStateDefault);
        }

        validateClaim(playerState, params.tierIndex);

        returnObject.grantedRewards = getRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey);

        await grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, returnObject.grantedRewards);

        playerState.seasonTierStates = playerState.seasonTierStates;
        playerState.seasonTierStates[params.tierIndex] = tierState.Claimed;

        returnObject.seasonTierStates = playerState.seasonTierStates;

        await setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp);
    }
    catch (error) {
        transformAndThrowCaughtException(error);
    }

    return returnObject;
};

function getTimestampMinutes(timestamp)
{
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes, claimTierKey)
{
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
        "key": ["EVENT_KEY", claimTierKey]
    });

    // the returned configuration contains all the tier rewards for the current season
    return result.data.configs.settings;
}

async function getCloudSaveData(cloudSaveApi, projectId, playerId)
{
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

function cloudSaveResponseToObject(getItemsResponse)
{
    let returnObject = {};

    getItemsResponse.data.results.forEach(item => {
        const key = _.camelCase(item.key);
        returnObject[key] = item.value;
    });

    return returnObject;
}

function shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp)
{
    // If the progress object is empty, then it might be the first time this player has ever used this function.
    // Resetting will create a fresh object.
    if (!playerState.seasonTierStates)
    {
        return true;
    }

    // Because the seasonal events repeat and do not have unique keys for each iteration, we first check whether the
    // current season's key is the same as the key of the season that was active the last time the event was completed.
    if (remoteConfigData.EVENT_KEY !== playerState.latestSeasonActivityEventKey)
    {
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

    if (playerState.latestSeasonActivityTimestamp < currentSeasonEarliestPotentialStartTimestamp)
    {
        return true;
    }

    return false;
}

function validateClaim(playerState, tierToClaimArrayIndex)
{
    if (tierToClaimArrayIndex < 0 ||
        tierToClaimArrayIndex >= seasonTierStatesDefault.length)
    {
        throw new Error("The given index doesn't fall within the tier list's bounds.");
    }

    if (playerState.seasonTierStates[tierToClaimArrayIndex] === tierState.Claimed)
    {
        throw new Error("Player has already claimed this tier.");
    }

    if (playerState.seasonTierStates[tierToClaimArrayIndex] === tierState.Locked)
    {
        throw new Error("The requested tier cannot be claimed because it's locked.");
    }
}

function getRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey)
{
    let returnRewards = [];

    const tierRewards = remoteConfigData[claimTierKey];

    if (tierRewards != null)
    {
        if (tierRewards.reward != null)
        {
            returnRewards.push(tierRewards.reward);
        }

        // Does the current player own the current Battle Pass?
        // By this point in the code, the BattlePass would have been reset if it was too old.
        const ownsBattlePass = playerState.battlePassPurchasedEventKey === remoteConfigData.EVENT_KEY;

        if (ownsBattlePass && tierRewards.battlePassReward != null)
        {
            returnRewards.push(tierRewards.battlePassReward);
        }
    }

    return returnRewards;
}

async function grantRewards(currencyApi, inventoryApi, projectId, playerId, rewardsToGrant)
{
    for (const reward of rewardsToGrant)
    {
        switch (reward.service)
        {
            case "currency":
                await grantCurrency(currencyApi, projectId, playerId, reward.id, reward.quantity);
                break;

            case "inventory":
                grantInventoryItem(inventoryApi, projectId, playerId, reward.id, reward.quantity);
                break;
        }
    }
}

async function grantCurrency(currencyApi, projectId, playerId, currencyId, amount)
{
    await currencyApi.incrementPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, amount });
}

async function grantInventoryItem(inventoryApi, projectId, playerId, inventoryItemId, amount)
{
    for (let i = 0; i < amount; i++)
    {
        await inventoryApi.addInventoryItem(projectId, playerId, { inventoryItemId: inventoryItemId });
    }
}

async function setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp)
{
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
        if (error.response.status === 429) {
            result.retryAfter = error.response.headers['retry-after'];
        }
        if (error.response.status === 400) {
            let arr = [];
            _.forEach(error.response.data.errors, error => {
                arr = _.concat(arr, error.messages);
            });
            result.additionalDetails = arr;
        }
    }
    else
    {
        result.title = error.name;
        result.message = error.message;
    }

    throw new Error(JSON.stringify(result));
}
