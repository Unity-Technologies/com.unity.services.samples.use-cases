using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud.Services;

public class ParsedTokenData
{
    public DateTime Timestamp { get; init; }
    public string InstanceId { get; init; } = "";
    public string InstanceName { get; init; } = "";
    public string AdNetwork { get; init; } = "";
    public string RewardName { get; init; } = "";
    public int RewardAmount { get; init; }
}

/// <summary>
/// Handles video ad reward validation and distribution with anti-fraud protection.
/// 
/// Core Responsibilities:
/// - Ad token parsing and validation (format, timing, reward data)
/// - Anti-fraud measures (duplicate detection, rate limiting, tamper prevention)
/// - Secure reward distribution to player economy
/// - Token usage tracking to prevent replay attacks
/// 
/// Security Features:
/// - Token age validation (prevents old token reuse)
/// - Duplicate token detection (prevents replay attacks)
/// - Rate limiting (minimum time between ad rewards)
/// - Reward amount validation (prevents inflated rewards)
/// - Comprehensive logging for fraud detection
/// 
/// Key Cloud Code Functions:
/// - HandleGrantVideoAdReward: Validates ad tokens and grants rewards
/// 
/// Exception Strategy:
/// - Uses exceptions for security violations (tampering, fraud attempts)
/// - Unlike other services, validation failures are exceptional cases indicating potential abuse
/// - All validation failures throw UnauthorizedAccessException for clear security boundaries
/// - Fail-secure approach: deny rewards on any suspicious activity
/// 
/// Token Format: {timestamp}_{instanceId}_{instanceName}_{adNetwork}_{rewardName}_{rewardAmount}
/// </summary>
public class AdRewardsService
{
    #region Constants
    
    private const int k_ExpectedTokenParts = 6;
    private const int k_MaxTokenAgeMinutes = 30;
    private const int k_MaxFutureToleranceSeconds = 60;
    private const int k_MaxAdRewardAmount = 1000;
    private const string k_ExpectedAdRewardCurrencyID = "GOLD";
    private const string k_LastAdTokenKey = "LAST_AD_TOKEN";
    private const int k_MinimumAdIntervalSeconds = 10; // Minimum time between ads

    
    #endregion
    
    #region Dependencies
    
    private readonly ILogger<AdRewardsService> m_Logger;
    private readonly IGameApiClient m_GameApiClient;
    private readonly PlayerEconomyService m_PlayerEconomyService;
    private readonly PlayerDataService m_PlayerDataService;
    
    #endregion

    public AdRewardsService(
        ILogger<AdRewardsService> logger,
        IGameApiClient gameApiClient,
        PlayerEconomyService playerEconomyService,
        PlayerDataService playerDataService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerEconomyService = playerEconomyService;
        m_PlayerDataService = playerDataService;
    }
    
    #region Cloud Code Functions

