using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.AreaUpgradables
{
    /// <summary>
    /// Manages command batching for area progression, collecting and processing commands 
    /// until an area is completed, then sending the batch to Cloud Code for validation and rewards.
    /// </summary>
    /// <remarks>
    /// Commands are stored in a temporary queue and processed when an area is completed.
    /// Each command represents a player action (unlock, upgrade) that contributes to area completion.
    /// The queue is not persisted between sessions as each individual upgrade/unlock is validated 
    /// by Cloud Code, and the area state is maintained in the cloud. The batching primarily serves 
    /// to organize end-of-area rewards and final validation.
    /// </remarks>
    public class CommandBatchSystem : IDisposable
    {
        private readonly AreaManager m_AreaManager;
        private readonly CloudBindingsProvider m_BindingsProvider;
        private readonly LocalStorageSystem m_LocalStorageSystem;
        private Queue<Command> m_CommandBatch = new Queue<Command>();
        
        // Minimum time (in seconds) that should elapse between first and last command
        private const int k_MinimumBatchTimeSeconds = 20;
        
        private bool m_ShouldSaveOnDispose = true;
        
        public event Action<CommandReward> AreaCompleteRewardsReceived;
        
        public CommandBatchSystem(
            PlayerDataManager dataManager,
            AreaManager areaManager, 
            CloudBindingsProvider bindingsProvider, 
            LocalStorageSystem localStorageSystem)
        {
            m_AreaManager = areaManager;
            m_LocalStorageSystem = localStorageSystem;
            m_BindingsProvider = bindingsProvider;
            
            m_AreaManager.AreaCompleteLocal += AreaComplete;

            var loadedCommands = m_LocalStorageSystem.LoadCommands();
            foreach (var command in loadedCommands)
            {
                m_CommandBatch.Enqueue(command);
            }
        }
        
        /// <summary>
        /// Adds a command to the batch queue for processing
        /// </summary>
        public void EnqueueCommand(Command command)
        {
            m_CommandBatch.Enqueue(command);
            
            m_LocalStorageSystem.SaveCommands(m_CommandBatch);
            
            Logger.LogVerbose($"Enqueued command: {command.GetKey()} at {command.Timestamp:HH:mm:ss}");
            
        }

        private void AreaComplete()
        {
            var areaCompleteCommand = new AreaCompleteCommand();
            areaCompleteCommand.Execute();
            EnqueueCommand(areaCompleteCommand);
            StartToFlushBatch();
        }
        
        private async void StartToFlushBatch()
        {
            await FlushBatch();
        }
        
        /// <summary>
        /// Processes all commands in the batch, converting them to command keys
        /// and sending them to Cloud Code for validation
        /// </summary>
        private async Task FlushBatch()
        {
            try
            {
                Logger.LogDemo($"Attempting to flush batch with {m_CommandBatch.Count} commands");
                
                if (m_CommandBatch.Count < 2)
                {
                    Logger.LogWarning("Command batch too small. Need at least one upgrade and an area complete command.");
                    return;
                }
                
                if (!HasSufficientTimeForAreaProgress())
                {
                    Logger.LogWarning("Command batch failed time-based validation. Area completion will not be processed.");
                    return;
                }
                
                var timeSignedKeys = ConvertCommandBatchToTimeSignedKeys();
                if (timeSignedKeys.Count > 0)
                {
                    await CallCloudCodeEndpoint(timeSignedKeys);
                    
                    m_CommandBatch.Clear();
                    m_LocalStorageSystem.ClearCommands();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error flushing command batch: {e.Message}");
                // Ensure commands are saved in case of failure
                m_LocalStorageSystem.SaveCommands(m_CommandBatch);
            }
        }
        
        /// <summary>
        /// Validates that the commands were not executed too quickly
        /// </summary>
        private bool HasSufficientTimeForAreaProgress()
        {
            var commandsWithTimestamps = m_CommandBatch.Select(cmd => cmd.Timestamp).ToList();
            var firstTimestamp = commandsWithTimestamps.Min();
            var lastTimestamp = commandsWithTimestamps.Max();
            
            TimeSpan timeSpan = lastTimestamp - firstTimestamp;
            
            if (timeSpan.TotalSeconds < k_MinimumBatchTimeSeconds)
            {
                Logger.LogWarning($"Command batch executed too quickly. Time elapsed: {timeSpan.TotalSeconds} seconds, " +
                    $"minimum required: {k_MinimumBatchTimeSeconds} seconds");
                return false;
            }
            
            Logger.LogDemo($"Command batch passed time validation. Total area progress time took: {timeSpan.TotalSeconds} seconds");
            return true;
        }
        
        /// <summary>
        /// Converts queued commands to their string identifiers for cloud processing
        /// </summary>
        private List<string> ConvertCommandBatchToTimeSignedKeys()
        {
            var commandKeys = new List<string>();
            var tempQueue = new Queue<Command>();
            
            // Process all commands in the batch
            while (m_CommandBatch.Count > 0)
            {
                var command = m_CommandBatch.Dequeue();
                commandKeys.Add(command.GetTimeSignedKey());
                tempQueue.Enqueue(command); // Keep a copy in case of failure
            }
            
            // Restore the commands to the queue for retry if needed
            m_CommandBatch = new Queue<Command>(tempQueue);
            
            return commandKeys;
        }

        private async Task CallCloudCodeEndpoint(List<string> timeSignedKeys)
        {
            try
            {
                var commandReward = await m_BindingsProvider.GemHunterBindings.ProcessAreaCompleteCommandBatch(timeSignedKeys);
                AreaCompleteRewardsReceived?.Invoke(commandReward);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error calling cloud endpoint: {e.Message}");
                throw;
            }
        }
        
        public void ClearForAccountDeletion()
        {
            Logger.LogDemo($"Clearing {m_CommandBatch.Count} commands for account deletion");
            
            m_CommandBatch.Clear();
            m_LocalStorageSystem.ClearCommands();
            
            // Prevent saving on dispose
            m_ShouldSaveOnDispose = false;
            
            Logger.LogDemo("Command batch cleared for account deletion");
        }
        
        public void Dispose()
        {
            // Save commands before disposing (unless account is being deleted)
            if (m_ShouldSaveOnDispose && m_CommandBatch.Count > 0)
            {
                m_LocalStorageSystem.SaveCommands(m_CommandBatch);
            }
            m_AreaManager.AreaCompleteLocal -= AreaComplete;
        }
    }
}
