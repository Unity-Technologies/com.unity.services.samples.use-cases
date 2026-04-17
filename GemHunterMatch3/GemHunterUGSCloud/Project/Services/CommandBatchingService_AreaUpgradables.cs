using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// CommandBatchingService_AreaUpgradables - Processes and validates batches of area completion commands with anti-cheat measures.
/// 
/// Core Responsibilities:
/// - Batch validation of area progression commands (unlock, upgrade, complete)
/// - Anti-cheat timing validation to prevent automated/scripted completion
/// - Command sequence validation and business rule enforcement
/// - Remote Config-driven reward distribution for area completion
/// - Comprehensive logging for monitoring and fraud detection
/// 
/// Key Cloud Code Functions:
/// - ProcessAreaCompleteCommandBatch: Validates command batches and distributes rewards
/// 
/// Anti-Cheat Features:
/// - Time-signed command validation (minimum time between first/last command)
/// - Command sequence validation (unlocks before upgrades, area complete must be last)
/// - Batch size limits and item count restrictions
/// - Graceful handling of malformed or tampered command timestamps
/// 
/// Command Types Supported:
/// - UnlockAreaItemCommand: Unlocks new items in area progression
/// - UpgradeAreaItemCommand: Upgrades existing area items
/// - AreaCompleteCommand: Marks area as completed (must be final command)
/// 
/// Reward System:
/// - Uses Unity Remote Config for flexible reward configuration
/// - Supports inventory item rewards with automatic aggregation
/// - Rewards only distributed after successful validation
/// - Configurable per area without code deployment
/// 
/// Security Strategy:
/// - Fail-secure: Any validation failure rejects entire batch
/// - Time validation prevents rapid automation
/// - Command sequence prevents out-of-order exploitation
/// - Comprehensive error logging for abuse detection
/// 
/// </summary>
public class CommandBatchingService_AreaUpgradables
{
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<CommandBatchingService_AreaUpgradables> m_Logger;
    private readonly PlayerEconomyService m_PlayerEconomyService;
    
    private const string k_AreaRewardsRemoteConfigKey = "AREA_COMPLETE_REWARDS";
    private const string k_UnlockAreaItemCommandKey = "UnlockAreaItemCommand";
    private const string k_UpgradeAreaItemCommandKey = "UpgradeAreaItemCommand";
    private const string k_AreaCompleteCommandKey = "AreaCompleteCommand";
    
    // Minimum time (in seconds) that should elapse between first and last command
    private const int k_MinimumBatchTimeSeconds = 20;
    
    private const int k_MaxBatchSize = 50;
    private const int k_MaxAreaItems = 10;

    private enum AreaCommandType
    {
        UnlockAreaItem,
        UpgradeAreaItem,
        AreaComplete
    }
    
    /// <summary>
    /// Represents a command with its embedded timestamp for validation
    /// </summary>
    private class TimeSignedCommand
    {
        public string CommandType { get; set; }
        public DateTime Timestamp { get; set; }
        
        public TimeSignedCommand(string commandType, DateTime timestamp)
        {
            CommandType = commandType;
            Timestamp = timestamp;
        }
    }
    
    private readonly Dictionary<string, AreaCommandType> m_CommandMapping = new()
    {
        { k_UnlockAreaItemCommandKey, AreaCommandType.UnlockAreaItem },
        { k_UpgradeAreaItemCommandKey, AreaCommandType.UpgradeAreaItem },
        { k_AreaCompleteCommandKey, AreaCommandType.AreaComplete }
    };

    private class RewardItem
    {
        public string Id { get; set; } = string.Empty;
        public int Amount { get; set; }
    }

