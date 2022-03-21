using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace RewardedAds
    {
        public class EconomyManager : MonoBehaviour
        {
            public static EconomyManager instance { get; private set; }

            public CurrencyHudView currencyHudView;

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

                UpdateCurrencyBalances(balances.Balances);
            }

            void UpdateCurrencyBalances(List<PlayerBalance> balances)
            {
                foreach (PlayerBalance balance in balances)
                {
                    SetCurrencyBalance(balance.CurrencyId, balance.Balance);
                }
            }

            public void SetCurrencyBalance(string currencyId, long balance)
            {
                currencyHudView.SetBalance(currencyId, balance);
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
