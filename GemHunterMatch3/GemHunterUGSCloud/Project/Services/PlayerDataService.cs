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
/// Central data management service for player information in a mobile game metagame hub.
/// 
/// Core Responsibilities:
/// - Player lifecycle management (new player onboarding, existing player validation/migration)
/// - Data persistence via Unity Cloud Save (protected and public data)
/// - Game mechanics (heart system, star progression, level completion handling)
/// - Profile management (display names, profile pictures)
/// 
/// Key Cloud Code Functions:
/// - OnSignInHandlePlayerInitialization: Main entry point for player setup
/// - HandleLevelWon/HandleLevelLost: Process gameplay outcomes
/// - SyncAllPlayerData: Reconnection sync
/// 
/// Documentation:
/// - Cloud Save in Cloud Code docs: 
/// https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.CloudSave.CloudSaveDataApi.html
/// </summary>
public class PlayerDataService
{
    #region Constants
    
    private const string k_PlayerDataKey = "PLAYER_DATA";
    public const string k_PublicProfilePictureKey = "PROFILE_PICTURE";
    public const string k_PublicDisplayNameKey = "DISPLAY_NAME";
    
    private const int k_MaxPreMadeProfilePictures = 9;
    private const int k_MaxGiftHearts = 3;
    private const int k_MaxPossibleStars = 25;
    private const int k_MaxPossibleHearts = 100;
    private const int k_DefaultAreaMaxProgress = 25;
    
    #endregion
    
    #region Dependencies
    
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<PlayerDataService> m_Logger;
    private readonly PlayerEconomyService m_PlayerEconomyService;
    private readonly GiftHeartCooldownService m_GiftHeartCooldownService;
    private static readonly Random s_Random = new Random();
    
    #endregion
    
    public PlayerDataService(ILogger<PlayerDataService> logger, IGameApiClient gameApiClient, PlayerEconomyService playerEconomyService, GiftHeartCooldownService giftHeartCooldownService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerEconomyService = playerEconomyService;
        m_GiftHeartCooldownService = giftHeartCooldownService;
    }
    
    #region Cloud Code Functions
    
