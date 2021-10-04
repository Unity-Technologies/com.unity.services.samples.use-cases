// This file is an inactive copy of what is published on the Cloud Code server for this example project.
// Changes made to this file will not have any effect locally. Changes to Cloud Code scripts are normally done
// directly in the Unity Dashboard website.

const _ = require("lodash-4.17");
const { CurrenciesApi } = require("@unity-services/economy-2.0");
const { SettingsApi } = require("@unity-services/remote-config-1.0");


module.exports = async ({ context }) => {
    const { projectId, playerId, accessToken} = context;
    const economy = new CurrenciesApi({ accessToken });
    const remoteConfig = new SettingsApi();

    const timestampMinutes = getTimestampMinutes();
    const rewards = await getRewardsFromRemoteConfig(remoteConfig, projectId, playerId, timestampMinutes);
    const grantedRewards = await grantRewards(economy, projectId, playerId, rewards);

    return { grantedRewards };
};

function getTimestampMinutes()
{
    const timestamp = _.now();
    let date = new Date(timestamp);
    return ("0" + date.getMinutes()).slice(-2);
}

async function getRewardsFromRemoteConfig(remoteConfig, projectId, playerId, timestampMinutes)
{
    let eventRewards = [];

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

    const rewardResults = result.data.configs.settings["CHALLENGE_REWARD"];

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
