using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace IdleClickerGame
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;

            public static EconomyManager instance { get; private set; }

            Dictionary<string, long> m_CurrencyBalance = new Dictionary<string, long>();


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
                m_CurrencyBalance.Clear();

                foreach (PlayerBalance balance in balances)
                {
                    SetCurrencyBalance(balance.CurrencyId, balance.Balance);
                }
            }

            public void IncrementCurrencyBalance(string currencyId, long increment)
            {
                long balance = 0;
                m_CurrencyBalance.TryGetValue(currencyId, out balance);

                balance += increment;

                SetCurrencyBalance(currencyId, balance);
            }

            public void SetCurrencyBalance(string currencyId, long balance)
            {
                m_CurrencyBalance[currencyId] = balance;

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
