// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");

module.exports = async ({ context }) => {
    const { projectId, playerId, accessToken} = context;
    const economy = new CurrenciesApi({ accessToken });
    const remoteConfig = new SettingsApi();
    const cloudSave = new DataApi({ accessToken });

    const timestamp = _.now();
    let grantedRewards = [];

    const timestampMinutes = getTimestampMinutes(timestamp);
    const { remoteConfigData, cloudSaveData } = await GetData(remoteConfig, cloudSave, projectId, playerId, timestampMinutes);

    const isDistributionAllowed = isRewardDistributionAllowed(remoteConfigData, cloudSaveData, timestamp);
    if (isDistributionAllowed) {
        const rewards = getRewardsFromRemoteConfig(remoteConfigData);
        grantedRewards = await grantRewards(economy, projectId, playerId, rewards);
        await saveEventCompleted(cloudSave, projectId, playerId, timestamp, remoteConfigData);
    }

    return { grantedRewards };
};

function getTimestampMinutes(timestamp)
{
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function GetData(remoteConfig, cloudSave, projectId, playerId, timestampMinutes)
{
    return await Promise.all([
        getRemoteConfigData(remoteConfig, projectId, playerId, timestampMinutes),
        getCloudSaveData(cloudSave, projectId, playerId)
    ]).then(function(promiseResponses) {
        return {
            'remoteConfigData': promiseResponses[0],
            'cloudSaveData': promiseResponses[1]
        }
    });
}

async function getRemoteConfigData(remoteConfig, projectId, playerId, timestampMinutes)
{
    const result = await remoteConfig.assignSettings({
        projectId,
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

async function getCloudSaveData(cloudSave, projectId, playerId)
{
    const getItemsResponse = await cloudSave.getItems(
        projectId,
        playerId,
        ["LAST_COMPLETED_EVENT", "LAST_COMPLETED_EVENT_TIMESTAMP"]
    );

    if (getItemsResponse.data.results &&
        getItemsResponse.data.results.length > 0 &&
        getItemsResponse.data.results[0] &&
        getItemsResponse.data.results[1])
    {
        return {
            lastCompletedEvent: getItemsResponse.data.results[0].value,
            lastCompletedEventTimestamp: getItemsResponse.data.results[1].value
        };
    }

    return {
        lastCompletedEvent: "",
        lastCompletedEventTimestamp: 0
    }
}

function isRewardDistributionAllowed(remoteConfigData, cloudSaveData, timestamp)
{
    const activeEventKey = remoteConfigData["EVENT_KEY"];
    const activeEventDurationMinutes = remoteConfigData["EVENT_TOTAL_DURATION_MINUTES"];
    const lastCompletedEvent = cloudSaveData.lastCompletedEvent;
    const lastCompletedEventTimestamp = cloudSaveData.lastCompletedEventTimestamp;

    if (activeEventKey !== lastCompletedEvent)
    {
        return true;
    }

    // Because the seasonal events cycle, and do not have unique keys for each cycle, we need to check that
    // the last time the event challenge was completed, is outside of the possible timespan for the current
    // event.
    return isLastCompletedEventTimestampOld(timestamp, activeEventDurationMinutes, lastCompletedEventTimestamp);
}

function isLastCompletedEventTimestampOld(currentTime, activeEventDurationMinutes, lastCompletedEventTimestamp)
{
    const millisecondsInAMinute = 60000;
    const eventDurationMilliseconds = activeEventDurationMinutes * millisecondsInAMinute;
    const earliestPotentialStartForActiveEvent = currentTime - eventDurationMilliseconds;

    return lastCompletedEventTimestamp < earliestPotentialStartForActiveEvent;
}

function getRewardsFromRemoteConfig(remoteConfigData)
{
    let eventRewards = [];

    const rewardResults = remoteConfigData["CHALLENGE_REWARD"];

    if (rewardResults != null && rewardResults["rewards"] != null)
    {
        eventRewards = rewardResults["rewards"];
    }

    return eventRewards;
}

async function grantRewards(economy, projectId, playerId, rewards)
{
    const incrementedRewards = [];

    for (let i = 0; i < rewards.length; i++)
    {
        let currencyId = rewards[i]["id"];
        let amount = rewards[i]["quantity"];
        if (currencyId != null && amount != null)
        {
            let currencyBalance = await economy.incrementPlayerCurrencyBalance(projectId, playerId, currencyId, { currencyId, amount });
            let rewardBalance = {
                "id": currencyBalance.data.currencyId,
                "quantity": currencyBalance.data.balance,
                "spriteAddress": rewards[i].spriteAddress};
            incrementedRewards.push(rewardBalance);
        }
    }

    return incrementedRewards;
}

async function saveEventCompleted(cloudSave, projectId, playerId, timestamp, remoteConfigData)
{
    await cloudSave.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: "LAST_COMPLETED_EVENT", value: remoteConfigData["EVENT_KEY"] },
                { key: "LAST_COMPLETED_EVENT_TIMESTAMP", value: timestamp },
            ]
        }
    );
}
