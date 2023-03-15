using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace Unity.Services.Samples.RewardedAds
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

        public async Task RefreshEconomyConfiguration()
        {
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
            if (this == null) return;

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

            foreach (var balance in balances)
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
