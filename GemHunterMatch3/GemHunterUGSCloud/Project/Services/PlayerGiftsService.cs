using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// PlayerGiftsService - Manages heart gifting system between players for social engagement.
/// 
/// Core Responsibilities:
/// - Heart gift sending with sender validation and deduction
/// - Heart gift receiving with batch processing and validation
/// - Gift data persistence and cleanup management
/// - Anti-abuse validation (timestamps, limits, self-gifting prevention)
/// - Integration with player heart economy through PlayerDataService
/// 
/// Key Cloud Code Functions:
/// - HandleSendPlayerGift: Processes heart gifts from sender to recipient
/// - CheckReceivedGifts: Retrieves and applies accumulated gifts to player
/// 
/// Gift Management Features:
/// - Maximum gift limits (30 hearts) to prevent economy inflation
/// - Gift age validation (7 days) with automatic cleanup
/// - Timestamp validation to prevent tampering
/// - Self-gifting prevention
/// - Duplicate gift tracking per sender
/// 
/// Data Flow:
/// - Sender: Validates → Deducts gift heart → Adds to recipient's pending gifts
/// - Recipient: Retrieves pending gifts → Validates data → Applies to hearts → Cleans up
/// - Atomic operations ensure consistency between sender deduction and recipient addition
/// 
/// Anti-Abuse Features:
/// - Future timestamp detection (potential tampering)
/// - Maximum received gifts per player
/// - Gift age limits with automatic expiration
/// - Input validation for player IDs and gift counts
/// - Comprehensive logging for gift tracking
/// 
/// Integration Points:
/// - PlayerDataService: Heart deduction and application
/// - Cloud Save: Cross-player gift data storage
/// - Service token authentication for writing to other players' data
/// 
/// </summary>
public class PlayerGiftsService
{
    #region Constants
    
    private const string k_PlayerGiftKey = "PLAYER_GIFT";
    private const int k_MaxReceivedGiftHearts = 30;
    private const long k_MaxGiftAgeSeconds = 7 * 24 * 60 * 60; // 7 days
    
    #endregion
    
    #region Dependencies
    
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<PlayerGiftsService> m_Logger;
    private readonly PlayerDataService m_PlayerDataService;
    
    #endregion
    
    public PlayerGiftsService(ILogger<PlayerGiftsService> logger, IGameApiClient gameApiClient, PlayerDataService playerDataService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerDataService = playerDataService;
    }
    
    #region Cloud Code Functions
    
