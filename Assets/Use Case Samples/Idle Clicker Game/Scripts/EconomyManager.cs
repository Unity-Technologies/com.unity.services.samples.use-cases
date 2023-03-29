using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class EconomyManager : MonoBehaviour
    {
        public CurrencyHudView currencyHudView;

        public static EconomyManager instance { get; private set; }

        readonly Dictionary<string, long> m_CurrencyBalance = new Dictionary<string, long>();

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

        public async Task RefreshEconomyConfiguration()
        {
            // Calling SyncConfigurationAsync(), will update the cached configuration list (the lists of Currency,
            // Inventory Item, and Purchase definitions) with any definitions that have been published or changed by
            // Economy or overriden by Game Overrides since the last time the player's configuration was cached. It also
            // ensures that other services like Cloud Code are working with the same configuration that has been cached.
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
        }

        public async Task RefreshCurrencyBalances()
        {
            GetBalancesResult balanceResult = null;

            try
            {
                balanceResult = await GetEconomyBalances();
            }
            catch (EconomyRateLimitedException e)
            {
                balanceResult = await Utils.RetryEconomyFunction(GetEconomyBalances, e.RetryAfter);
            }
            catch (Exception e)
            {
                Debug.Log("Problem getting Economy currency balances:");
                Debug.LogException(e);
            }

            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (this == null)
                return;

            UpdateCurrencyBalances(balanceResult?.Balances);
        }

        static Task<GetBalancesResult> GetEconomyBalances()
        {
            var options = new GetBalancesOptions { ItemsPerFetch = 100 };
            return EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
        }

        void UpdateCurrencyBalances(List<PlayerBalance> balances)
        {
            if (balances == null)
            {
                return;
            }

            m_CurrencyBalance.Clear();

            foreach (PlayerBalance balance in balances)
            {
                SetCurrencyBalance(balance.CurrencyId, balance.Balance);
            }
        }

        public void SetCurrencyBalance(string currencyId, long balance)
        {
            m_CurrencyBalance[currencyId] = balance;

            currencyHudView.SetBalance(currencyId, balance);
        }

        public void IncrementCurrencyBalance(string currencyId, long increment)
        {
            long balance = 0;
            m_CurrencyBalance.TryGetValue(currencyId, out balance);

            balance += increment;

            SetCurrencyBalance(currencyId, balance);
        }

        public long GetCurrencyBalance(string currencyId)
        {
            return m_CurrencyBalance[currencyId];
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