    public CommandBatchingService_AreaUpgradables(ILogger<CommandBatchingService_AreaUpgradables> logger, IGameApiClient gameApiClient, PlayerEconomyService playerEconomyService)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
        m_PlayerEconomyService = playerEconomyService;
    }

    /// <summary>
    /// Processes a batch of time-signed commands for area completion
    /// </summary>
    [CloudCodeFunction("ProcessAreaCompleteCommandBatch")]
    public async Task<CommandReward> ProcessAreaCompleteCommandBatch(IExecutionContext context, List<string> timeSignedCommandKeys)
    {
        try
        {
            // Parse the time-signed command keys
            var parsedCommands = ParseTimeSignedCommands(timeSignedCommandKeys);
            
            // Extract just the command types for standard validation
            var commandTypes = parsedCommands.Select(cmd => cmd.CommandType).ToList();
            
            ValidateCommandBatch(context, commandTypes, parsedCommands);
            
            // Process rewards
            var commandReward = await ProcessAreaCompletionRewards(context, commandTypes);
            
            m_Logger.LogInformation("Area complete batch processed successfully");
            return commandReward;
        }
        catch (Exception error)
        {
            m_Logger.LogError(error.ToString());
            throw;
        }
    }
    
    /// <summary>
    /// Parses time-signed command keys (in "CommandType|Timestamp" format) into structured
    /// TimeSignedCommand objects for validation and processing. Handles missing or invalid
    /// timestamps gracefully by using DateTime.MinValue as a sentinel.
    /// </summary>
    /// <param name="timeSignedCommandKeys">Command keys with embedded timestamps</param>
    /// <returns>List of structured command objects with their timestamps</returns>
    private List<TimeSignedCommand> ParseTimeSignedCommands(List<string> timeSignedCommandKeys)
    {
        var parsedCommands = new List<TimeSignedCommand>();
        
        foreach (var signedKey in timeSignedCommandKeys)
        {
            try
            {
                // Format is "CommandType|Timestamp"
                string[] parts = signedKey.Split('|');
                
                if (parts.Length >= 2)
                {
                    string commandType = parts[0];
                    if (DateTime.TryParse(parts[1], out DateTime timestamp))
                    {
                        parsedCommands.Add(new TimeSignedCommand(commandType, timestamp));
                        m_Logger.LogDebug($"Parsed command: {commandType} with timestamp {timestamp:yyyy-MM-dd HH:mm:ss}");
                    }
                    else
                    {
                        // Couldn't parse timestamp, use just the command type
                        parsedCommands.Add(new TimeSignedCommand(commandType, DateTime.MinValue));
                        m_Logger.LogWarning($"Could not parse timestamp for command: {commandType}");
                    }
                }
                else
                {
                    // No timestamp separator found, treat as simple command type
                    parsedCommands.Add(new TimeSignedCommand(signedKey, DateTime.MinValue));
                    m_Logger.LogWarning($"No timestamp found in command key: {signedKey}");
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Error parsing command key: {signedKey}, Error: {e.Message}");
                // Add what we can salvage from the key
                parsedCommands.Add(new TimeSignedCommand(signedKey, DateTime.MinValue));
            }
        }
        
        return parsedCommands;
    }
     
    /// <summary>
    /// Validates that sufficient time has elapsed between the first and last command,
    /// which helps prevent automated/scripted execution and ensures the player genuinely
    /// engaged with the area progression.
    /// </summary>
    /// <param name="commands">The time-signed commands to validate</param>
    /// <returns>True if sufficient time has elapsed</returns>
    private bool HasSufficientTimeForAreaProgress(List<TimeSignedCommand> commands)
    {
        // Filter to commands with valid timestamps
        var commandsWithTimestamps = commands.Where(c => c.Timestamp != DateTime.MinValue).ToList();
        
        var firstTimestamp = commandsWithTimestamps.Min(c => c.Timestamp);
        var lastTimestamp = commandsWithTimestamps.Max(c => c.Timestamp);
        
        var timeSpan = lastTimestamp - firstTimestamp;
        
        m_Logger.LogInformation($"Time between first and last command: {timeSpan.TotalSeconds:F1} seconds");
        
        if (timeSpan.TotalSeconds < k_MinimumBatchTimeSeconds)
        {
            m_Logger.LogWarning($"Commands executed too quickly! Time elapsed: {timeSpan.TotalSeconds:F1}s, minimum required: {k_MinimumBatchTimeSeconds}s");
            return false;
        }
        
        return true;
    }
    
    private void ValidateCommandBatch(IExecutionContext context, List<string> commandTypes, List<TimeSignedCommand> signedCommands)
    {
        var playerId = context.PlayerId;
        // Validate command sequence, structure, and counts
        ValidateCommands(playerId, commandTypes);
    
        // Validate timing
        if (!HasSufficientTimeForAreaProgress(signedCommands))
        {
            var errorMessage = $"Command batch executed too quickly for player {context.PlayerId}. Potential automation detected.";
            m_Logger.LogError(errorMessage);
            throw new UnauthorizedAccessException(errorMessage);
        }
    }
    
    private void ValidateCommands(string playerId, List<string> commands)
    {
        // Basic count validation
        ValidateBatchSize(commands);
        
        // Verify area complete is the last command
        ValidateAreaCompleteIsLast(commands);
        
        // Validate command sequence and structure
        ValidateCommandSequence(playerId, commands);
    }
    
    private void ValidateBatchSize(List<string> commands)
    {
        if (commands.Count == 0)
        {
            m_Logger.LogError("Commands list is empty");
            throw new ArgumentException("Command batch cannot be empty");
        }
        
        if (commands.Count > k_MaxBatchSize)
        {
            m_Logger.LogError($"Too many commands in batch: {commands.Count}");
            throw new ArgumentException("Command batch exceeds maximum size");
        }
    }

    private void ValidateAreaCompleteIsLast(List<string> commands)
    {
        if (commands.Count == 0 || commands.Last() != k_AreaCompleteCommandKey)
        {
            m_Logger.LogError("Last command must be Area Complete");
            throw new ArgumentException("Command batch must end with Area Complete command");
        }
    }

    private void ValidateCommandSequence(string playerId, List<string> commands)
    {
        bool areaCompleteSeen = false;
        int areaItemsUnlocked = 0;

        foreach (var commandString in commands)
        {
            // Validate command exists - assume cheating if not
            if (!m_CommandMapping.TryGetValue(commandString, out var command))
            {
                var errorMessage = $"Invalid command type detected: {commandString} for player {playerId}. Potential tampering.";
                m_Logger.LogError(errorMessage);
                throw new UnauthorizedAccessException(errorMessage); // Security violation!
            }

            // No commands should appear after an AreaComplete command
            if (areaCompleteSeen)
            {
                var errorMessage = $"Invalid command sequence for player {playerId}: commands found after Area Complete";
                m_Logger.LogError(errorMessage);
                throw new UnauthorizedAccessException(errorMessage);
            }
            
            if (areaItemsUnlocked > k_MaxAreaItems)
            {
                var errorMessage = $"Too many area items unlocked for player {playerId}: {areaItemsUnlocked} exceeds maximum of {k_MaxAreaItems}";
                m_Logger.LogError(errorMessage);
                throw new UnauthorizedAccessException(errorMessage);
            }

            switch (command)
            {
                case AreaCommandType.UnlockAreaItem:
                    areaItemsUnlocked++;
                    break;
                case AreaCommandType.UpgradeAreaItem:
                    // Note: For simplicity, no rules for area upgrades
                    break;
                case AreaCommandType.AreaComplete:
                    areaCompleteSeen = true;
                    break;
            }
        }
    }
    
    /// <summary>
    /// Processes rewards for area completion based on the validated commands
    /// </summary>
    private async Task<CommandReward> ProcessAreaCompletionRewards(IExecutionContext context, List<string> commandTypes)
    {
        var rewards = await GetAreaCompletionRewards(context);
        await DistributeInventoryRewards(context, rewards);

        return new CommandReward
        {
            Rewards = rewards.Select(r => new Reward
            {
                Service = "inventory",
                Id = r.Id,
                Amount = r.Amount
            }).ToList()
        };
    }

    private async Task<List<RewardItem>> GetAreaCompletionRewards(IExecutionContext context)
    {
        // Get and parse Remote Config
        var remoteConfigResponse = await m_GameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            context.EnvironmentId,
            null,
            new List<string> { k_AreaRewardsRemoteConfigKey });

        if (remoteConfigResponse?.Data?.Configs?.Settings == null)
        {
            var errorMessage = "Failed to retrieve reward data from Remote Config.";
            m_Logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        var rewardsConfig = remoteConfigResponse.Data.Configs.Settings[k_AreaRewardsRemoteConfigKey].ToString()!;

        var areaRewards = JsonConvert.DeserializeObject<Dictionary<string, List<RewardItem>>>(rewardsConfig);

        if (!areaRewards.TryGetValue("area1CompleteRewards", out var area1Rewards))
        {
            throw new InvalidOperationException("Missing area1CompleteRewards configuration");
        }

        return area1Rewards;
    }

    private async Task DistributeInventoryRewards(IExecutionContext context, List<RewardItem> rewards)
    {
        foreach (var reward in rewards)
        {
            await m_PlayerEconomyService.UpdateInventoryItem(context, reward.Id, reward.Amount);
            m_Logger.LogInformation("Added {Amount} of {ItemId} to player inventory", reward.Amount, reward.Id);
        }
    }
}
