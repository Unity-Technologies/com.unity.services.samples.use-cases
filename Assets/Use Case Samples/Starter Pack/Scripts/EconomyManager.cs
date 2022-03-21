using System.Threading.Tasks;
using Unity.Services.Economy;
using Unity.Services.Economy.Model;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace StarterPack
    {
        public class EconomyManager : MonoBehaviour
        {
            public CurrencyHudView currencyHudView;
            public InventoryHudView inventoryHudView;

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

            public void SetCurrencyBalance(string currencyId, long balance)
            {
                currencyHudView.SetBalance(currencyId, balance);
            }

            public async Task RefreshInventory()
            {
                var options = new PlayerInventory.GetInventoryOptions { ItemsPerFetch = 100 };
                var getInventoryTask = Economy.PlayerInventory.GetInventoryAsync(options);
                var inventory = await Utils.ProcessEconomyTaskWithRetry(getInventoryTask);

                if (this == null) return;

                inventoryHudView.Refresh(inventory);
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
