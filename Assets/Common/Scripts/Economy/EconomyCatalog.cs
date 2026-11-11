using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.RemoteConfig;

namespace Unity.Services.Samples.Economy
{
    // Replacement for EconomyService.Instance.Configuration: fetches the economy
    // catalog from the ECONOMY_CURRENCIES / ECONOMY_ITEMS / ECONOMY_VIRTUAL_PURCHASES
    // Remote Config keys and caches it in memory. Samples call FetchAsync() where
    // they previously called SyncConfigurationAsync() (once per scene entry).
    public class EconomyCatalog
    {
        const string k_CurrenciesKey = "ECONOMY_CURRENCIES";
        const string k_ItemsKey = "ECONOMY_ITEMS";
        const string k_VirtualPurchasesKey = "ECONOMY_VIRTUAL_PURCHASES";

        public static EconomyCatalog Instance { get; } = new EconomyCatalog();

        List<CurrencyDefinition> m_Currencies = new List<CurrencyDefinition>();
        List<InventoryItemDefinition> m_InventoryItems = new List<InventoryItemDefinition>();
        List<VirtualPurchaseDefinition> m_VirtualPurchases = new List<VirtualPurchaseDefinition>();

        EconomyCatalog() { }

        public async Task FetchAsync()
        {
            await RemoteConfigService.Instance.FetchConfigsAsync(new UserAttributes(), new AppAttributes());

            m_Currencies = ParseDefinitions(k_CurrenciesKey, ParseCurrency);
            m_InventoryItems = ParseDefinitions(k_ItemsKey, ParseInventoryItem);
            m_VirtualPurchases = ParseDefinitions(k_VirtualPurchasesKey, ParseVirtualPurchase);
        }

        public List<CurrencyDefinition> GetCurrencies()
        {
            return m_Currencies;
        }

        public List<InventoryItemDefinition> GetInventoryItems()
        {
            return m_InventoryItems;
        }

        public List<VirtualPurchaseDefinition> GetVirtualPurchases()
        {
            return m_VirtualPurchases;
        }

        public bool IsCurrency(string id)
        {
            return m_Currencies.Exists(currency => currency.Id == id);
        }

        public VirtualPurchaseDefinition GetVirtualPurchase(string purchaseId)
        {
            return m_VirtualPurchases.Find(purchase => purchase.Id == purchaseId);
        }

        static List<T> ParseDefinitions<T>(string configKey, Func<string, JObject, T> parseDefinition)
        {
            var definitions = new List<T>();

            var json = RemoteConfigService.Instance.appConfig.GetJson(configKey);
            if (string.IsNullOrEmpty(json))
            {
                return definitions;
            }

            foreach (var property in JObject.Parse(json).Properties())
            {
                definitions.Add(parseDefinition(property.Name, (JObject)property.Value));
            }

            return definitions;
        }

        static CurrencyDefinition ParseCurrency(string id, JObject json)
        {
            return new CurrencyDefinition
            {
                Id = id,
                Name = json.Value<string>("name"),
                Initial = json.Value<long?>("initial") ?? 0,
                Max = json.Value<long?>("max") ?? 0,
                CustomDataDeserializable = new CustomData(json.Value<JObject>("customData")),
            };
        }

        static InventoryItemDefinition ParseInventoryItem(string id, JObject json)
        {
            return new InventoryItemDefinition
            {
                Id = id,
                Name = json.Value<string>("name"),
                CustomDataDeserializable = new CustomData(json.Value<JObject>("customData")),
            };
        }

        static VirtualPurchaseDefinition ParseVirtualPurchase(string id, JObject json)
        {
            return new VirtualPurchaseDefinition
            {
                Id = id,
                Name = json.Value<string>("name"),
                Costs = ParseItemQuantities(json.Value<JArray>("costs")),
                Rewards = ParseItemQuantities(json.Value<JArray>("rewards")),
            };
        }

        static List<PurchaseItemQuantity> ParseItemQuantities(JArray json)
        {
            var itemQuantities = new List<PurchaseItemQuantity>();
            if (json == null)
            {
                return itemQuantities;
            }

            foreach (var entry in json)
            {
                itemQuantities.Add(new PurchaseItemQuantity
                {
                    Item = new ItemReference(entry.Value<string>("id")),
                    Amount = entry.Value<int>("amount"),
                });
            }

            return itemQuantities;
        }

        struct UserAttributes { }

        struct AppAttributes { }
    }
}
