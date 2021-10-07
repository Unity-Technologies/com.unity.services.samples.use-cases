using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace SeasonalEvents
{
    public class CloudCodeManager : MonoBehaviour
    {
        public static CloudCodeManager instance { get; private set; }
        public static event Action<string, long> CurrencyBalanceUpdated;

        struct GrantEventRewardRequest
        {
        }

        struct GrantEventRewardResult
        {
            public RewardDetail[] grantedRewards;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
            }
        }

        public async Task CallGrantEventRewardEndpoint()
        {
            try
            {
                // The CallEndpointAsync method requires two objects to be passed in: the name of the script being
                // called, and a struct for any arguments that need to be passed to the script. In this sample,
                // we didn't need to pass any additional arguments, so we're passing an empty struct. Alternatively,
                // you could pass an empty string.
                var updatedRewardBalances = await CloudCode.CallEndpointAsync<GrantEventRewardResult>("GrantEventReward",
                    new GrantEventRewardRequest());

                // The GrantEventReward script returns the total balance for each of the currencies that are distributed
                // as part of the event reward. These total balances ultimately come from the Economy API which returns
                // them to the Cloud Code script as part of the reward distribution, so we will update our currency HUD
                // displays directly with these new balances.
                foreach (var reward in updatedRewardBalances.grantedRewards)
                {
                    CurrencyBalanceUpdated?.Invoke(reward.id, reward.quantity);
                }
            }
            catch (Exception e)
            {
                Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                Debug.LogException(e);
            }
        }
    }
}
