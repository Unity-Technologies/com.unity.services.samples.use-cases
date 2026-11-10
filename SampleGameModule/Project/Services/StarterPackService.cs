using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SampleGameModule.Infrastructure;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Core;

namespace SampleGameModule.Services;

/// <summary>
/// Starter Pack sample: a one-time virtual purchase, gated by the STARTER_PACK_STATUS Cloud Save key.
/// Used by: Starter Pack, CloudCodeManager.CallPurchaseStarterPackEndpoint.
/// </summary>
public class StarterPackService
{
    const string k_StatusKey = "STARTER_PACK_STATUS";
    const string k_PurchaseId = "STARTER_PACK_PURCHASE";

    readonly EconomyModuleService m_Economy;
    readonly PlayerDataStore m_Store;
    readonly ILogger<StarterPackService> m_Logger;

    public StarterPackService(EconomyModuleService economy, PlayerDataStore store, ILogger<StarterPackService> logger)
    {
        m_Economy = economy;
        m_Store = store;
        m_Logger = logger;
    }

    public class StarterPackStatus
    {
        [JsonProperty("claimed")] public bool Claimed { get; set; }
    }

    /// <summary>
    /// Used by: Starter Pack sample, CloudCodeManager.CallPurchaseStarterPackEndpoint.
    /// Buys the starter pack once per player and returns the updated wallet and inventory.
    /// </summary>
    [CloudCodeFunction("StarterPack_PurchaseStarterPack")]
    public async Task<PlayerEconomySnapshot> PurchaseStarterPack(IExecutionContext context)
    {
        // Prevent the purchase if Cloud Save confirms the player already claimed a Starter Pack.
        var status = await m_Store.GetAsync(context, k_StatusKey);
        if (status.As<StarterPackStatus>()?.Claimed == true)
        {
            m_Logger.LogError("The Starter Pack has already been claimed by this player.");
            throw new ModuleError(0, "Error", "The Starter Pack has already been claimed by this player.");
        }

        // Make the purchase. An error is thrown if the player can't afford it.
        var snapshot = await m_Economy.MakeVirtualPurchasesAsync(context, new[] { k_PurchaseId });

        // Let Cloud Save know that the Starter Pack has been claimed by this player.
        await m_Store.SetAsync(context, k_StatusKey, new StarterPackStatus { Claimed = true });

        return snapshot;
    }

    /// <summary>
    /// Used by: Starter Pack sample, StarterPackSceneManager.OnResetPlayerDataButtonPressed.
    /// Forgets the purchase so the starter pack can be bought again. A testing aid, like the Economy_* grants:
    /// remove it from a real game.
    /// </summary>
    [CloudCodeFunction("StarterPack_ResetForDebug")]
    public Task ResetForDebug(IExecutionContext context) => m_Store.DeleteAsync(context, k_StatusKey);
}
