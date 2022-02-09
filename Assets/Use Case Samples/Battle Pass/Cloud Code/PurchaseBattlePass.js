// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { DataApi } = require("@unity-services/cloud-save-1.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");
const { PurchasesApi } = require("@unity-services/economy-2.0");
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

    const { projectId, playerId, accessToken } = context;
    const cloudSaveApi = new DataApi({ accessToken });
    const remoteConfigApi = new SettingsApi();
    const purchasesApi = new PurchasesApi({ accessToken });
    const economyCurrencyApi = new CurrenciesApi({ accessToken });
    const economyInventoryApi = new InventoryApi({ accessToken });

    const timestamp = _.now();
    const timestampMinutes = getTimestampMinutes(timestamp);

    const remoteConfigData = await getRemoteConfigData(remoteConfigApi, projectId, playerId, timestampMinutes);
    let playerState = await getCloudSaveData(cloudSaveApi, projectId, playerId);

    if (shouldResetBattlePassProgress(remoteConfigData, playerState, timestamp))
    {
        playerState = _.clone(playerStateDefault);
    }

    let returnObject = {};

    returnObject.purchaseResult = await tryPurchase(purchasesApi, projectId, playerId, remoteConfigData, playerState, timestamp);

    if (returnObject.purchaseResult === "success")
    {
        returnObject.grantedRewards = await grantPastClaimedBattlePassRewards(
            projectId, playerId, economyCurrencyApi, economyInventoryApi, remoteConfigData, playerState);
    }

    returnObject.seasonTierStates = playerState.seasonTierStates;

    // even if the purchase failed, let's save the player state in case it was reset due to season change
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
        },
        "key": ["EVENT_KEY", "BATTLE_PASS_"]
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

async function tryPurchase(purchasesApi, projectId, playerId, remoteConfigData, playerState, timestamp)
{
    if (playerState.battlePassPurchasedEventKey === remoteConfigData.EVENT_KEY)
    {
        return "invalidAlreadyOwned";
    }

    if (playerState.battlePassPurchasedEventKey != "")
    {
        return "invalidUnknownBattlePassState";
    }

    try
    {
        await purchasesApi.makeVirtualPurchase(projectId, playerId, { id: "BATTLE_PASS" });
    }
    catch (error)
    {
        if (error.response)
        {
            return JSON.stringify(error.response.data);
        }
        logger.error(JSON.stringify(error.message));
        return error;
    }

    playerState.battlePassPurchasedTimestamp = timestamp;
    playerState.battlePassPurchasedEventKey = remoteConfigData.EVENT_KEY;

    return "success";
}

async function grantPastClaimedBattlePassRewards(
    projectId, playerId, economyCurrencyApi, economyInventoryApi, remoteConfigData, playerState)
{
    let returnRewards = [];

    for (let i = 0; i < playerState.seasonTierStates.length; i++)
    {
        const tierStateToTest = playerState.seasonTierStates[i];

        if (tierStateToTest !== tierState.Claimed)
        {
            continue;
        }

        const claimTierKey = "BATTLE_PASS_TIER_" + (i + 1);

        tierRewards = getBattlePassRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey)

        await grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, tierRewards);

        returnRewards = returnRewards.concat(tierRewards);
    }

    return returnRewards;
}

function getBattlePassRewardsFromRemoteConfig(remoteConfigData, playerState, claimTierKey)
{
    let returnRewards = [];

    const tierRewards = remoteConfigData[claimTierKey];

    if (tierRewards != null)
    {
        // this method trusts that the Battle Pass is owned
        returnRewards.push(tierRewards.battlePassReward);
    }

    return returnRewards;
}

async function grantRewards(economyCurrencyApi, economyInventoryApi, projectId, playerId, rewardsToGrant)
{
    for (const reward of rewardsToGrant)
    {
        switch (reward.service)
        {
            case "currency":
                await grantCurrency(economyCurrencyApi, projectId, playerId, reward.id, reward.quantity);
                break;

            case "inventory":
                grantInventoryItem(economyInventoryApi, projectId, playerId, reward.id, reward.quantity);
                break;
        }
    }
}

async function grantCurrency(economyCurrencyApi, projectId, playerId, currencyId, amount)
{
    await economyCurrencyApi.incrementPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, amount });
}

async function grantInventoryItem(economyInventoryApi, projectId, playerId, inventoryItemId, amount)
{
    for (let i = 0; i < amount; i++)
    {
        await economyInventoryApi.addInventoryItem(projectId, playerId, { inventoryItemId: inventoryItemId });
    }
}

// this should only be executed if the purchase was a success
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
