using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Economy;
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

            public async Task InitializeCurrencySprites()
            {
                var currencies = await Economy.Configuration.GetCurrenciesAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                // Setup 3 lists to facilitate async operation. Since we require a list of tasks to perform the
                // await Task.WhenAll call, we can simply setup the other 2 lists to track corresponding ids and
                // sprite handles, which are required to process results once all tasks are complete.
                var ids = new List<string>();
                var handles = new List<AsyncOperationHandle<Sprite>>();
                var tasks = new List<Task<Sprite>>();

                // Fill above 3 lists so we can async wait for all Addressables to be loaded and interpret results.
                foreach (var currency in currencies)
                {
                    var spriteAddress = currency.CustomData["spriteAddress"] as string;
                    var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);

                    ids.Add(currency.Id);
                    handles.Add(handle);
                    tasks.Add(handle.Task);
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
                var options = new PlayerBalances.GetBalancesOptions { ItemsPerFetch = 100 };
                var getBalancesTask = Economy.PlayerBalances.GetBalancesAsync(options);
                var balances = await Utils.ProcessEconomyTaskWithRetry(getBalancesTask);
                if (this == null) return;

                currencyHudView.SetBalances(balances);
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
