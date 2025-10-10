using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GemHunterUGS.Scripts.AreaUpgradables;
using Newtonsoft.Json;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Handles local persistence of player data, economy data, and profile pictures.
    /// Provides methods to save and load JSON files to/from the application's persistent data path,
    /// with error handling and logging for debugging.
    /// </summary>
    public class LocalStorageSystem
    {
        private const string k_PlayerDataFileName = "player_data.json";
        private const string k_EconomyDataFileName = "player_economy.json";
        private const string k_ProfilePictureFileName = "profile_picture.json";
        private const string k_CommandBatchFileName = "command_batch.json";
        private const string k_SaveEmoji = "💾";
        
        /// <summary>
        /// Loads the player's saved data from local storage.
        /// Returns null if no data exists or if loading fails.
        /// </summary>
        public PlayerData LoadPlayerData()
        {
            string path = Path.Combine(Application.persistentDataPath, k_PlayerDataFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                string jsonData = File.ReadAllText(path);
                var playerData = JsonConvert.DeserializeObject<PlayerData>(jsonData);
                Logger.LogDemo($"{k_SaveEmoji} Loaded player data for player: {playerData?.DisplayName}");
                
                return playerData;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error loading local player data: {e.Message}");
                return null;
            }
        }
        
        public PlayerEconomyData LoadEconomyData()
        {
            string path = Path.Combine(Application.persistentDataPath, k_EconomyDataFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                string jsonData = File.ReadAllText(path);
                var economyData = JsonConvert.DeserializeObject<PlayerEconomyData>(jsonData);
                Logger.LogDemo($"{k_SaveEmoji} Loaded economy data with {economyData?.Currencies?.Count ?? 0} currencies and {economyData?.ItemInventory?.Count ?? 0} inventory items");
                
                return economyData;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error loading local economy data: {e.Message}");
                return null;
            }
        }
        
        public ProfilePicture LoadProfilePicture()
        {
            string path = Path.Combine(Application.persistentDataPath, k_ProfilePictureFileName);
            if (File.Exists(path))
            {
                try
                {
                    string jsonData = File.ReadAllText(path);
                    var profilePicture = JsonConvert.DeserializeObject<ProfilePicture>(jsonData);
                    Logger.LogDemo($"{k_SaveEmoji} Loaded profile picture of type: {profilePicture?.Type}");
                    
                    return profilePicture;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error loading local profile picture: {e.Message}");
                }
            }
            
            return null;
        }
        
        public void SavePlayerData(PlayerData playerData)
        {
            try
            {
                string jsonData = JsonConvert.SerializeObject(playerData);
                string path = Path.Combine(Application.persistentDataPath, k_PlayerDataFileName);
                File.WriteAllText(path, jsonData);
                
                Logger.LogDemo($"{k_SaveEmoji} PlayerData saved locally for player: {playerData.DisplayName}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error saving player data locally: {e.Message}");
            }
        }
        
        public void SaveEconomyData(PlayerEconomyData economyData)
        {
            try
            {
                string jsonData = JsonConvert.SerializeObject(economyData);
                string path = Path.Combine(Application.persistentDataPath, k_EconomyDataFileName);
                File.WriteAllText(path, jsonData);
                
                Logger.LogDemo($"{k_SaveEmoji} Economy data saved locally");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error saving economy data locally: {e.Message}");
            }
        }
        
        public void SaveProfilePicture(ProfilePicture profilePicture)
        {
            try
            {
                string jsonData = JsonConvert.SerializeObject(profilePicture);
                string path = Path.Combine(Application.persistentDataPath, k_ProfilePictureFileName);
                File.WriteAllText(path, jsonData);
                
                Logger.LogDemo($"{k_SaveEmoji} Profile picture saved locally of type: {profilePicture.Type}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error saving profile picture locally: {e.Message}");
            }
        }
        
        /// <summary>
        /// Loads saved commands from local storage
        /// </summary>
        public Queue<Command> LoadCommands()
        {
            string path = Path.Combine(Application.persistentDataPath, k_CommandBatchFileName);
            var commandQueue = new Queue<Command>();
    
            if (!File.Exists(path))
            {
                Logger.LogVerbose("No saved command batch found");
                return commandQueue;
            }
    
            try
            {
                string jsonData = File.ReadAllText(path);
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All // Use type information during deserialization
                };
                var savedCommands = JsonConvert.DeserializeObject<List<Command>>(jsonData, settings);
        
                if (savedCommands != null && savedCommands.Count > 0)
                {
                    foreach (var command in savedCommands)
                    {
                        commandQueue.Enqueue(command);
                    }
            
                    Logger.LogDemo($"{k_SaveEmoji} Loaded {savedCommands.Count} saved commands from local storage");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error loading command batch from local storage: {e.Message}");
            }
    
            return commandQueue;
        }
        
        /// <summary>
        /// Saves commands to local storage
        /// </summary>
        public void SaveCommands(Queue<Command> commands)
        {
            if (commands == null || commands.Count == 0)
            {
                Logger.LogVerbose("No commands to save");
                return;
            }
    
            try
            {
                var commandList = commands.ToList();
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All // Include type information
                };
                string jsonData = JsonConvert.SerializeObject(commandList, settings);
        
                string path = Path.Combine(Application.persistentDataPath, k_CommandBatchFileName);
                File.WriteAllText(path, jsonData);
        
                Logger.LogDemo($"{k_SaveEmoji} Saved {commandList.Count} commands to local storage");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error saving command batch to local storage: {e.Message}");
            }
        }
        
        /// <summary>
        /// Clears the saved commands from local storage
        /// </summary>
        public void ClearCommands()
        {
            DeleteFile(k_CommandBatchFileName);
            Logger.LogDemo("Cleared saved commands from local storage");
        }
        
        public void DeleteLocalData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            
            DeleteFile(k_PlayerDataFileName);
            DeleteFile(k_EconomyDataFileName);
            DeleteFile(k_ProfilePictureFileName);
            DeleteFile(k_CommandBatchFileName);
            
            Logger.LogDemo("All local data deleted");
        }
        
        private void DeleteFile(string fileName)
        {
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                File.Delete(path);
                
                Logger.LogVerbose($"Deleted local file: {fileName}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error deleting {fileName}: {e.Message}");
            }
        }
    }
}

