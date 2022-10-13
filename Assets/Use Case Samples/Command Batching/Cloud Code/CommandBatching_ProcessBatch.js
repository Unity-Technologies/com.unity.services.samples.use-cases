// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { SettingsApi } = require("@unity-services/remote-config-1.1");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

const commandKeys = {
    DefeatRedEnemy: "COMMANDBATCH_DEFEAT_RED_ENEMY",
    DefeatBlueEnemy: "COMMANDBATCH_DEFEAT_BLUE_ENEMY",
    OpenChest: "COMMANDBATCH_OPEN_CHEST",
    AchieveBonusGoal: "COMMANDBATCH_ACHIEVE_BONUS_GOAL",
    GameOver: "COMMANDBATCH_GAME_OVER"
};

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, environmentId, accessToken } = context;
        const economyCurrency = new CurrenciesApi({ accessToken });
        const cloudSave = new DataApi({ accessToken });
        const remoteConfig = new SettingsApi({ accessToken });

        validateCommands(params.commands);
        await processRewards(remoteConfig, economyCurrency, cloudSave, projectId, environmentId, playerId, params.commands);
    } catch (error) {
        transformAndThrowCaughtError(error);
    }
};

function validateCommands(commands) {
    validateBatchHasCorrectNumberOfCommands(commands);
    validateGameOverIsLastCommand(commands);
    validateCommandsAreInLegalOrder(commands);
}

function validateBatchHasCorrectNumberOfCommands(commands) {
    // The correct number of commands is 7 exactly (6 turns + 1 game over)
    if (commands.length > 7) {
        throw new InvalidArgumentError('Too many commands in batch.');
    }

    if (commands.length < 7) {
        throw new InvalidArgumentError('Not enough commands in batch.');
    }
}

function validateGameOverIsLastCommand(commands) {
    if (commands[commands.length - 1] !== commandKeys.GameOver) {
        throw new InvalidArgumentError('Last command must be Game Over.');
    }
}

function validateCommandsAreInLegalOrder(commands) {
    // any open chest command always follows defeat enemy command
    // achieveBonusGoal only appears in the list after openChest has appeared
    // no commands occur after gameOver

    let achieveBonusGoalValidMove = false;
    let openChestValidMove = false;
    let gameOverSeen = false;

    for (let i = 0; i < commands.length; i++) {
        if (gameOverSeen) {
            throw new InvalidArgumentError('There can be no commands after Game Over.');
        }

        switch (commands[i]) {
            case commandKeys.DefeatRedEnemy:
            case commandKeys.DefeatBlueEnemy:
                openChestValidMove = true;
                break;

            case commandKeys.OpenChest:
                if (openChestValidMove) {
                    achieveBonusGoalValidMove = true;
                } else {
                    throw new InvalidGameplayError('Chests can only be opened immediately after a red or blue enemy has been defeated.');
                }
                break;

            case commandKeys.AchieveBonusGoal:
                if (achieveBonusGoalValidMove) {
                    openChestValidMove = false;
                } else {
                    throw new InvalidGameplayError('Bonus goals can only be achieved once a chest has been opened.');
                }
                break;

            case commandKeys.GameOver:
                gameOverSeen = true;
                break;
        }
    }
}

async function processRewards(remoteConfig, economyCurrency, cloudSave, projectId, environmentId, playerId, commands) {
    const commandRewardOptions = await getCommandRewardOptions(remoteConfig, projectId, environmentId);
    const rewardsByService = groupCommandRewardsByService(commands, commandRewardOptions);
    await distributeRewards(economyCurrency, cloudSave, projectId, playerId, rewardsByService);
}

async function getCommandRewardOptions(remoteConfig, projectId, environmentId) {
    const remoteConfigResponse = await remoteConfig.assignSettingsGet(
        projectId,
        environmentId,
        'settings',
        [
            commandKeys.DefeatRedEnemy,
            commandKeys.DefeatBlueEnemy,
            commandKeys.OpenChest,
            commandKeys.AchieveBonusGoal,
            commandKeys.GameOver
        ]
    );

    if (remoteConfigResponse.data.configs == null ||
        remoteConfigResponse.data.configs.settings == null) {
        throw new CloudCodeCustomError('There was a problem getting command reward data from Remote Config.');
    }

    return getRewardsInDesiredStructure(remoteConfigResponse.data.configs.settings);
}

