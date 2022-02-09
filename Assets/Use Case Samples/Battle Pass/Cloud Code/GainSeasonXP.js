// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");

const tierState = { Locked: 0, Unlocked: 1, Claimed: 2 };
const seasonTierStatesDefault = [ 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 ];
const seasonXpNeededToUnlockTier = 100;
const playerStateDefault = {
    seasonXp                     : 0,
    seasonTierStates             : seasonTierStatesDefault,
    latestSeasonActivityTimestamp: 0,
    latestSeasonActivityEventKey : "",
    battlePassPurchasedTimestamp : 0,
    battlePassPurchasedEventKey  : "",
}

module.exports = async ({ params, context, logger }) => {

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const remoteConfigApi = new SettingsApi();

    const timestamp = _.now();
    const timestampMinutes = getTimestampMinutes(timestamp);

    const remoteConfigData = await getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes);
    let playerState = await getCloudSaveData(cloudSaveApi, projectId, playerId);

    if (shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp))
    {
        playerState = _.clone(playerStateDefault);
    }

    let returnObject = {
        unlockedNewTier: -1
    };

    returnObject.validationResult = validate(params.amount);

    if (returnObject.validationResult === "valid")
    {
        playerState.seasonXp += params.amount;

        returnObject.unlockedNewTier = unlockNewTiersIfEligible(remoteConfigData, playerState);
    }

    returnObject.seasonXp = playerState.seasonXp;
    returnObject.seasonTierStates = playerState.seasonTierStates;

    await setCloudSaveData(cloudSaveApi, projectId, playerId, remoteConfigData, playerState, timestamp);

    return returnObject;
};

function getTimestampMinutes(timestamp)
{
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes)
{
    // get the current season configuration
    const result = await remoteConfigApi.assignSettings({
        projectId,
        "userId": playerId,
        // associate the current timestamp with the user in Remote Config to affect which season campaign we get
        "attributes": {
            "unity": {},
            "app": {},
            "user": {
                "timestampMinutes": timestampMinutes
            },
        }
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

    if (remoteConfigData.EVENT_KEY !== playerState.latestSeasonActivityEventKey)
    {
        return true;
    }

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

function validate(xpIncreaseAmount)
{
    if (xpIncreaseAmount < 1)
    {
        return "invalidAmount";
    }

    return "valid";
}

function unlockNewTiersIfEligible(remoteConfigData, playerState)
{
    const oldHighestUnlockedTierIndex = getHighestNonLockedTier(playerState);
    const newHighestUnlockedTierIndex = Math.floor(playerState.seasonXp / seasonXpNeededToUnlockTier);

    if (newHighestUnlockedTierIndex > oldHighestUnlockedTierIndex)
    {
        // The current operation unlocked at least one new tier.
        // Loop to make sure we don't skip any tiers, in case the XP increase was huge.
        for (let i = newHighestUnlockedTierIndex; i >= 0; i--)
        {
            if (playerState.seasonTierStates[i] === tierState.Locked)
            {
                playerState.seasonTierStates[i] = tierState.Unlocked;
            }
        }

        return newHighestUnlockedTierIndex;
    }

    // Nothing new was unlocked.
    return -1;
}

function getHighestNonLockedTier(playerState)
{
    // Loop backwards through the tier states to find the highest one that's not locked.

    for (let i = playerState.seasonTierStates.length - 1; i >= 0; i--)
    {
        if (playerState.seasonTierStates[i] != tierState.Locked)
        {
            return i;
        }
    }

    // There are no tiers unlocked. In our current design, we don't expect this,
    // but this function should be ready for any possible future design change.
    return -1;
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
