using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Services
{
    /// <summary>
    /// LootBoxCooldownService - Manages timing restrictions for loot box claims to prevent abuse.
    /// 
    /// Core Responsibilities:
    /// - Cooldown timer validation for loot box eligibility
    /// - Last claim timestamp persistence and retrieval
    /// - Real-time cooldown calculations and remaining time tracking
    /// - New player handling (no previous claim history)
    /// - Graceful error handling for missing or corrupted cooldown data
    /// 
    /// Key Cloud Code Functions:
    /// - CheckLootBoxCooldown: Validates if player can claim and returns remaining cooldown
    /// 
    /// Cooldown Management Features:
    /// - Configurable cooldown period (20 seconds default, easily adjustable)
    /// - Epoch timestamp-based precision timing
    /// - Real-time remaining cooldown calculations
    /// - Automatic eligibility determination
    /// - First-time player support (treats missing data as immediately claimable)
    /// 
    /// Data Persistence:
    /// - Cloud Save integration for cross-session cooldown persistence
    /// - Simple key-value storage for last claim timestamps
    /// - Fault-tolerant data retrieval with safe fallbacks
    /// - Automatic handling of corrupted or missing timestamp data
    /// 
    /// Integration Points:
    /// - Used by LootBoxService for claim validation
    /// - Provides both boolean eligibility and numeric remaining time
    /// - Supports UI cooldown displays and server-side validation
    /// 
    /// Error Handling:
    /// - Graceful degradation when cooldown data is unavailable
    /// - Safe defaults for new players (immediate eligibility)
    /// </summary>
    public class LootBoxCooldownService
    {
        private const int k_DefaultCooldownSeconds = 20;
        private const string k_CooldownKey = "LOOT_BOX_COOLDOWN";

        private readonly IGameApiClient m_GameApiClient;
        private readonly ILogger<LootBoxCooldownService> m_Logger;

        public LootBoxCooldownService(ILogger<LootBoxCooldownService> logger, IGameApiClient gameApiClient)
        {
            m_Logger = logger;
            m_GameApiClient = gameApiClient;
        }
        
        [CloudCodeFunction("CheckLootBoxCooldown")]
        public async Task<LootBoxCooldownResult> CheckLootBoxCooldown(IExecutionContext context)
        {
            if (string.IsNullOrEmpty(context.PlayerId))
            {
                throw new InvalidOperationException("Player ID is required");
            }
            
            try
            {
                var lastClaimTime = await GetLastClaimTime(context);
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var timeSinceClaim = currentTime - lastClaimTime;

                var remainingCooldown = Math.Max(0, k_DefaultCooldownSeconds - timeSinceClaim);
                var canGrant = remainingCooldown <= 0;
                
                return new LootBoxCooldownResult
                {
                    CanGrantFlag = canGrant,
                    CurrentCooldown = remainingCooldown,
                    DefaultCooldown = k_DefaultCooldownSeconds
                };
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Error checking cooldown for player {PlayerId}", context.PlayerId);
                throw;
            }
        }
        
        public async Task UpdateCooldownTimer(IExecutionContext context)
        {
            var epochTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var setItemBody = new SetItemBody(k_CooldownKey, epochTime);

            try
            {
                await m_GameApiClient.CloudSaveData.SetProtectedItemAsync(
                    context,
                    context.ServiceToken,
                    context.ProjectId,
                    context.PlayerId,
                    setItemBody
                );
            
                m_Logger.LogInformation($"Updated cooldown timer for player {context.PlayerId} to {epochTime}");
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to update cooldown timer for player {PlayerId}", context.PlayerId);
                throw;
            }
        }

        private async Task<long> GetLastClaimTime(IExecutionContext context)
        {
            try
            {
                var response = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(
                    context,
                    context.ServiceToken,
                    context.ProjectId,
                    context.PlayerId,
                    new List<string> { k_CooldownKey }
                );

                var claimTimeItem = response.Data.Results.FirstOrDefault(item => item.Key == k_CooldownKey);

                if (claimTimeItem?.Value == null)
                {
                    m_Logger.LogInformation("No previous claim time found for player {PlayerId}", context.PlayerId);
                    return 0;
                }

                try
                {
                    var timestamp = JsonConvert.DeserializeObject<long>(claimTimeItem.Value?.ToString() ?? "0");
                    return timestamp;
                }
                catch (JsonException)
                {
                    m_Logger.LogWarning("Could not parse claim time for player {PlayerId}", context.PlayerId);
                    return 0;
                }
            }
            catch (Exception e)
            {
                m_Logger.LogWarning(e, "Error getting claim time for player {PlayerId}, defaulting to 0", context.PlayerId);
                return 0;
            }
        }
    }
}
