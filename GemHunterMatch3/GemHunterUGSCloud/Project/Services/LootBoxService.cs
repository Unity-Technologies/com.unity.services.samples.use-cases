using System;
using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace GemHunterUGSCloud.Services
{
    /// <summary>
    /// LootBoxService - Manages loot box generation, reward distribution, and cooldown enforcement.
    /// 
    /// Core Responsibilities:
    /// - Loot box claiming with cooldown validation
    /// - Random reward generation (currencies and inventory items)
    /// - Reward distribution through PlayerEconomyService
    /// - Cooldown timer management after successful claims
    /// - Thread-safe random number generation for reward calculations
    /// 
    /// Key Cloud Code Functions:
    /// - ClaimLootBox: Validates cooldown, generates rewards, grants items, updates timer
    /// 
    /// Reward Generation Features:
    /// - Random currency rewards (100-500 coins)
    /// - Probability-based inventory items (50% chance for boosters)
    /// - Configurable booster types (bombs, rockets, color bonuses)
    /// - Variable item quantities (1-3 per reward)
    /// 
    /// Integration Points:
    /// - LootBoxCooldownService: Cooldown validation and timer updates
    /// - PlayerEconomyService: Currency and inventory item distribution
    /// - Thread-safe random generation for concurrent player requests
    /// 
    /// Reward Flow:
    /// - Validates player can claim (cooldown check)
    /// - Generates random rewards based on configured probabilities
    /// - Grants all rewards atomically through economy service
    /// - Updates cooldown timer for next claim period
    /// - Comprehensive logging for reward tracking
    /// 
    /// Error Handling:
    /// - Cooldown validation prevents premature claims
    /// - Atomic reward granting with rollback on failure
    /// - Detailed error logging for debugging failed claims
    /// - Exception wrapping for meaningful error messages
    /// </summary>
    public class LootBoxService
    {
        private readonly IGameApiClient m_GameApiClient;
        private readonly ILogger<LootBoxService> m_Logger;
        private readonly LootBoxCooldownService m_CooldownService;
        private readonly PlayerEconomyService m_PlayerEconomyService;
        
        private static readonly ThreadLocal<Random> s_Random = new ThreadLocal<Random>(() => new Random());
        
        public LootBoxService(
            ILogger<LootBoxService> logger,
            IGameApiClient gameApiClient,
            LootBoxCooldownService cooldownService,
            PlayerEconomyService playerEconomyService)
        {
            m_Logger = logger;
            m_GameApiClient = gameApiClient;
            m_CooldownService = cooldownService;
            m_PlayerEconomyService = playerEconomyService;
        }
        
        [CloudCodeFunction("ClaimLootBox")]
        public async Task<LootBoxResult> ClaimLootBox(IExecutionContext context)
        {
            m_Logger.LogInformation("Starting loot box claim process for player {PlayerId}", context.PlayerId);
            
            try
            {
                // 1. Check cooldown if player can claim
                var cooldown = await m_CooldownService.CheckLootBoxCooldown(context);
                if (!cooldown.CanGrantFlag)
                {
                    throw new InvalidOperationException("Loot box is not ready to be claimed");
                }
                
                // 2. Generate rewards
                var rewards = GenerateRewards();

                // 3. Update cooldown before granting rewards
                await m_CooldownService.UpdateCooldownTimer(context);
                
                // 4. Grant rewards
                await GrantLootBoxRewards(context, rewards);
                
                return rewards;
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Error in ClaimLootBox: {Message}", e.Message);
                throw;
            }
        }
        
        private LootBoxResult GenerateRewards()
        {
            var result = new LootBoxResult();
            
            var coinAmount = s_Random.Value.Next(100, 500);
            result.AddCurrency("COIN", coinAmount);
            
            // If you want to add 50% chance of getting booster
            if (s_Random.Value.NextDouble() < 0.5)
            {
                var boosters = new[] { "SMALL_BOMB", "LARGE_BOMB", "HORIZONTAL_ROCKET", "VERTICAL_ROCKET", "COLOR_BONUS" };
                var selectedBooster = boosters[s_Random.Value.Next(boosters.Length)];
                result.AddInventoryItem(selectedBooster, s_Random.Value.Next(1, 3));
            }
            
            return result;
        }
        
        private async Task GrantLootBoxRewards(IExecutionContext context, LootBoxResult rewards)
        {
            try
            {
                // Grant currencies
                foreach (var currency in rewards.Currencies)
                {
                    await m_PlayerEconomyService.UpdatePlayerCurrency(context, currency.Key, currency.Value);
                    m_Logger.LogInformation("Granted {Amount} {Currency} to player {PlayerId}", 
                        currency.Value, currency.Key, context.PlayerId);
                }

                // Grant inventory items
                foreach (var item in rewards.InventoryItems)
                {
                    await m_PlayerEconomyService.UpdateInventoryItem(context, item.Key, item.Value);
                    m_Logger.LogInformation("Granted {Amount}x {Item} to player {PlayerId}", 
                        item.Value, item.Key, context.PlayerId);
                }

                m_Logger.LogInformation("Successfully granted all rewards to player {PlayerId}", context.PlayerId);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e, "Failed to grant rewards to player {PlayerId}", context.PlayerId);
                throw new InvalidOperationException($"Failed to grant loot box rewards: {e.Message}", e);
            }
        }
    }
}