    [CloudCodeFunction("OnSignInHandlePlayerInitialization")]
    public async Task<PlayerInitializationResponse> OnSignInHandlePlayerInitialization(IExecutionContext context)
    {
        if (context.PlayerId == null)
        {
            m_Logger.LogError("PlayerId cannot be null during initialization");
            throw new ArgumentNullException(nameof(context.PlayerId));
        }
        
        m_Logger.LogInformation("Handling consolidated player initialization for {PlayerId}", context.PlayerId);

        try
        {
            var (success, playerData) = await TryGetPlayerData(context);
            bool isNewPlayer = !success || playerData == null;
        
            PlayerInitializationResponse response;
        
            if (isNewPlayer)
            {
                response = await HandleNewPlayerInitialization(context);
            }
            else
            {
                response = await HandleExistingPlayerInitialization(context, playerData!);
            }
        
            response.InitializationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
            m_Logger.LogInformation("Successfully completed consolidated initialization for {PlayerId}, IsNew: {IsNew}", 
                context.PlayerId, response.IsNewPlayer);
        
            return response;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to initialize player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    /// <summary>
    /// Lightweight sync method for reconnection scenarios where full initialization isn't needed
    /// </summary>
    [CloudCodeFunction("SyncAllPlayerData")]
    public async Task<PlayerDataSyncResponse?> SyncAllPlayerData(IExecutionContext context)
    {
        if (context.PlayerId == null)
        {
            m_Logger.LogError("PlayerId cannot be null during sync");
            throw new ArgumentNullException(nameof(context.PlayerId));
        }
        
        m_Logger.LogInformation("Syncing player data for {PlayerId}", context.PlayerId);

        try
        {
            var (playerSuccess, playerData) = await TryGetPlayerData(context);
            if (!playerSuccess || playerData == null)
            {
                m_Logger.LogWarning("Cannot sync: Player data not found for {PlayerId}", context.PlayerId);
                return null;
            }

            var economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context);
            if (economyData == null)
            {
                m_Logger.LogWarning("Cannot sync: Economy data not found for {PlayerId}", context.PlayerId);
                return null;
            }

            return new PlayerDataSyncResponse
            {
                PlayerData = playerData,
                EconomyData = economyData,
                LastUpdateTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to sync player data for {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    [CloudCodeFunction("GetPlayerData")]
    public async Task<PlayerData?> GetPlayerData(IExecutionContext context)
    {
        m_Logger.LogInformation("GetPlayerData method called");
        var (success, playerData) = await TryGetPlayerData(context);

        if (success && playerData != null)
        {
            return playerData;
        }
        
        m_Logger.LogWarning("PlayerData null--likely new player");
        return null;
    }
    
    [CloudCodeFunction("HandleLevelWon")]
    public async Task<PlayerData?> HandleLevelWon(IExecutionContext context)
    {
        // TODO: In your game, consider adding validation such as:
        // - Does player have hearts to play (unless infinite hearts active)?
        // - Is the level within player's unlocked progression area?
        // - Rate limiting: Has player completed levels suspiciously fast?
        // - Score/time validation: Are completion metrics reasonable?

        try
        {
            m_Logger.LogInformation("Handling level won for player {PlayerId}", context.PlayerId);
        
            var (success, playerData) = await TryGetPlayerData(context);
            if (!success || playerData == null)
            {
                m_Logger.LogError("Cannot handle level won: Player data not found for player {PlayerId}", context.PlayerId);
                return null;
            }
        
            var updatedPlayerData = await AddStar(context, playerData);
            m_Logger.LogInformation("Successfully handled level won for player {PlayerId}", context.PlayerId);
            return updatedPlayerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to handle level won for player {PlayerId}", context.PlayerId);
            return null; // Or throw, depending on how you want the client to handle it
        }
    }
    
    [CloudCodeFunction("HandleLevelLost")]
    public async Task<PlayerData?> HandleLevelLost(IExecutionContext context)
    {
        try
        {
            m_Logger.LogInformation("Handling level lost for player {PlayerId}", context.PlayerId);
        
            var (success, playerData) = await TryGetPlayerData(context);
            if (!success || playerData == null)
            {
                m_Logger.LogError("Cannot handle level lost: Player data not found for player {PlayerId}", context.PlayerId);
                return null;
            }
        
            var updatedPlayerData = await DeductHeart(context, playerData); // If you modernize this too
            m_Logger.LogInformation("Successfully handled level lost for player {PlayerId}", context.PlayerId);
            return updatedPlayerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to handle level lost for player {PlayerId}", context.PlayerId);
            return null;
        }
    }
    
    #endregion

    #region Player Lifecycle Management
    
    private async Task<PlayerInitializationResponse> HandleNewPlayerInitialization(IExecutionContext context)
    {
        m_Logger.LogInformation("Initializing new player {PlayerId}", context.PlayerId);

        try
        {
            var playerData = CreateDefaultPlayerData();
            var profilePicture = CreateStartingProfilePicture();

            // Initialize economy data
            var economyTasks = new[]
            {
                m_PlayerEconomyService.AddBonusNewPlayerCurrencies(context),
                m_PlayerEconomyService.InitializeNewPlayerInventoryItems(context)
            };
            
            // Run economy initialization in parallel with player data save
            var savePlayerDataTask = SaveAllPlayerData(context, playerData);
            var saveProfileTask = SavePublicPlayerProfilePicture(context, profilePicture);
            
            await Task.WhenAll(economyTasks.Concat(new[] { savePlayerDataTask, saveProfileTask }));

            // Get the initialized economy data
            var economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context);
            
            if (economyData == null)
            {
                m_Logger.LogError("Failed to initialize economy data for new player {PlayerId}", context.PlayerId);
                throw new InvalidOperationException("Failed to initialize player economy");
            }

            return new PlayerInitializationResponse
            {
                PlayerData = playerData,
                EconomyData = economyData,
                ProfilePicture = profilePicture,
                IsNewPlayer = true
            };
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to initialize new player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    private async Task<PlayerInitializationResponse> HandleExistingPlayerInitialization(IExecutionContext context, PlayerData playerData)
    {
        m_Logger.LogInformation("Initializing existing player {PlayerId}", context.PlayerId);

        try
        {
            bool needsSave = false;

            // Validate and migrate existing player data - each operation returns true if changes were made
            // This ensures players with legacy/corrupted data are automatically fixed on sign-in
            needsSave |= EnsureGameAreasExist(playerData);
            needsSave |= FixAreaProgressTracking(playerData);
            needsSave |= ValidateAndFixPlayerData(playerData);
            needsSave |= await CheckAndReplenishGiftHearts(context, playerData);

            // Get economy data and profile picture in parallel
            var economyTask = m_PlayerEconomyService.GetPlayerEconomyData(context);
            var profileTask = GetPlayerProfilePicture(context);
            var saveTask = needsSave ? SaveAllPlayerData(context, playerData) : Task.CompletedTask;

            await Task.WhenAll(economyTask, profileTask, saveTask);

            var economyData = await economyTask;
            var profilePicture = await profileTask;

            if (economyData == null)
            {
                m_Logger.LogError("Failed to get economy data for existing player {PlayerId}", context.PlayerId);
                throw new InvalidOperationException("Failed to get player economy data");
            }

            bool fixedMissingItems = await m_PlayerEconomyService.EnsurePlayerHasRequiredInventoryItems(context, economyData);

            if (fixedMissingItems)
            {
                economyData = await m_PlayerEconomyService.GetPlayerEconomyData(context);
            }

            return new PlayerInitializationResponse
            {
                PlayerData = playerData,
                EconomyData = economyData,
                ProfilePicture = profilePicture,
                IsNewPlayer = false
            };
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to initialize existing player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    private async Task<ProfilePicture?> GetPlayerProfilePicture(IExecutionContext context)
    {
        var (success, profilePicture) = await TryGetPlayerProfilePicture(context);
        return success ? profilePicture : null;
    }
    
    private async Task<bool> CheckAndReplenishGiftHearts(IExecutionContext context, PlayerData playerData)
    {
        if (playerData.GiftHearts < k_MaxGiftHearts && 
            await m_GiftHeartCooldownService.CheckGiftHeartReplenish(context))
        {
            playerData.GiftHearts = k_MaxGiftHearts;
            m_Logger.LogInformation("Replenished gift hearts");
            return true;
        }
        return false;
    }

    #endregion

    #region Data Validation

    private bool EnsureGameAreasExist(PlayerData playerData)
    {
        if (playerData.GameAreasData == null || !playerData.GameAreasData.Any())
        {
            playerData.GameAreasData = new List<AreaData> { CreateFirstAreaData() };
            m_Logger.LogInformation("Added missing game areas data");
            return true;
        }
        return false;
    }

    private bool FixAreaProgressTracking(PlayerData playerData)
    {
        bool needsFix = false;

        foreach (var area in playerData.GameAreasData)
        {
            if (area.MaxProgress <= 0)
            {
                area.MaxProgress = k_DefaultAreaMaxProgress;
                needsFix = true;
            }
        }

        if (needsFix)
        {
            m_Logger.LogInformation("Fixed area progress tracking");
        }

        return needsFix;
    }

    private bool ValidateAndFixPlayerData(PlayerData playerData)
    {
        bool needsFix = false;
    
        var (heartsChanged, newHearts) = ClampValue(playerData.Hearts, 0, k_MaxPossibleHearts, "hearts");
        var (starsChanged, newStars) = ClampValue(playerData.Stars, 0, k_MaxPossibleStars, "stars");
        var (giftHeartsChanged, newGiftHearts) = ClampValue(playerData.GiftHearts, 0, k_MaxGiftHearts, "gift hearts");
    
        if (heartsChanged) playerData.Hearts = newHearts;
        if (starsChanged) playerData.Stars = newStars;
        if (giftHeartsChanged) playerData.GiftHearts = newGiftHearts;
    
        return heartsChanged || starsChanged || giftHeartsChanged;
    }
    
    private (bool changed, int clampedValue) ClampValue(int value, int min, int max, string valueName)
    {
        int clampedValue = Math.Clamp(value, min, max);
    
        if (value != clampedValue)
        {
            m_Logger.LogWarning("Player had invalid {ValueName}: {Original}, clamped to {Clamped}", 
                valueName, value, clampedValue);
            return (true, clampedValue);
        }
    
        return (false, clampedValue);
    }
    
    #endregion
    
    #region Data Creation
    
    private PlayerData CreateDefaultPlayerData()
    {
        m_Logger.LogDebug("Creating default player data");

        var initialArea = CreateFirstAreaData();
        
        var playerData = new PlayerData
        {
            DisplayName = "New Player",
            Hearts = 8,
            GiftHearts = 3,
            Stars = 5,
            HasInfiniteHeartEffectActive = false,
            CurrentArea = initialArea,
            GameAreasData = new List<AreaData>
            {
                initialArea
            },
        };
        return playerData;
    }

    private ProfilePicture CreateStartingProfilePicture()
    {
        return new ProfilePicture()
        {
            Type = "pre-made",
            ImageId = s_Random.Next(0, k_MaxPreMadeProfilePictures),
            ImageData = " "
        };
    }
    
    private static AreaData CreateFirstAreaData()
    { 
        var newAreaData = new AreaData
        {
            AreaName = "Area1",
            AreaLevel = 1,
            CurrentProgress = 0,
            MaxProgress = 25,
            TotalUpgradableSlots = 5,
            UnlockRequirement_Stars = 1,
            UpgradableAreaItems = new List<UpgradableAreaItem>()
        };
        
        newAreaData.UpgradableAreaItems.Add(new UpgradableAreaItem
        {
            UpgradableName = "Shark",
            UpgradableId = 1,
            IsUnlocked = true,  // First item starts unlocked
            CurrentLevel = 0,
            MaxLevel = 5,
            PerLevelCoinUpgradeRequirement = 20
        });
        
        return newAreaData;
    }

    #endregion

    #region Generic Data Access Layer

    /// <summary>
    /// TryGetData uses generic type parameters (T) to work with different data types.
    /// 
    /// What does "T" mean?
    /// - T is a placeholder for any type (like PlayerData, PlayerStatus, etc.)
    /// - When you call the method, you specify what T should be: TryGetData<PlayerData>(...)
    /// - This allows one method to work with many different data types safely
    /// 
    /// What does "where T : class" mean?
    /// - This is a constraint that says T must be a reference type (class), not a value type
    /// - Examples of classes: PlayerData, string, List<int>
    /// - Examples of value types: int, bool, float (these would NOT work)
    /// - This constraint ensures we can return null when data isn't found
    /// 
    /// Example Usage:
    /// var (success, playerData) = await TryGetData<PlayerData>(context, "PLAYER_DATA");
    /// var (success, config) = await TryGetData<ConfigData>(context, "CONFIG");
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<(bool success, T? data)> TryGetDefaultAccessData<T>(IExecutionContext context, string key) where T : class
    {
        try
        {
            var result = await m_GameApiClient.CloudSaveData.GetItemsAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!,
                new List<string> { key });

            var dataItem = result.Data.Results.FirstOrDefault(item => item.Key == key);

            if (dataItem?.Value == null)
            {
                return (false, null);
            }

            var deserializedData = JsonConvert.DeserializeObject<T>(dataItem.Value.ToString() ?? string.Empty);
            return (true, deserializedData);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to get data for key: {Key}, player: {PlayerId}", key, context.PlayerId);
            return (false, null);
        }
    }
    /// <summary>
    /// For getting Protected Player data--docs on Player Data access classes:
    /// https://docs.unity.com/ugs/en-us/manual/cloud-save/manual/concepts/player-data
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="context"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public async Task<(bool success, T? data)> TryGetProtectedData<T>(IExecutionContext context, string key) where T : class
    {
        try
        {
            var result = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                context.PlayerId!,
                new List<string> { key });

            var dataItem = result.Data.Results.FirstOrDefault(item => item.Key == key);
            
            if (dataItem?.Value == null)
            {
                return (false, null);
            }
            
            var deserializedData = JsonConvert.DeserializeObject<T>(dataItem.Value.ToString() ?? string.Empty);
            return (true, deserializedData);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to get protected data for key: {Key}, player: {PlayerId}", key, context.PlayerId);
            return (false, null);
        }
    }

    private async Task<(bool success, T? data)> TryGetPublicData<T>(IExecutionContext context, string key) where T : class
    {
        try
        {
            var result = await m_GameApiClient.CloudSaveData.GetPublicItemsAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                context.PlayerId!,
                new List<string> { key });
            
            var dataItem = result?.Data?.Results?.FirstOrDefault(item => item.Key == key);
            
            if (dataItem?.Value == null)
            {
                return (false, null);
            }
            
            var deserializedData = JsonConvert.DeserializeObject<T>(dataItem.Value.ToString() ?? string.Empty);
            return (true, deserializedData);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to get public data for key: {Key}, player: {PlayerId}", key, context.PlayerId);
            return (false, null);
        }
    }

    public async Task SaveProtectedData<T>(IExecutionContext context, string key, T data) where T : class
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        try
        {
            var setItemBody = new SetItemBody(key, data);
            await m_GameApiClient.CloudSaveData.SetProtectedItemAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                context.PlayerId,
                setItemBody);
            
            m_Logger.LogInformation("Successfully saved protected data for key: {Key}", key);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save protected data for key: {Key}, player: {PlayerId}", key, context.PlayerId);
            throw;
        }
    }

    /// <summary>
    /// Docs on SetPublicItemAsync:
    /// <see href="<https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.CloudSave.CloudSaveDataApi.html#Unity_Services_Apis_CloudSave_CloudSaveDataApi_SetPublicItem_System_String_System_String_Unity_Services_Apis_CloudSave_SetItemBody_System_Threading_CancellationToken_>"/>
    /// </summary>
    /// <param name="context"></param>
    /// <param name="displayName"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    private async Task SavePublicData<T>(IExecutionContext context, string key, T data) where T : class
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        try
        {
            var setItemBody = new SetItemBody(key, data);
            await m_GameApiClient.CloudSaveData.SetPublicItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId,
                setItemBody);
            m_Logger.LogInformation("Successfully saved public data for key: {Key}", key);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save public data for key: {Key}, player: {PlayerId}", key, context.PlayerId);
            throw;
        }
    }
    
    #endregion
    
    #region Player Data Operations
    
    private async Task<(bool success, PlayerData? playerData)> TryGetPlayerData(IExecutionContext context)
    {
        return await TryGetProtectedData<PlayerData>(context, k_PlayerDataKey);
    }
    
    public async Task SaveAllPlayerData(IExecutionContext context, PlayerData playerData)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
        
        try
        {
            var tasks = new[]
            {
                SaveProtectedData(context, k_PlayerDataKey, playerData),
                SavePublicData(context, k_PublicDisplayNameKey, playerData.DisplayName!),
            };

            await Task.WhenAll(tasks);
            m_Logger.LogInformation("Successfully saved all player data for player {PlayerId}", context.PlayerId);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save player data for player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    public async Task SavePlayerData(IExecutionContext context, PlayerData playerData)
    {
        await SaveProtectedData(context, k_PlayerDataKey, playerData);
    }
    
    public async Task<(bool success, string? displayName)> TryGetPlayerDisplayName(IExecutionContext context)
    {
        var (success, displayName) = await TryGetProtectedData<string>(context, k_PublicDisplayNameKey);
        return (success, displayName);
    }

    private async Task<(bool success, ProfilePicture? profilePicture)> TryGetPlayerProfilePicture(IExecutionContext context)
    {
        return await TryGetPublicData<ProfilePicture>(context, k_PublicProfilePictureKey);
    }
    
    public async Task<(bool success, Dictionary<string, string?> data)> TryGetProtectedCloudData(IExecutionContext context, List<string> keys)
    {
        try
        {
            var result = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                context.PlayerId ?? throw new InvalidOperationException("PlayerId is null"),
                keys);

            var retrievedData = new Dictionary<string, string?>();
            foreach (var item in result.Data.Results)
            {
                retrievedData[item.Key] = Convert.ToString(item.Value);
            }

            return (true, retrievedData);
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Error retrieving data from CloudSave");
            return (false, new Dictionary<string, string?>());
        }
    }

    public async Task SavePublicPlayerDisplayName(IExecutionContext context, string displayName)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
    
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name cannot be null or empty", nameof(displayName));
        }
        
