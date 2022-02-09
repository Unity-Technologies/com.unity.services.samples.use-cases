using System.Threading.Tasks;
using Unity.Services.Economy;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace LootBoxes
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;

            public static EconomyManager instance { get; private set; }


            void Awake()
            {
                if (instance != null && instance != this)
                {
                    Destroy(this);
                }
                else
                {
                    instance = this;
                }
            }

            public async Task RefreshCurrencyBalances()
            {
                var options = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                var getBalancesTask = Economy.PlayerBalances.GetBalancesAsync(options);
                var balances = await Utils.ProcessEconomyTaskWithRetry(getBalancesTask);

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                currencyHudView.SetBalances(balances);
            }

            void OnDestroy()
            {
                if (instance == this)
                {
                    instance = null;
                }
            }
        }
    }
}
