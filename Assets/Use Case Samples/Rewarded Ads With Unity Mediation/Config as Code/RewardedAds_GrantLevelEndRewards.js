const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.3");
const { DataApi } = require("@unity-services/cloud-save-1.2");

const badRequestError = 400;
const tooManyRequestsError = 429;

// In this sample, for simplicity, the rewards are hardcoded.
// Alternatively, you could use Remote Config to dynamically define these,
// this would allow a single source of truth for the values used in both cloud and client code.
// See CommandBatch or Seasonal Events samples for examples of how this could be done.
const rewardCurrencyId = "GEM";
const baseRewardAmount = 25;
const frequencyOfMiniGameOccurrence = 3;
const cloudSaveKeyLastLevelEndBaseRewardTimestamp = "REWARDED_ADS_LAST_LEVEL_END_BASE_REWARD_TIMESTAMP";
const cloudSaveKeyLastLevelEndBoosterRewardTimestamp = "REWARDED_ADS_LAST_LEVEL_END_BOOSTER_REWARD_TIMESTAMP";
const cloudSaveKeyLevelEndCount = "REWARDED_ADS_LEVEL_END_COUNT";
const cloudSaveKeyLevelBoosterRewardsDistributed = "REWARDED_ADS_LEVEL_BOOSTER_REWARDS_DISTRIBUTED"

module.exports = async ({ params, context, logger }) => {
    try {
        const { projectId, playerId, environmentId, accessToken} = context;
        const economyCurrencyApi = new CurrenciesApi({ accessToken });
        const cloudSaveApi = new DataApi({ accessToken });
        const servicesData = { projectId, playerId, environmentId, cloudSaveApi, economyCurrencyApi, logger };

        const { isDistributingBoosterRewards, multiplier } = processMultiplierParam(params.multiplier);
        const currentTimestamp = _.now();

        const { levelEndCount, lastCalledTime, boosterRewardsDistributed } = await getCurrentCloudSaveData(servicesData, isDistributingBoosterRewards);
        validateScriptUsage(multiplier, currentTimestamp, levelEndCount, lastCalledTime, isDistributingBoosterRewards, boosterRewardsDistributed);
        const rewardCurrencyBalance = await processRewards(servicesData, multiplier, isDistributingBoosterRewards);
        await updateCloudSaveData(servicesData, currentTimestamp, levelEndCount, isDistributingBoosterRewards);

        return {
            rewardCurrencyId,
            rewardCurrencyBalance
        };
    } catch (error) {
        transformAndThrowCaughtException(error);
    }
};

function processMultiplierParam(multiplier) {
    if (multiplier === undefined) {
        return { isDistributingBoosterRewards: false, multiplier: 1 };
    }

    return { isDistributingBoosterRewards: true, multiplier };
}

async function getCurrentCloudSaveData(servicesData, isDistributingBoosterRewards) {
    let cloudSaveKeys = [cloudSaveKeyLevelEndCount];

    if (isDistributingBoosterRewards) {
        cloudSaveKeys.push(cloudSaveKeyLastLevelEndBoosterRewardTimestamp, cloudSaveKeyLevelBoosterRewardsDistributed);
    } else {
        cloudSaveKeys.push(cloudSaveKeyLastLevelEndBaseRewardTimestamp);
    }

    const getItemsResponse = await servicesData.cloudSaveApi.getItems(
        servicesData.projectId,
        servicesData.playerId,
        cloudSaveKeys
    );

    return cloudSaveResponseToObject(getItemsResponse);
}

function cloudSaveResponseToObject(getItemsResponse) {
    let returnObject = {
        levelEndCount: 0,
        lastCalledTime: 0,
        boosterRewardsDistributed: undefined
    };

    getItemsResponse.data.results.forEach(item => {
        if (item.key === cloudSaveKeyLastLevelEndBoosterRewardTimestamp ||
            item.key === cloudSaveKeyLastLevelEndBaseRewardTimestamp) {
            returnObject.lastCalledTime = item.value;
        } else if (item.key === cloudSaveKeyLevelEndCount) {
            returnObject.levelEndCount = item.value;
        } else if (item.key === cloudSaveKeyLevelBoosterRewardsDistributed) {
            returnObject.boosterRewardsDistributed = item.value;
        }
    });

    return returnObject;
}

function validateScriptUsage(multiplier, currentTimestamp, levelEndCount, lastCalledTime, isDistributingBoosterRewards, boosterRewardsDistributed) {
    if (isDistributingBoosterRewards) {
        validateAcceptableMultiplier(multiplier, levelEndCount);
        validateAcceptableTimeBetweenScriptCalls(currentTimestamp, lastCalledTime, isDistributingBoosterRewards);
        validateLevelBoosterRewardsNotAlreadyDistributed(boosterRewardsDistributed);
    } else {
        validateAcceptableTimeBetweenScriptCalls(currentTimestamp, lastCalledTime, isDistributingBoosterRewards);
    }

}