        try
        {
            var setItemBody = new SetItemBody(k_PublicDisplayNameKey, displayName);
            await m_GameApiClient.CloudSaveData.SetPublicItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!,
                setItemBody);
            
            m_Logger.LogInformation("Successfully saved display name for player {PlayerId}", context.PlayerId);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save display name for player {PlayerId}", context.PlayerId);
            throw;
        }
    }

    public async Task SavePublicPlayerProfilePicture(IExecutionContext context, ProfilePicture profilePicture)
    {
        if (context.PlayerId == null)
        {
            throw new InvalidOperationException("PlayerId cannot be null");
        }
    
        if (profilePicture == null)
        {
            throw new ArgumentNullException(nameof(profilePicture));
        }
        
        try
        {
            var setItemBody = new SetItemBody(k_PublicProfilePictureKey, profilePicture);
            await m_GameApiClient.CloudSaveData.SetPublicItemAsync(
                context,
                context.AccessToken,
                context.ProjectId,
                context.PlayerId!,
                setItemBody);

            m_Logger.LogInformation("Successfully saved profile picture for player {PlayerId}", context.PlayerId);
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save profile picture for player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    #endregion
    
    #region Game Actions
    
    public async Task<PlayerData?> DeductPlayerGiftHeart(IExecutionContext context)
    {
        var (success, playerData) = await TryGetPlayerData(context);
        if (!success || playerData == null)
        {
            m_Logger.LogError("Cannot deduct gift heart: Player data not found");
            return null;
        }
    
        if (playerData.GiftHearts <= 0)
        {
            m_Logger.LogWarning("Cannot deduct gift heart: No hearts available to send");
            return null;
        }
    
        playerData.GiftHearts--;
        m_Logger.LogInformation("Deducted heart from player {PlayerId}. Hearts remaining: {Hearts}", 
            context.PlayerId, playerData.GiftHearts);
        
        try
        {
            await SavePlayerData(context, playerData);
            return playerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save player data after deducting gift heart");
            return null;
        }
    }

    public async Task<PlayerData?> ApplyPlayerGift(IExecutionContext context, PlayerData playerData, int giftsReceived)
    {
        if (giftsReceived <= 0)
        {
            m_Logger.LogWarning("Cannot apply gift: Invalid gift amount");
            return null;
        }
        
        playerData.Hearts += giftsReceived;
        
        try
        {
            await SaveProtectedData(context, k_PlayerDataKey, playerData); 
            return playerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save player data after applying gift");
            return null;
        }
    }
    
    private async Task<PlayerData> AddStar(IExecutionContext context, PlayerData playerData)
    {
        playerData.Stars += 1;
        if (playerData.Stars > k_MaxPossibleStars)
        {
            playerData.Stars = k_MaxPossibleStars;
            m_Logger.LogInformation("Player stars clamped to maximum");
        }
        
        try
        {
            await SavePlayerData(context, playerData); // Now throws
            return playerData;
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Failed to save player data after adding star");
            return null;
        }
    }

    private async Task<PlayerData?> DeductHeart(IExecutionContext context, PlayerData playerData)
    {
        // If a player has infinite hearts, don't deduct hearts
        if (playerData.HasInfiniteHeartEffectActive)
        {
            m_Logger.LogInformation($"Player {context.PlayerId} has infinite hearts active - no hearts deducted");
            return playerData;
        }
        
        playerData.Hearts -= 1;
        if (playerData.Hearts < 0)
        {
            playerData.Hearts = 0;
            m_Logger.LogInformation("Player {PlayerId} is dead :(", context.PlayerId);
        }
    
        await SavePlayerData(context, playerData);
        return playerData;
    }
    
    #endregion
}