function getRewardsInDesiredStructure(settings) {
    // Ensures that every expected command exists and has at least an empty array for rewards.
    _.defaultsDeep(settings, {
        [commandKeys.DefeatRedEnemy] : { rewards: [] },
        [commandKeys.DefeatBlueEnemy] : { rewards: [] },
        [commandKeys.OpenChest] : { rewards: [] },
        [commandKeys.AchieveBonusGoal] : { rewards: [] },
        [commandKeys.GameOver] : { rewards: [] }
    })

    // Reduces object structure to { commandKey: [] } from { commandKey: { rewards: [] }}
    return _.mapValues(settings, command => {
        return command.rewards;
    });
}

function groupCommandRewardsByService(commands, commandRewards) {
    const rewardsGroupedByService = {
        'currency': [],
        'cloudSave': []
    };

    commands.forEach(command => {
        commandRewards[command].forEach(reward => {
            const existingReward = rewardsGroupedByService[reward.service].find(x => x.id === reward.id);

            if (existingReward === undefined) {
                rewardsGroupedByService[reward.service].push({ id: reward.id, amount: reward.amount });
            } else {
                existingReward.amount += reward.amount;
            }
        });
    });

    return rewardsGroupedByService;
}

async function distributeRewards(economyCurrency, cloudSave, projectId, playerId, rewardsByService) {
    await Promise.all([
        distributeCurrencyRewards(economyCurrency, projectId, playerId, rewardsByService['currency']),
        distributeCloudSaveRewards(cloudSave, projectId, playerId, rewardsByService['cloudSave'])
    ]);
}

async function distributeCurrencyRewards(economyCurrency, projectId, playerId, currencyRewards) {
    let currencyRewardTasks = [];
    for (let i = 0; i < currencyRewards.length; i++) {
        const currencyId = currencyRewards[i].id;
        const amount = currencyRewards[i].amount;
        const currencyModifyBalanceRequest = { currencyId, amount };
        const requestParameters = { projectId, playerId, currencyId, currencyModifyBalanceRequest };
        currencyRewardTasks.push(economyCurrency.incrementPlayerCurrencyBalance(requestParameters));
    }

    await Promise.all(currencyRewardTasks);
}

async function distributeCloudSaveRewards(cloudSave, projectId, playerId, cloudSaveRewards) {
    const currentCloudSaveData = await getCurrentCloudSaveData(cloudSave, projectId, playerId, cloudSaveRewards);
    const updatedCloudSaveData = updateCloudSaveData(currentCloudSaveData, cloudSaveRewards);
    await saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedCloudSaveData);
}

async function getCurrentCloudSaveData(cloudSave, projectId, playerId, cloudSaveRewards) {
    const cloudSaveKeys = cloudSaveRewards.map(function(reward) {
        return reward.id;
    });

    const getItemsResponse = await cloudSave.getItems(
        projectId,
        playerId,
        cloudSaveKeys
    );

    return cloudSaveResponseToObject(getItemsResponse);
}

function cloudSaveResponseToObject(getItemsResponse) {
    let returnObject = {};

    getItemsResponse.data.results.forEach(item => {
        returnObject[item.key] = item.value;
    });

    return returnObject;
}

function updateCloudSaveData(currentCloudSaveData, cloudSaveRewards) {
    let updatedCloudSaveData = [];

    cloudSaveRewards.forEach(reward => {
        let totalAmount = 0;

        if (currentCloudSaveData[reward.id] !== undefined)
        {
            totalAmount += currentCloudSaveData[reward.id];
        }

        totalAmount += reward.amount;

        updatedCloudSaveData.push({ key: reward.id, value: totalAmount });
    });

    return updatedCloudSaveData;
}

async function saveUpdatedCloudSaveData(cloudSave, projectId, playerId, updatedCloudSaveData) {
    await cloudSave.setItemBatch(
        projectId,
        playerId,
        { data: updatedCloudSaveData }
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

class InvalidArgumentError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "InvalidArgumentError";
        this.status = 2;
    }
}

class InvalidGameplayError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "InvalidGameplayError";
        this.status = 3;
    }
}
