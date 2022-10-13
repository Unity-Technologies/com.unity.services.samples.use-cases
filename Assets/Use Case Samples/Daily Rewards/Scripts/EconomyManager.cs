using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace UnityGamingServicesUseCases
{
    namespace DailyRewards
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;

            public static EconomyManager instance { get; private set; }

            private List<CurrencyDefinition> currencies;
            Dictionary<string, Sprite> m_CurrencyIdToSprite = new Dictionary<string, Sprite>();


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
                // Calling GetCurrenciesAsync (or GetInventoryItemsAsync), in addition to returning the appropriate
                // Economy configurations, will update the cached configuration list, including any new Currency, 
                // Inventory Item, or Purchases that have been published since the last time the player's configuration
                // was cached.
                //
                // This is important to do before hitting the Economy or Remote Config services for any other calls as
                // both use the cached data list.
                currencies = await EconomyService.Instance.Configuration.GetCurrenciesAsync();
            }

            public async Task FetchCurrencySprites()
            {
                if (currencies is null || currencies.Count <= 0)
                {
                    Debug.Log("Can't fetch currency sprites, ensure RefreshEconomyConfiguration() has been called.");
                    return;
                }

                // Setup 3 lists to facilitate async operation. Since we require a list of tasks to perform the
                // await Task.WhenAll call, we can simply setup the other 2 lists to track corresponding ids and
                // sprite handles, which are required to process results once all tasks are complete.
                var ids = new List<string>();
                var handles = new List<AsyncOperationHandle<Sprite>>();
                var tasks = new List<Task<Sprite>>();

                // Fill above 3 lists so we can async wait for all Addressables to be loaded and interpret results.
                foreach (var currency in currencies)
                {
                    var customData = currency.CustomData;
                    if (customData != null
                        && customData.TryGetValue("spriteAddress", out var spriteAddressObject)
                        && spriteAddressObject is string spriteAddress)
                    {
                        var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);

                        ids.Add(currency.Id);
                        handles.Add(handle);
                        tasks.Add(handle.Task);
                    }
                }

                // Wait for all Addressables to be loaded.
                await Task.WhenAll(tasks);
                if (this == null) return;

                // Iterate all Addressables and save off the Sprites into our local dictinary.
                for (var i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    var handle = handles[i];

                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        m_CurrencyIdToSprite[id] = handle.Result;
                    }
                    else
                    {
                        Debug.LogError($"A sprite could not be found for the address {id}." +
                            $" Addressables exception: {handle.OperationException}");
                    }
                }
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

                currencyHudView.SetBalances(balanceResult);
            }

            static Task<GetBalancesResult> GetEconomyBalances()
            {
                var options = new GetBalancesOptions { ItemsPerFetch = 100 };
                return EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
            }

            public Sprite GetSpriteForCurrencyId(string currencyId)
            {
                if (string.IsNullOrEmpty(currencyId))
                {
                    return null;
                }
                return m_CurrencyIdToSprite[currencyId];
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
