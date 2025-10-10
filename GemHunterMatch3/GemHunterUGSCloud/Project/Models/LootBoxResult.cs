using System.Collections.Generic;
namespace GemHunterUGSCloud.Models;

public class LootBoxResult
{
    public Dictionary<string, int> Currencies { get; set; } = new();
    public Dictionary<string, int> InventoryItems { get; set; } = new();
    
    public void AddCurrency(string currencyId, int amount)
    {
        if (!string.IsNullOrEmpty(currencyId))
        {
            Currencies[currencyId] = amount;
        }
    }

    public void AddInventoryItem(string itemId, int amount)
    {
        if (!string.IsNullOrEmpty(itemId))
        {
            InventoryItems[itemId] = amount;
        }
    }
}

public class LootBoxCooldownResult
{
    public bool CanGrantFlag { get; set; }
    public long CurrentCooldown { get; set; }
    public int DefaultCooldown { get; set; }
}