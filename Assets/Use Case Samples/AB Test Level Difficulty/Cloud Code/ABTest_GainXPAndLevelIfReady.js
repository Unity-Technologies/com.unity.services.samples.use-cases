// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { DataApi } = require("@unity-services/cloud-save-1.0");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");

const rateLimitError = 429;
const validationError = 400;

module.exports = async ({ params, context, logger }) => {
    const { projectId, playerId, environmentId, accessToken} = context;
    const economy = new CurrenciesApi({ accessToken });
    const cloudSave = new DataApi({ accessToken });
    const remoteConfig = new SettingsApi({ accessToken });

    let updatedPlayerLevel, updatedPlayerXP, xpIncreaseAmount, shouldPlayerLevelUp, levelUpRewards;

    try
    {
        // Get Current Values
        const promiseResponses = await Promise.all([
            getCloudSaveData(cloudSave, projectId, playerId),
            getRemoteConfigData(remoteConfig, projectId, environmentId)
        ]);

        const currentLevel = promiseResponses[0].currentLevel;
        const currentXP = promiseResponses[0].currentXP;
        const xpNeededForNextLevel = promiseResponses[1].xpNeededForNextLevel;
        xpIncreaseAmount = promiseResponses[1].xpIncreaseAmount;

        // Do Calculations
        updatedPlayerLevel = currentLevel;
        updatedPlayerXP = currentXP + xpIncreaseAmount;
        shouldPlayerLevelUp = updatedPlayerXP >= xpNeededForNextLevel;

        if (shouldPlayerLevelUp)
        {
            updatedPlayerLevel = currentLevel + 1;
            updatedPlayerXP = 0;
        }

        // Set Updated Values
        levelUpRewards = {};
        if (shouldPlayerLevelUp)
        {
            const promiseResponses = await Promise.all([
                distributeLevelUpRewards(economy, projectId, playerId),
                saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedPlayerXP, updatedPlayerLevel)
            ]);

            levelUpRewards = promiseResponses[0];
        }
        else
        {
            await saveUpdatedPlayerXP(cloudSave, projectId, playerId, updatedPlayerXP);
        }
    }
    catch (error)
    {
        transformAndThrowCaughtError(error);
    }

    return {
        "playerLevel": updatedPlayerLevel,
        "playerXP": updatedPlayerXP,
        "playerXPUpdateAmount": xpIncreaseAmount,
        "didLevelUp": shouldPlayerLevelUp,
        "levelUpRewards": levelUpRewards
    };
};

async function getCloudSaveData(cloudSave, projectId, playerId)
{
    const getItemsResponse = await cloudSave.getItems(
        projectId,
        playerId,
        ["PLAYER_LEVEL", "PLAYER_XP"]
    );

    if (getItemsResponse.data.results &&
        getItemsResponse.data.results.length > 0 &&
        getItemsResponse.data.results[0] &&
        getItemsResponse.data.results[1])
    {
        return {
            currentLevel: getItemsResponse.data.results[0].value,
            currentXP: getItemsResponse.data.results[1].value
        };
    }

    throw new CloudCodeCustomError("Failed to get initial Player Level or Player XP data from Cloud Save.");
}

async function getRemoteConfigData(remoteConfig, projectId, environmentId)
{
    const getRemoteConfigSettingsResponse = await remoteConfig.assignSettingsGet(
        projectId,
        environmentId,
        'settings',
        ["XP_INCREASE", "LEVEL_UP_XP_NEEDED"]
    );

    if (getRemoteConfigSettingsResponse.data.configs &&
        getRemoteConfigSettingsResponse.data.configs.settings &&
        getRemoteConfigSettingsResponse.data.configs.settings.XP_INCREASE &&
        getRemoteConfigSettingsResponse.data.configs.settings.LEVEL_UP_XP_NEEDED)
    {
        return {
            xpIncreaseAmount: getRemoteConfigSettingsResponse.data.configs.settings.XP_INCREASE,
            xpNeededForNextLevel: getRemoteConfigSettingsResponse.data.configs.settings.LEVEL_UP_XP_NEEDED
        };
    }

    throw new CloudCodeCustomError("Failed to get XP_INCREASE or LEVEL_UP_XP_NEEDED data from Remote Config.");
}

async function distributeLevelUpRewards(economy, projectId, playerId)
{
    const rewardId = "COIN";
    const rewardAmount = 100;
    const updatedCurrency = await economy.incrementPlayerCurrencyBalance(projectId, playerId, rewardId, { rewardId, amount: rewardAmount });
    return { currencyId: updatedCurrency.data.currencyId, rewardAmount: rewardAmount, balance: updatedCurrency.data.balance };
}

async function saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedPlayerXP, updatedPlayerLevel)
{
    await cloudSave.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: "PLAYER_XP", value: updatedPlayerXP },
                { key: "PLAYER_LEVEL", value: updatedPlayerLevel },
            ]
        }
    );
}

async function saveUpdatedPlayerXP(cloudSave, projectId, playerId, updatedPlayerXP)
{
    await cloudSave.setItem(
        projectId,
        playerId,
        { key: "PLAYER_XP", value: updatedPlayerXP }
    );
}

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
function transformAndThrowCaughtError(error) {
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
        if (error.response.status === rateLimitError)
        {
            result.retryAfter = error.response.headers['retry-after'];
        }
        else if (error.response.status === validationError)
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
