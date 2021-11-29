using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace ABTestLevelDifficulty
    {
        public class CloudCodeManager : MonoBehaviour
        {
            public static CloudCodeManager instance { get; private set; }
            public static event Action<string, long> LeveledUp;
            public static event Action<int> XPIncreased;

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

            public async Task CallGainXPAndLevelIfReadyEndpoint()
            {
                try
                {
                    var gainXPAndLevelResults = await CloudCode.CallEndpointAsync<GainXPAndLevelResult>("GainXPAndLevelIfReady", "");

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (gainXPAndLevelResults.playerXPUpdateAmount > 0)
                    {
                        XPIncreased?.Invoke(gainXPAndLevelResults.playerXPUpdateAmount);
                    }

                    if (gainXPAndLevelResults.didLevelUp)
                    {
                        CompleteLevelUpUpdates(gainXPAndLevelResults);
                    }

                    CloudSaveManager.instance.UpdateCachedPlayerXP(gainXPAndLevelResults.playerXP);
                }
                catch (Exception e)
                {
                    if (string.Equals(e.Message, "HTTP/1.1 422 Unprocessable Entity"))
                    {
                        Debug.Log("Cloud Code may have hit Cloud Save's rate limiting. Try again in a minute.");
                    }
                    else
                    {
                        Debug.Log("Problem calling cloud code endpoint: " + e.Message);
                    }
                    Debug.LogException(e);
                }
            }

            void CompleteLevelUpUpdates(GainXPAndLevelResult levelUpResults)
            {
                var rewardCurrencyId = levelUpResults.levelUpRewards.currencyId;
                LeveledUp?.Invoke(rewardCurrencyId, levelUpResults.levelUpRewards.rewardAmount);
                EconomyManager.instance.SetCurrencyBalance(rewardCurrencyId,
                    levelUpResults.levelUpRewards.balance);

                CloudSaveManager.instance.UpdateCachedPlayerLevel(levelUpResults.playerLevel);
            }

            void OnDestroy()
            {
                instance = null;
            }

            public struct LevelUpRewards
            {
                public string currencyId;
                public int rewardAmount;
                public int balance;
            }

            public struct GainXPAndLevelResult
            {
                public int playerLevel;
                public int playerXP;
                public int playerXPUpdateAmount;
                public bool didLevelUp;
                public LevelUpRewards levelUpRewards;
            }
        }
    }
}
