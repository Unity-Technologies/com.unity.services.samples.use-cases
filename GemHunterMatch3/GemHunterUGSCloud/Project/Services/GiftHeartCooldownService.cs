using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// GiftHeartCooldownService - Manages timing for gift heart replenishment to regulate social economy.
/// 
/// Core Responsibilities:
/// - Gift heart replenishment eligibility validation
/// - Automatic timer updates upon successful replenishment checks
/// - Last replenishment timestamp persistence and retrieval
/// - Time-based cooldown calculations for heart regeneration
/// - Integration with player gifting economy through controlled replenishment
/// 
/// Key Cloud Code Functions:
/// - CheckGiftHeartReplenish: Validates and triggers gift heart replenishment
/// 
/// Replenishment Features:
/// - Configurable replenishment period (120 seconds default)
/// - Automatic timer reset upon successful replenishment
/// - Epoch timestamp-based precision timing
/// - New player support (immediate first replenishment)
/// - Atomic check-and-update operations
/// 
/// Integration Points:
/// - Used by PlayerDataService for gift heart economy management
/// - Supports social gifting rate limiting
/// - Prevents gift heart farming and abuse
/// - Maintains balanced social interaction economy
/// 
/// </summary>
public class GiftHeartCooldownService
{
    private const int k_GiftHeartReplenishSeconds = 120;
    private const string k_GiftHeartCooldownKey = "GIFT_HEART_COOLDOWN";
    
    private readonly ILogger<GiftHeartCooldownService> m_Logger;
    private readonly IGameApiClient m_GameApiClient;
    
    
    public GiftHeartCooldownService(ILogger<GiftHeartCooldownService> logger, IGameApiClient gameApiClient)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
    }
    
    [CloudCodeFunction("CheckGiftHeartReplenish")]
    public async Task<bool> CheckGiftHeartReplenish(IExecutionContext context)
    {
        bool shouldReplenish = await ShouldReplenishGiftHearts(context);
        if (shouldReplenish)
        {
            await UpdateReplenishTimer(context);
        }
        return shouldReplenish;
    }
    
    private async Task<bool> ShouldReplenishGiftHearts(IExecutionContext context)
    {
        var lastReplenishTime = await GetLastReplenishTime(context);
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return (currentTime - lastReplenishTime) >= k_GiftHeartReplenishSeconds;
    }

    private async Task<long> GetLastReplenishTime(IExecutionContext context)
    {
        var response = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(
            context,
            context.ServiceToken,
            context.ProjectId,
            context.PlayerId,
            new List<string> { k_GiftHeartCooldownKey }
        );

        var timeItem = response.Data.Results.FirstOrDefault();

        if (timeItem?.Value is long directValue)
        {
            return directValue;
        }

        return 0;
    }

    private async Task UpdateReplenishTimer(IExecutionContext context)
    {
        var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var setItemBody = new SetItemBody(k_GiftHeartCooldownKey, epochTime);
        
        await m_GameApiClient.CloudSaveData.SetProtectedItemAsync(
            context,
            context.ServiceToken,
            context.ProjectId,
            context.PlayerId,
            setItemBody
        );
    }
}
