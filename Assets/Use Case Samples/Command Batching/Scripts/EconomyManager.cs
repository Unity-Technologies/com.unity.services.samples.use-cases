using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace CommandBatching
    {
        public class EconomyManager : MonoBehaviour
        {
            public static EconomyManager instance { get; private set; }

            public CurrencyHudView currencyHudView;

            Dictionary<string, long> m_CurrencyBalances = new Dictionary<string, long>();


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
                m_CurrencyBalances.Clear();

                foreach (PlayerBalance balance in balances)
                {
                    SetCurrencyBalance(balance.CurrencyId, balance.Balance);
                }
            }

            public void IncrementCurrencyBalance(string currencyId, long increment)
            {
                long balance = 0;
                m_CurrencyBalances.TryGetValue(currencyId, out balance);

                balance += increment;

                SetCurrencyBalance(currencyId, balance);
            }

            public void SetCurrencyBalance(string currencyId, long balance)
            {
                m_CurrencyBalances[currencyId] = balance;

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