    /// <summary>
    /// Handles the granting of rewards when a player watches a video ad.
    /// Validates the ad token format, reward data, timing, and grants the reward.
    /// </summary>
    /// <param name="adToken">Unique token representing this ad view with embedded reward data</param>
    /// <returns>Updated player economy data after the reward is granted</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when validation fails</exception>
    /// <exception cref="InvalidOperationException">Thrown when player economy data cannot be retrieved</exception>
    [CloudCodeFunction("HandleGrantVideoAdReward")]
    public async Task<PlayerEconomyData?> HandleGrantVideoAdReward(IExecutionContext context, string adToken)
    {
        try
        {
            m_Logger.LogInformation("Processing video ad reward for player {PlayerId} with token {AdToken}", context.PlayerId, adToken);
            
            // Parse the token into structured data
            ParsedTokenData tokenData = ParseToken(adToken);

            // Validate the token data structure and values
            ValidateTokenData(tokenData);

            // Validate token usage (checks against player's history)
            await ValidateTokenUsage(context, m_GameApiClient, adToken);

            // Grant reward
            await m_PlayerEconomyService.UpdatePlayerCurrency(context, EconomyConstants.Currencies.k_Coin, tokenData.RewardAmount);

            // Store the token to prevent reuse
            await StoreLastAdToken(context, adToken);

            m_Logger.LogInformation($"Successfully granted ad reward: {tokenData.RewardName} x{tokenData.RewardAmount} from {tokenData.AdNetwork}");

            return await m_PlayerEconomyService.GetPlayerEconomyData(context)
                ?? throw new InvalidOperationException("Failed to get player economy data");
        }
        catch (Exception ex)
        {
            m_Logger.LogError(ex, "Error granting ad reward for player {PlayerId}", context.PlayerId);
            throw;
        }
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Parses the ad token into a structured ParsedTokenData object.
    /// Expected format: {timestamp}_{instanceId}_{instanceName}_{adNetwork}_{rewardName}_{rewardAmount}
    /// </summary>
    /// <param name="adToken">The ad token to parse</param>
    /// <returns>ParsedTokenData containing the extracted values</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when token format is invalid</exception>
    private ParsedTokenData ParseToken(string adToken)
    {
        if (string.IsNullOrEmpty(adToken))
        {
            throw new UnauthorizedAccessException("Token is null or empty");
        }

        string[] parts = adToken.Split('_');
        if (parts.Length != k_ExpectedTokenParts)
        {
            throw new UnauthorizedAccessException($"Invalid token format. Expected {k_ExpectedTokenParts} parts, got {parts.Length}");
        }

        // Parse timestamp directly
        if (!long.TryParse(parts[0], out long ticks))
        {
            throw new UnauthorizedAccessException("Invalid timestamp format in token");
        }

        DateTime timestamp;
        try
        {
            // Always specify UTC for server timestamps
            timestamp = new DateTime(ticks, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new UnauthorizedAccessException("Timestamp in token is out of range");
        }

        if (!int.TryParse(parts[5], out int rewardAmount))
        {
            throw new UnauthorizedAccessException("Invalid reward amount format");
        }

        return new ParsedTokenData
        {
            Timestamp = timestamp,
            InstanceId = parts[1],
            InstanceName = parts[2],
            AdNetwork = parts[3],
            RewardName = parts[4],
            RewardAmount = rewardAmount
        };
    }
    
    /// <summary>
    /// Validates the parsed token data against business rules.
    /// </summary>
    /// <param name="data">The parsed token data to validate</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when validation fails</exception>
    private void ValidateTokenData(ParsedTokenData data)
    {
        // Validate timestamp age
        DateTime now = DateTime.UtcNow;
        TimeSpan age = now - data.Timestamp;

        if (age.TotalMinutes > k_MaxTokenAgeMinutes)
        {
            throw new UnauthorizedAccessException("Token is too old");
        }

        if (age.TotalSeconds < -k_MaxFutureToleranceSeconds)
        {
            throw new UnauthorizedAccessException("Token timestamp is too far in the future");
        }

        // Validate instance ID
        if (string.IsNullOrEmpty(data.InstanceId))
        {
            throw new UnauthorizedAccessException("Instance ID cannot be empty");
        }

        // Validate reward data
        if (data.RewardName != k_ExpectedAdRewardCurrencyID)
        {
            throw new UnauthorizedAccessException($"Invalid reward name. Expected '{k_ExpectedAdRewardCurrencyID}', got '{data.RewardName}'");
        }

        if (data.RewardAmount <= 0)
        {
            throw new UnauthorizedAccessException($"Invalid reward amount. Must be positive, got {data.RewardAmount}");
        }

        if (data.RewardAmount > k_MaxAdRewardAmount)
        {
            throw new UnauthorizedAccessException($"Invalid reward amount. Expected under {k_MaxAdRewardAmount}, got {data.RewardAmount}");
        }
    }
    
    /// <summary>
    /// Validates token usage against player's history to prevent abuse.
    /// Checks for duplicate tokens and enforces minimum time between rewards.
    /// </summary>
    /// <param name="context">Execution context</param>
    /// <param name="gameApiClient">Game API client</param>
    /// <param name="adToken">The ad token to validate</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when token has been used or timing is invalid</exception>
    private async Task ValidateTokenUsage(IExecutionContext context, IGameApiClient gameApiClient, string adToken)
    {
        try
        {
            // Try to get the player's last ad token
            var (success, previousTokenObj) = await m_PlayerDataService.TryGetProtectedCloudData(context, new List<string> { k_LastAdTokenKey });
            
            string? previousToken;
            if (success)
            {
                previousToken = $"{previousTokenObj}";
            }
            else
            {
                previousToken = null;
            }

            // Check for duplicate token usage
            if (HasTokenBeenUsed(adToken, previousToken))
            {
                throw new UnauthorizedAccessException("Ad token is duplicate of last used. Reward denied.");
            }

            // Validate reward timing
            if (!HasSufficientAdIntervalElapsed(previousToken))
            {
                throw new UnauthorizedAccessException("Ad rewarded too quickly. Reward denied.");
            }
        }

        catch (Exception ex) when(!(ex is UnauthorizedAccessException))
        {
            // Log the error but deny the reward for safety if there's an issue checking the token
            m_Logger.LogError(ex, "Error validating token usage");
            throw new UnauthorizedAccessException("Unable to validate token usage");
        }
    }
    
    /// <summary>
    /// Checks if the provided ad token has been used before to prevent duplicate rewards.
    /// </summary>
    /// <param name="adToken">Current ad token to check</param>
    /// <param name="previousToken">Previously stored token (if any)</param>
    /// <returns>True if the token has been used before, false if it's new/unique</returns>
    private bool HasTokenBeenUsed(string adToken, string? previousToken)
    {
        if (!string.IsNullOrEmpty(previousToken) && adToken == previousToken)
        {
            m_Logger.LogWarning($"Duplicate ad token detected: {adToken}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates that sufficient time has passed since the last ad reward was granted.
    /// </summary>
    /// <param name="previousToken">Previously stored token containing timestamp</param>
    /// <returns>True if enough time has passed since the last reward, false otherwise</returns>
    private bool HasSufficientAdIntervalElapsed(string? previousToken)
    {
        if (string.IsNullOrEmpty(previousToken))
        {
            // No Previous token exists - timing is valid
            return true;
        }

        try
        {
            ParsedTokenData previousTokenData = ParseToken(previousToken);
            TimeSpan timeBetweenAds = DateTime.UtcNow - previousTokenData.Timestamp;

            if (timeBetweenAds.TotalSeconds < k_MinimumAdIntervalSeconds)
            {
                m_Logger.LogWarning("Ad rewarded too quickly. Time since last ad: {TimeBetweenAds} seconds",
                    timeBetweenAds.TotalSeconds);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            // If we can't parse the previous token, we can't validate timing
            // Better to allow the reward than block legitimate users
            m_Logger.LogError(ex, "Error parsing previous token for timing validation. Allowing reward");
            return true; // Fail-open strategy
        }
    }
    
    /// <summary>
    /// Stores the ad token and timestamp for future validation of ad rewards.
    /// </summary>
    /// <param name="adToken">The ad token to store</param>
    private async Task StoreLastAdToken(IExecutionContext context, string adToken)
    {
        // Store the token for future validation
        await m_PlayerDataService.SaveProtectedData(context, k_LastAdTokenKey, adToken);
    }
    
    #endregion
}
