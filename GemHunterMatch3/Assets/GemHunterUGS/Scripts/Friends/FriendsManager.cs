using System;
using System.Collections.Generic;
using System.Linq;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using Unity.Services.Authentication;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Friends
{
    /// <summary>
    /// Manages friend relationships and player interactions in the game.
    /// Handles friend list updates, player filtering, and heart gifting functionality.
    /// </summary>
    public class FriendsManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private FriendsClient m_FriendsClient;
        [SerializeField] private FriendsMenuUIController m_FriendsMenuUIController;
        
        private PlayerDataManager m_PlayerDataManager;
        
        // Player Lists
        public List<Player> Friends { get; private set; }
        public List<Player> AllPlayers => FilteredPlayerList();
        private List<Player> m_AllPlayers;
        
        public event Action<List<Player>> AllPlayersListUpdated;
        public event Action<List<Player>> FriendsListUpdated;

        private void Start()
        {
            SetupEventHandlers();
            
            m_AllPlayers = new List<Player>();
            Friends = new List<Player>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
        }

        private void SetupEventHandlers()
        {
            m_FriendsClient.FetchedPlayerListFromCloud += UpdateAllPlayers;
            m_FriendsClient.FetchedFriendsListFromCloud += UpdateFriendsList;
            
            m_FriendsMenuUIController.FriendAdded += AddFriendLocally;
            m_FriendsMenuUIController.FriendRemoved += RemoveFriendLocal;
            m_FriendsMenuUIController.GiftingHeart += HandleGiftHeart;
        }

        private void UpdateAllPlayers(List<Player> players)
        {
            if (players == null)
            {
                Logger.LogWarning("No players found");
                return;
            }
            if (players.Count == 0) return;
            
            m_AllPlayers = players;
            var filteredList = FilteredPlayerList();
            
            AllPlayersListUpdated?.Invoke(filteredList);
        }

        private void UpdateFriendsList(List<Player> friends)
        {
            Friends = friends ?? new List<Player>();
            Logger.Log($"\u26A1 Local FriendsListUpdated with {Friends.Count} friends");
            
            // Update both lists since friend status affects filtered players
            AllPlayersListUpdated?.Invoke(FilteredPlayerList());
            FriendsListUpdated?.Invoke(Friends);
        }
        
        private List<Player> FilteredPlayerList()
        {
            var currentPlayerId = AuthenticationService.Instance.PlayerId;
            var filteredPlayerList = m_AllPlayers
                .Where(player => 
                    !IsCurrentPlayer(player, currentPlayerId) && 
                    !IsFriend(player))
                .ToList();
            
            // Logger.Log($"Filtered all players list count: {filteredPlayerList.Count}");
            // foreach(var player in filteredPlayerList)
            // {
            //     Logger.Log($"Player in list: {player.DisplayName} ({player.PlayerId})");
            // }
            
            return filteredPlayerList;
        }

        private bool IsCurrentPlayer(Player player, string currentPlayerId)
        {
            return player.PlayerId == currentPlayerId;
        }

        private bool IsFriend(Player player)
        {
            if (Friends == null)
                return false;
        
            return Friends.Any(friend => friend.PlayerId == player.PlayerId);
        }
        
        private void AddFriendLocally(Player otherPlayer)
        {
            if (Friends.Any(f => f.PlayerId == otherPlayer.PlayerId))
            {
                Logger.LogWarning($"Player {otherPlayer.DisplayName} is already a friend");
                return;
            }
            
            Logger.Log($"Adding friend locally: {otherPlayer.DisplayName}");
            Friends.Add(otherPlayer);
            
            // Remove from all players list
            // m_AllPlayers = m_AllPlayers.Where(p => p.PlayerId != player.PlayerId).ToList();
            
            var filteredList = FilteredPlayerList();
            Logger.Log($"After adding friend - All players count: {m_AllPlayers.Count}, Filtered count: {filteredList.Count}");
            
            AllPlayersListUpdated?.Invoke(filteredList);
            FriendsListUpdated?.Invoke(Friends);
        }

        private void RemoveFriendLocal(Player otherPlayer)
        {
            Logger.Log("Removing friend...");
            Friends.Remove(otherPlayer);
            AllPlayersListUpdated?.Invoke(FilteredPlayerList());
            FriendsListUpdated?.Invoke(Friends);
        }

        private void HandleGiftHeart(string playerId)
        {
            if (!IsGiftDataValid(playerId))
            {
                Logger.LogWarning($"Player {playerId} is not valid for data");
                return;
            }
            
            if (m_PlayerDataManager.ModifyGiftHearts(-1))
            {
                Logger.Log($"Local Gift Heart deducted -1 to gift playerId: {playerId} a heart");
            }
        }

        /// <summary>
        /// Validates if a player can receive a gift heart
        /// </summary>
        /// <returns>True if the player can receive a gift, false otherwise</returns>
        private bool IsGiftDataValid(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.LogWarning("Player ID is empty");
                return false;
            }

            if (m_AllPlayers.Find(p => p.PlayerId == playerId) == null)
            {
                Logger.LogWarning($"Player {playerId} does not exist");
                return false;
            }

            if (playerId == AuthenticationService.Instance.PlayerId)
            {
                Logger.LogWarning($"Player cannot send gift to self");
                return false;
            }
            
            return true;
        }
    
        private void OnDestroy()
        {
            if (m_FriendsClient != null)
            {
                m_FriendsClient.FetchedPlayerListFromCloud -= UpdateAllPlayers;
                m_FriendsClient.FetchedFriendsListFromCloud -= UpdateFriendsList;
            }
    
            m_AllPlayers?.Clear();
            Friends?.Clear();
        }
    }
}
