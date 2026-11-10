using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Unity.Services.Samples.Economy
{
    // Plain-C# replacements for the Unity.Services.Economy.Model types the samples
    // consume. Field names match the Economy SDK so per-sample code only swaps its
    // using directive. Catalog data comes from Remote Config (see EconomyCatalog);
    // player state comes from Cloud Save via the SampleGameModule module
    // (see PlayerEconomy).

    public class CurrencyDefinition
    {
        public string Id;
        public string Name;
        public long Initial;
        public long Max;
        public CustomData CustomDataDeserializable;
    }

    public class InventoryItemDefinition
    {
        public string Id;
        public string Name;
        public CustomData CustomDataDeserializable;
    }

    public class VirtualPurchaseDefinition
    {
        public string Id;
        public string Name;
        public List<PurchaseItemQuantity> Costs = new List<PurchaseItemQuantity>();
        public List<PurchaseItemQuantity> Rewards = new List<PurchaseItemQuantity>();
    }

    public class PurchaseItemQuantity
    {
        public ItemReference Item;
        public int Amount;
    }

    public class ItemReference
    {
        readonly ConfigurationItem m_Item;

        public ItemReference(string id)
        {
            m_Item = new ConfigurationItem { Id = id };
        }

        public ConfigurationItem GetReferencedConfigurationItem()
        {
            return m_Item;
        }
    }

    public class ConfigurationItem
    {
        public string Id;
    }

    public class PlayerBalance
    {
        public string CurrencyId;
        public long Balance;
    }

    public class GetBalancesResult
    {
        public List<PlayerBalance> Balances = new List<PlayerBalance>();
    }

    public class PlayersInventoryItem
    {
        public string InventoryItemId;
        public string PlayersInventoryItemId;
    }

    public class GetInventoryResult
    {
        public List<PlayersInventoryItem> PlayersInventoryItems = new List<PlayersInventoryItem>();
    }

    public class CurrencyExchangeItem
    {
        public string Id;
        public long Amount;
    }

    public class InventoryExchangeItem
    {
        public string Id;
        public int Amount;
    }

    public class Rewards
    {
        public List<CurrencyExchangeItem> Currency = new List<CurrencyExchangeItem>();
        public List<InventoryExchangeItem> Inventory = new List<InventoryExchangeItem>();
    }

    public class Costs
    {
        public List<CurrencyExchangeItem> Currency = new List<CurrencyExchangeItem>();
        public List<InventoryExchangeItem> Inventory = new List<InventoryExchangeItem>();
    }

    public class MakeVirtualPurchaseResult
    {
        public Costs Costs = new Costs();
        public Rewards Rewards = new Rewards();
    }

    // The samples pass ItemsPerFetch but Cloud Save reads don't paginate the same
    // way, so these are accepted and ignored.
    public class GetBalancesOptions
    {
        public int ItemsPerFetch = 100;
    }

    public class GetInventoryOptions
    {
        public int ItemsPerFetch = 100;
    }

    public class EconomyException : Exception
    {
        // Matches Economy's "purchase costs not met" error code, referenced by
        // Virtual Shop/Scripts/EconomyManager.cs and Starter Pack/Scripts/CloudCodeManager.cs.
        public const int PurchaseCostsNotMetErrorCode = 10504;

        public int ErrorCode;

        public EconomyException(int errorCode, string message, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class EconomyRateLimitedException : EconomyException
    {
        public int RetryAfter;

        public EconomyRateLimitedException(int retryAfter, string message, Exception innerException = null)
            : base(0, message, innerException)
        {
            RetryAfter = retryAfter;
        }
    }

    // Shim so `CustomDataDeserializable.GetAs<Dictionary<string, string>>()` calls
    // (Virtual Shop/Scripts/AddressablesManager.cs) keep compiling.
    public class CustomData
    {
        readonly JObject m_Data;

        public CustomData(JObject data)
        {
            m_Data = data;
        }

        public T GetAs<T>()
        {
            return m_Data == null ? default : m_Data.ToObject<T>();
        }
    }
}
