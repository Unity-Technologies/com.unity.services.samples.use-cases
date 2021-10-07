using System;
using Unity.Services.Economy;
using UnityEngine;

namespace SeasonalEvents
{
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager instance { get; private set; }
        public static event Action<string, long> CurrencyBalanceUpdated;

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

        public async void GetUpdatedBalances()
        {
            var balancesOptions = new PlayerBalances.GetBalancesOptions {ItemsPerFetch = 100};
            var getBalancesResult = await Economy.PlayerBalances.GetBalancesAsync(balancesOptions);
            foreach (var balance in getBalancesResult.Balances)
            {
                CurrencyBalanceUpdated?.Invoke(balance.CurrencyId, balance.Balance);
            }
        }
    }
}