function validateAcceptableMultiplier(multiplier, levelEndCount) {
    // Most of the time the only acceptable multiplier is 2.
    // During the first (0 index) level end, and every 3rd level end after that (3, 6, 9),
    // the acceptable multipliers are 2, 3 and 5

    let acceptableMultipliers = [2];

    // We subtract 1 from levelEndCount when calculating acceptableMultipliers because
    // levelEndCount is always incremented during the script execution that distributes
    // the level's base rewards, and that execution always finishes before the one that
    // distributes reward multipliers (the only time this validation occurs).
    if ((levelEndCount - 1) % frequencyOfMiniGameOccurrence === 0) {
        acceptableMultipliers.push(3, 5);
    }

    if (!acceptableMultipliers.includes(multiplier)) {
        throw new InvalidRewardGrantError("Invalid reward multiplier supplied (" + multiplier + ") " +
            "for level count: " + (levelEndCount - 1));
    }
}

function validateAcceptableTimeBetweenScriptCalls(currentTimestamp, lastCalledTime, isDistributingBoosterRewards) {
    let necessaryTimePassMilliseconds;

    if (isDistributingBoosterRewards) {
        // Distributing booster rewards requires watching a rewarded video ad.
        // We'll assume at least 5 seconds will pass between rewarded ad views
        // for our sample.
        necessaryTimePassMilliseconds = 5000;
    } else {
        // In our sample use case, significantly less time will pass between calls
        // if the player chooses not to watch a rewarded ad.
        necessaryTimePassMilliseconds = 1000;
    }

    if (currentTimestamp < lastCalledTime + necessaryTimePassMilliseconds) {
        throw new InvalidRewardGrantError("Not enough time has passed since last level end.");
    }
}

function validateLevelBoosterRewardsNotAlreadyDistributed(boosterRewardsDistributed) {
    if (boosterRewardsDistributed) {
        throw new InvalidRewardGrantError("Booster rewards have already been distributed for this level.");
    }
}

async function processRewards(servicesData, multiplier, isDistributingBoosterRewards) {
    let amount;

    if (isDistributingBoosterRewards) {
        // We subtract baseRewardAmount in the equation because a separate call to this script
        // distributes the baseRewardAmount. When isDistributingBoosterRewards is true, that
        // indicates that the only rewards being distributed in this execution flow are the ones
        // over and above baseRewardAmount.
        amount = (baseRewardAmount * multiplier) - baseRewardAmount;
    } else {
        amount = baseRewardAmount;
    }

    const currencyModifyBalanceRequest = {
        currencyId: rewardCurrencyId,
        amount
    };

    const requestParameters = {
        projectId: servicesData.projectId,
        playerId: servicesData.playerId,
        currencyId: rewardCurrencyId,
        currencyModifyBalanceRequest
    };

    const balanceResponse = await servicesData.economyCurrencyApi.incrementPlayerCurrencyBalance(requestParameters);

    return balanceResponse.data.balance;
}

async function updateCloudSaveData(servicesData, currentTimestamp, levelEndCount, isDistributingBoosterRewards) {
    let updatedCloudSaveData = [];

    if (isDistributingBoosterRewards) {
        updatedCloudSaveData.push(
            { key: cloudSaveKeyLastLevelEndBoosterRewardTimestamp, value: currentTimestamp },
            { key: cloudSaveKeyLevelBoosterRewardsDistributed, value: true }
        );
    } else {
        updatedCloudSaveData.push(
            { key: cloudSaveKeyLastLevelEndBaseRewardTimestamp, value: currentTimestamp },
            { key: cloudSaveKeyLevelEndCount, value: levelEndCount + 1 },
            { key: cloudSaveKeyLevelBoosterRewardsDistributed, value: false }
        );
    }

    await servicesData.cloudSaveApi.setItemBatch(
        servicesData.projectId,
        servicesData.playerId,
        { data: updatedCloudSaveData }
    );
}

// Some form of this function appears in all Cloud Code scripts.
// Its purpose is to parse the errors thrown from the script into a standard exception object which can be stringified.
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
        result.detail = error.response.data.detail ? error.response.data.detail : error.response.data;
        
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

class InvalidRewardGrantError extends CloudCodeCustomError {
    constructor(message) {
        super(message);
        this.name = "InvalidRewardGrant";
        this.status = 2;
    }
}

module.exports.params = {
  "multiplier": "NUMERIC"
};