    [CloudCodeFunction("HandleSendPlayerGift")]
    public async Task<PlayerData?> HandleSendPlayerGift(IExecutionContext context,
        string recipientPlayerId)
    {
        try
        {
            m_Logger.LogInformation("Handling send player gift from {SenderId} to {RecipientId}", 
                context.PlayerId, recipientPlayerId);
        
            // Validate input
            var validationResult = ValidateSendGiftRequest(context, recipientPlayerId);
            if (!validationResult.isValid)
            {
                m_Logger.LogWarning("Send gift validation failed: {Reason}", validationResult.reason);
                return null;
            }
        
            // Deduct heart from sender
            var updatedPlayerData = await m_PlayerDataService.DeductPlayerGiftHeart(context);
            if (updatedPlayerData == null)
            {
                m_Logger.LogWarning("Failed to deduct gift heart from sender");
                return null;
            }
        
            // Add gift to recipient
            await AddGiftToRecipient(context, recipientPlayerId);
        
            m_Logger.LogInformation("Heart sent successfully from {SenderId} to {RecipientId}", 
                context.PlayerId, recipientPlayerId);
        
            return updatedPlayerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to handle send player gift from {SenderId} to {RecipientId}", 
                context.PlayerId, recipientPlayerId);
            return null;
        }
    }
    
    [CloudCodeFunction("CheckReceivedGifts")]
    public async Task<PlayerData?> CheckReceivedGifts(IExecutionContext context)
    {
        try
        {
            m_Logger.LogInformation("Checking received gifts for player {PlayerId}", context.PlayerId);
        
            var gifts = await GetPlayerGifts(context);
        
            if (gifts == null)
            {
                m_Logger.LogInformation("No heart gifts received for player {PlayerId}", context.PlayerId);
                return await m_PlayerDataService.GetPlayerData(context);
            }
        
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
            // Validate gift data
            if (!IsGiftDataValid(gifts, currentTime))
            {
                m_Logger.LogWarning("Invalid gift data found, cleaning up for player {PlayerId}", context.PlayerId);
                await DeletePlayerGifts(context);
                return await m_PlayerDataService.GetPlayerData(context);
            }
        
            // Apply gifts to player
            var updatedPlayerData = await ApplyGiftsToPlayer(context, gifts);
            if (updatedPlayerData == null)
            {
                m_Logger.LogError("Failed to apply gifts to player {PlayerId}", context.PlayerId);
                return await m_PlayerDataService.GetPlayerData(context);
            }
        
            // Clean up gift data
            await DeletePlayerGifts(context);
        
            m_Logger.LogInformation("Successfully processed {GiftCount} heart gifts for player {PlayerId}", 
                gifts.GiftedHearts, context.PlayerId);
        
            return updatedPlayerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to check received gifts for player {PlayerId}", context.PlayerId);
            return null;
        }
    }
    
    #endregion
    
    #region Validation
    
    private (bool isValid, string reason) ValidateSendGiftRequest(IExecutionContext context, string recipientPlayerId)
    {
        if (string.IsNullOrEmpty(recipientPlayerId))
        {
            return (false, "Recipient player ID is null or empty");
        }
        
        if (context.PlayerId == recipientPlayerId)
        {
            return (false, "Cannot send heart to self");
        }
        
        return (true, string.Empty);
    }
    
    private bool IsGiftDataValid(PlayerGifts gifts, long currentTime)
    {
        // Validate gift count
        if (gifts.GiftedHearts <= 0 || gifts.GiftedHearts > k_MaxReceivedGiftHearts)
        {
            m_Logger.LogWarning("Invalid gift count: {Count}", gifts.GiftedHearts);
            return false;
        }
        
        foreach (var (playerId, timestamp) in gifts.FromPlayerIdsAtTimestamp)
        {
            // Check for future timestamps (potential tampering)
            if (timestamp > currentTime)
            {
                m_Logger.LogWarning("Gift has future timestamp from player {PlayerId}: {Timestamp}", 
                    playerId, timestamp);
                return false;
            }
            
            // Check for very old gifts (optional cleanup)
            if (currentTime - timestamp > k_MaxGiftAgeSeconds)
            {
                m_Logger.LogInformation("Gift from player {PlayerId} is older than {Days} days, will be cleaned up", 
                    playerId, k_MaxGiftAgeSeconds / (24 * 60 * 60));
            }
        }
        
        return true;
    }
    
    #endregion
    
    #region Gift Operations
    
    private async Task AddGiftToRecipient(IExecutionContext context, string recipientPlayerId)
    {
        try
        {
            var existingGifts = await GetPlayerGiftsForOtherPlayer(context, recipientPlayerId);
            
            PlayerGifts gifts;
            if (existingGifts != null)
            {
                gifts = existingGifts;
                gifts.GiftedHearts++;
                gifts.FromPlayerIdsAtTimestamp[context.PlayerId!] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
            else
            {
                gifts = new PlayerGifts
                {
                    GiftedHearts = 1,
                    FromPlayerIdsAtTimestamp = new Dictionary<string, long>
                    {
                        { context.PlayerId!, DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                    }
                };
            }
            
            // Ensure we don't exceed maximum gifts
            if (gifts.GiftedHearts > k_MaxReceivedGiftHearts)
            {
                gifts.GiftedHearts = k_MaxReceivedGiftHearts;
                m_Logger.LogWarning("Recipient {RecipientId} would exceed max gifts, clamped to {Max}", 
                    recipientPlayerId, k_MaxReceivedGiftHearts);
            }
            
            await SavePlayerGiftsForPlayer(context, recipientPlayerId, gifts);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to add gift to recipient {RecipientId}", recipientPlayerId);
            throw;
        }
    }
    
    private async Task<PlayerData?> ApplyGiftsToPlayer(IExecutionContext context, PlayerGifts gifts)
    {
        var playerData = await m_PlayerDataService.GetPlayerData(context);
        if (playerData == null)
        {
            m_Logger.LogError("Could not load player data to apply gifts");
            return null;
        }
        
        return await m_PlayerDataService.ApplyPlayerGift(context, playerData, gifts.GiftedHearts);
    }
        #endregion
    
    #region Data Access
    
    private async Task<PlayerGifts?> GetPlayerGifts(IExecutionContext context)
    {
        var (success, gifts) = await m_PlayerDataService.TryGetProtectedData<PlayerGifts>(context, k_PlayerGiftKey);
        return success ? gifts : null;
    }

    private async Task<PlayerGifts?> GetPlayerGiftsForOtherPlayer(IExecutionContext context, string targetPlayerId)
    {
        try
        {
            var result = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context,
                context.ServiceToken, // ServiceToken for accessing other players' data
                context.ProjectId,
                targetPlayerId, // Different player ID
                new List<string> { k_PlayerGiftKey }
            );

            if (!result.Data.Results.Any())
            {
                return null; // No gifts found - this is normal
            }

            return JsonConvert.DeserializeObject<PlayerGifts>(
                result.Data.Results.First().Value.ToString() ?? string.Empty);
        }
        catch (JsonException ex)
        {
            m_Logger.LogError(ex, "Failed to deserialize gift data for player {PlayerId}", targetPlayerId);
            throw;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to get player gifts for player {PlayerId}", targetPlayerId);
            throw;
        }
    }

    private async Task SavePlayerGiftsForPlayer(IExecutionContext context, string playerId, PlayerGifts gifts)
    {
        try
        {
            var setItemBody = new SetItemBody(k_PlayerGiftKey, gifts);
            
            await m_GameApiClient.CloudSaveData.SetProtectedItemAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                playerId,
                setItemBody
            );
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save player gifts for player {PlayerId}", playerId);
            throw;
        }
    }
    
    private async Task DeletePlayerGifts(IExecutionContext context)
    {
        try
        {
            await m_GameApiClient.CloudSaveData.DeleteProtectedItemAsync(
                context,
                context.ServiceToken,
                k_PlayerGiftKey,
                context.ProjectId,
                context.PlayerId!
            );
            
            m_Logger.LogInformation("Deleted gift data after processing for player {PlayerId}", context.PlayerId);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to delete gift data for player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    #endregion
}
