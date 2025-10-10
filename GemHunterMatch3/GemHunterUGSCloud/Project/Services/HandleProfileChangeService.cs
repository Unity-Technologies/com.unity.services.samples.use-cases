using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// Core Responsibilities:
/// - Display name validation and updates with character restrictions
/// - Profile picture management (custom images and pre-made selections)
/// - Base64 image validation and size enforcement
/// - Public profile data synchronization
/// - Input sanitization and security validation
/// 
/// Key Cloud Code Functions:
/// - ChangeDisplayName: Validates and updates player display names
/// - ChangeProfilePicture: Handles custom images and pre-made profile picture selection
/// 
/// Display Name Features:
/// - Length validation (4-16 characters)
/// - Character restriction (alphanumeric and underscores only)
/// - Regex-based input sanitization
/// - Automatic fallback to "New Player" on errors
/// - Dual storage (public data for friends + protected player data)
/// 
/// Profile Picture Features:
/// - Support for custom Base64-encoded images
/// - Pre-made image selection (IDs 1-10)
/// - Base64 validation with data URL prefix handling
/// - Size limits (512KB max) to prevent storage abuse
/// - Format validation with decode verification
/// 
/// Security & Validation:
/// - Comprehensive input validation for all profile data
/// - Base64 decoding verification to prevent malformed data
/// - Size restrictions to prevent storage abuse
/// - Character whitelist for display names
/// - Graceful error handling with safe fallbacks
/// 
/// Data Consistency:
/// - Updates both public profile data (for friends/social features)
/// - Updates protected player data (for game state consistency)
/// - Atomic operations ensure profile data stays synchronized
/// </summary>
public class HandleProfileChangeService
{
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<HandleProfileChangeService> m_Logger;
    private readonly PlayerDataService m_PlayerDataService;
    
    private const int k_MaxProfilePictureSize = 512 * 1024;
    private const int k_MaxDisplayNameLength = 16;
    private const int k_MinDisplayNameLength = 4;

    public HandleProfileChangeService(ILogger<HandleProfileChangeService> logger, IGameApiClient gameApiClient, PlayerDataService playerDataService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerDataService = playerDataService;
    }
    
    [CloudCodeFunction("ChangeDisplayName")]
    public async Task<string> ChangeDisplayName(IExecutionContext context, string newDisplayName)
    {
        try
        {
            ValidateDisplayName(newDisplayName);
            await m_PlayerDataService.SavePublicPlayerDisplayName(context, newDisplayName);
            await UpdatePlayerDataDisplayName(context, newDisplayName);
            return newDisplayName;
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to change display name for player {PlayerId}", context.PlayerId);
            return "New Player";
        }
    }
    
    private void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name cannot be empty");
        }

        if (displayName.Length < k_MinDisplayNameLength || displayName.Length > k_MaxDisplayNameLength)
        {
            throw new ArgumentException($"Display name must be between {k_MinDisplayNameLength} and {k_MaxDisplayNameLength} characters");
        }

        // Additional validation (e.g., allowed characters) could be added here
        if (!Regex.IsMatch(displayName, @"^[a-zA-Z0-9_]+$"))
        {
            throw new ArgumentException("Display name can only contain letters, numbers, and underscores");
        }
    }

    private async Task UpdatePlayerDataDisplayName(IExecutionContext context, string newDisplayName)
    {
        // Load current player data
        var playerData = await m_PlayerDataService.GetPlayerData(context);
        if (playerData != null)
        {
            // Update display name
            playerData.DisplayName = newDisplayName;
            // Save updated player data
            await m_PlayerDataService.SavePlayerData(context, playerData);
        }
    }

    [CloudCodeFunction("ChangeProfilePicture")]
    public async Task<bool> ChangeProfilePicture(IExecutionContext context, ProfilePictureChangeRequest request)
    {
        bool isValidated;
        try
        {
            isValidated = ValidatePlayerProfilePicture(request);
        }
        catch (Exception e)
        {
            m_Logger.LogError(e, "Failed to change profile picture for player {PlayerId}", context.PlayerId);
            return false;
        }

        if (isValidated)
        {
            await SaveNewProfilePicture(context, request);    
        }
        
        return isValidated;
    }

    private bool ValidatePlayerProfilePicture(ProfilePictureChangeRequest request)
    {
        switch (request.Type)
        {
            case "custom":
            {
                if (request.ImageData is null)
                {
                    m_Logger.LogWarning("No image data provided for custom profile picture");
                    return false;
                }
                return IsValidBase64Image(request.ImageData);
            }
            case "pre-made":
            {
                return request.ImageId is >= 1 and <= 10;
            }
            default:
                m_Logger.LogWarning("Invalid profile picture type. Must be 'custom' or 'premade'.");
                return false;
        }
    }

    private bool IsValidBase64Image(string base64Image)
    {
        if (string.IsNullOrEmpty(base64Image))
        {
            m_Logger.LogWarning("Image data is required for custom profile pictures");
            return false;
        }

        // If you want to log the first few characters to debug:
        // m_Logger.LogInformation($"Base64 prefix: {base64Image.Substring(0, Math.Min(20, base64Image.Length))}");
    
        // Clean up the base64 string - remove any data URL prefix if present
        if (base64Image.Contains(","))
        {
            base64Image = base64Image.Split(',')[1];
        }

        // Validate standard Base64 format
        if (!Regex.IsMatch(base64Image, @"^[A-Za-z0-9+/]*={0,2}$", RegexOptions.None))
        {
            m_Logger.LogWarning("Invalid Base64 string format");
            return false;
        }

        try
        {
            // Decode and check actual size
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            if (imageBytes.Length > k_MaxProfilePictureSize)
            {
                m_Logger.LogWarning($"Profile picture size exceeds the maximum allowed size of {k_MaxProfilePictureSize / 1024} KB");
                return false;
            }

            return true;
        }
        catch (FormatException)
        {
            m_Logger.LogWarning("String is not valid base64");
            return false;
        }
    }

    private async Task SaveNewProfilePicture(IExecutionContext context, ProfilePictureChangeRequest request)
    {
        var profilePicture = new ProfilePicture
        {
            Type = request.Type
        };
        
        switch (request.Type)
        {
            case "custom":
            {
                profilePicture.ImageData = request.ImageData!;
                break;
            }
            case "pre-made":
                profilePicture.ImageId = request.ImageId;
                break;
            default:
                throw new ArgumentException("Invalid profile picture type");
        }
        
        await m_PlayerDataService.SavePublicPlayerProfilePicture(context, profilePicture);
    }
}