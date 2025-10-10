using System;
using System.Collections;
using System.Collections.Generic;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using Unity.Services.Authentication;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Friends
{
    /// <summary>
    /// Handles communication with the cloud services for friend-related operations in the game.
    /// This includes fetching player and friend lists, managing friend additions/removals, and sending/receiving gifts.
    /// The class also interacts with the <see cref="FriendsMenuUIController"/> to update the UI based on cloud data.
    /// </summary>
    public class FriendsClient : MonoBehaviour
    {
        [SerializeField]
        private FriendsMenuUIController m_FriendsMenuUIController;
        
        private GameManagerUGS m_GameManagerUGS;
        private CloudBindingsProvider m_BindingsProvider;
        private PlayerDataManagerClient m_PlayerDataManagerClient;
        private PlayerDataManager m_PlayerDataManager;

        private List<Player> m_AllPlayers;
        private List<Player> m_Friends;

        private bool m_SubscribedToInitialization;

        public event Action<List<Player>> FetchedPlayerListFromCloud;
        public event Action<List<Player>> FetchedFriendsListFromCloud;
        public event Action HeartGiftGiven;
        public event Action FriendsMenuDataInitialized;

        private const string k_FriendsEmoji = "👫";
        private const string k_GiftHeartEmoji = "💌";
        
        private void Start()
        {
            InitializeDependencies();
            SetupEventHandlers();

            if (m_PlayerDataManagerClient.IsPlayerInitializedInCloud)
            {
                StartCoroutine(DelayedInitialization());
            }
            else
            {
                m_PlayerDataManagerClient.PlayerInitialized += InitializeFriendsMenuData;
                m_SubscribedToInitialization = true;
            }
        }

        private IEnumerator DelayedInitialization()
        {
            yield return new WaitForEndOfFrame();
            InitializeFriendsMenuData();
        }

        private void InitializeDependencies()
        {
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
            m_PlayerDataManagerClient = GameSystemLocator.Get<PlayerDataManagerClient>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            
            if (m_FriendsMenuUIController == null)
            {
                m_FriendsMenuUIController = GetComponent<FriendsMenuUIController>();
            }
        }

        private void SetupEventHandlers()
        {
            m_FriendsMenuUIController.FriendAdded += AddFriend;
            m_FriendsMenuUIController.FriendRemoved += RemoveFriend;
            m_FriendsMenuUIController.GiftingHeart += SendPlayerGift;
        }

        private void InitializeFriendsMenuData()
        {
            GetPlayersInfo();
            GetFriends();
            GetGiftsFromOtherPlayers();
            FriendsMenuDataInitialized?.Invoke();
        }
        
        /// <summary>
        /// Fetches the list of all players from the cloud and updates the local cache.
        /// Raises the <see cref="FetchedPlayerListFromCloud"/> event upon successful retrieval.
        /// </summary>
        private async void GetPlayersInfo()
        {
            try
            {
                Logger.LogDemo($"{k_FriendsEmoji} Getting all player info...");
                m_AllPlayers = await m_BindingsProvider.GemHunterBindings.GetPlayerList();
                Logger.LogDemo($"\u2601 {k_FriendsEmoji} Fetched info on {m_AllPlayers.Count} players");
                FetchedPlayerListFromCloud?.Invoke(m_AllPlayers);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private async void GetFriends()
        {
            try
            {
                Logger.LogDemo($"{k_FriendsEmoji} Getting all friend info...");
                m_Friends = await m_BindingsProvider.GemHunterBindings.GetFriends();
                Logger.LogDemo($"\u2601 {k_FriendsEmoji} Fetched info on {m_Friends.Count} friends");
                FetchedFriendsListFromCloud?.Invoke(m_Friends);
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private async void GetGiftsFromOtherPlayers()
        {
            var playerData = await m_BindingsProvider.GemHunterBindings.CheckReceivedGifts();
            if (playerData != null)
            {
                var heartsReceived = playerData.Hearts - m_PlayerDataManager.PlayerDataLocal.Hearts;
                Logger.LogDemo($"\u2601 {k_GiftHeartEmoji} Received {heartsReceived} heart gifts from other players");
                m_PlayerDataManagerClient.HandleCloudDataUpdate(playerData);
            }
            else
            {
                Logger.LogDemo($"No gifts from other players :(");
            }
        }

        private async void AddFriend(Player player)
        {
            try
            {
                var updatedFriends = await m_BindingsProvider.GemHunterBindings.AddFriend(player.PlayerId);
                Logger.LogDemo($"{k_FriendsEmoji} Added friend to list of {updatedFriends.Count} friends");
                FetchedFriendsListFromCloud?.Invoke(updatedFriends);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to add friend: {e.Message}");
            }
        }

        private async void RemoveFriend(Player player)
        {
            try
            {
                var updatedFriends = await m_BindingsProvider.GemHunterBindings.RemoveFriend(player.PlayerId);
                FetchedFriendsListFromCloud?.Invoke(updatedFriends);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to remove friend: {e.Message}");
            }
        }

        private async void SendPlayerGift(string playerId)
        {
            if (!IsGiftDataValid(playerId))
            {
                Logger.LogWarning($"Player {playerId} is not valid for data");
                return;
            }
            if (!m_PlayerDataManagerClient.CanModifyGiftHearts(-1))
            {
                Logger.LogWarning($"{k_GiftHeartEmoji} No gift hearts left");
                return;
            }
            
            var updatedPlayerData = await m_BindingsProvider.GemHunterBindings.HandleSendPlayerGift(playerId);
            if (updatedPlayerData == null)
            {
                Logger.Log("Can't send player gift");
                return;
            }
            m_PlayerDataManagerClient.HandleCloudDataUpdate(updatedPlayerData);
            HeartGiftGiven?.Invoke();
        }
        
        public void RefreshFriendsList()
        {
            GetFriends();
        }
        
        private bool IsGiftDataValid(string playerId)
        {
            if (string.IsNullOrEmpty(playerId))
            {
                Logger.LogWarning("Player ID is empty");
                return false;
            }

            if (playerId == AuthenticationService.Instance.PlayerId)
            {
                Logger.LogWarning($"Player cannot send gift to self");
                return false;
            }
            
            return true;
        }
        
        private void OnDisable()
        {
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            if (m_PlayerDataManagerClient != null && m_SubscribedToInitialization)
            {
                m_PlayerDataManagerClient.PlayerInitialized -= InitializeFriendsMenuData;    
            }

            if (m_FriendsMenuUIController != null)
            {
                m_FriendsMenuUIController.FriendAdded -= AddFriend;
                m_FriendsMenuUIController.FriendRemoved -= RemoveFriend;
                m_FriendsMenuUIController.GiftingHeart -= SendPlayerGift;
            }
        }
    }
}