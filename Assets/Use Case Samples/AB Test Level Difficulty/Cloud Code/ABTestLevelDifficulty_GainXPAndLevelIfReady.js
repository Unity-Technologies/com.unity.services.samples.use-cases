// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { DataApi } = require("@unity-services/cloud-save-1.2");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { SettingsApi } = require("@unity-services/remote-config-1.1");

const tooManyRequestsError = 429;
const badRequestError = 400;

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, environmentId, accessToken } = context;
        const economy = new CurrenciesApi({ accessToken });
        const cloudSave = new DataApi({ accessToken });
        const remoteConfig = new SettingsApi({ accessToken });

        // Get Current Values
        const promiseResponses = await Promise.all([
            getCloudSaveData(cloudSave, projectId, playerId),
            getRemoteConfigData(remoteConfig, projectId, environmentId)
        ]);

        const currentLevel = promiseResponses[0].currentLevel;
        const currentXP = promiseResponses[0].currentXP;
        const xpNeededForNextLevel = promiseResponses[1].xpNeededForNextLevel;
        const xpIncreaseAmount = promiseResponses[1].xpIncreaseAmount;

        // Do Calculations
        let updatedPlayerLevel = currentLevel;
        let updatedPlayerXP = currentXP + xpIncreaseAmount;
        const shouldPlayerLevelUp = updatedPlayerXP >= xpNeededForNextLevel;

        if (shouldPlayerLevelUp) {
            updatedPlayerLevel = currentLevel + 1;
            updatedPlayerXP = 0;
        }

        // Set Updated Values
        let levelUpRewards = {};
        if (shouldPlayerLevelUp) {
            const promiseResponses = await Promise.all([
                distributeLevelUpRewards(economy, projectId, playerId),
                saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedPlayerXP, updatedPlayerLevel)
            ]);

            levelUpRewards = promiseResponses[0];
        } else {
            await saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedPlayerXP, updatedPlayerLevel);
        }

        return {
            "playerLevel": updatedPlayerLevel,
            "playerXP": updatedPlayerXP,
            "playerXPUpdateAmount": xpIncreaseAmount,
            "didLevelUp": shouldPlayerLevelUp,
            "levelUpRewards": levelUpRewards
        };
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

async function getCloudSaveData(cloudSave, projectId, playerId) {
    const getItemsResponse = await cloudSave.getItems(
        projectId,
        playerId,
        ["AB_TEST_PLAYER_LEVEL", "AB_TEST_PLAYER_XP"]
    );

    return cloudSaveResponseToObject(getItemsResponse);
}

function cloudSaveResponseToObject(getItemsResponse) {
    const returnObject = {
        currentLevel: 1,
        currentXP: 0
    };

    if (getItemsResponse.data.results) {
        getItemsResponse.data.results.forEach(item => {
            if (item.key === "AB_TEST_PLAYER_LEVEL") {
                returnObject.currentLevel = item.value;
            } else if (item.key === "AB_TEST_PLAYER_XP") {
                returnObject.currentXP = item.value;
            }
        });
    }

    return returnObject;
}

async function getRemoteConfigData(remoteConfig, projectId, environmentId) {
    const getRemoteConfigSettingsResponse = await remoteConfig.assignSettingsGet(
        projectId,
        environmentId,
        'settings',
        ["AB_TEST_XP_INCREASE", "AB_TEST_LEVEL_UP_XP_NEEDED"]
    );

    if (getRemoteConfigSettingsResponse.data.configs &&
        getRemoteConfigSettingsResponse.data.configs.settings &&
        getRemoteConfigSettingsResponse.data.configs.settings.AB_TEST_XP_INCREASE &&
        getRemoteConfigSettingsResponse.data.configs.settings.AB_TEST_LEVEL_UP_XP_NEEDED) {

        return {
            xpIncreaseAmount: getRemoteConfigSettingsResponse.data.configs.settings.AB_TEST_XP_INCREASE,
            xpNeededForNextLevel: getRemoteConfigSettingsResponse.data.configs.settings.AB_TEST_LEVEL_UP_XP_NEEDED
        };
    }

    throw new CloudCodeCustomError("Failed to get AB_TEST_XP_INCREASE or AB_TEST_LEVEL_UP_XP_NEEDED data from Remote Config.");
}

async function distributeLevelUpRewards(economy, projectId, playerId) {
    const currencyId = "COIN";
    const amount = 100;

    const currencyModifyBalanceRequest = { currencyId, amount };
    const requestParameters = { projectId, playerId, currencyId, currencyModifyBalanceRequest };
    const updatedCurrency = await economy.incrementPlayerCurrencyBalance(requestParameters);

    return { currencyId: updatedCurrency.data.currencyId, rewardAmount: amount, balance: updatedCurrency.data.balance };
}

async function saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedPlayerXP, updatedPlayerLevel) {
    await cloudSave.setItemBatch(
        projectId,
        playerId,
        {
            data: [
                { key: "AB_TEST_PLAYER_XP", value: updatedPlayerXP },
                { key: "AB_TEST_PLAYER_LEVEL", value: updatedPlayerLevel },
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
