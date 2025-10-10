using System;
using System.Collections.Generic;
using System.Linq;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using GemHunterUGS.Scripts.PlayerHub;
using Unity.Properties;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Friends
{
    /// <summary>
    /// Manages the UI and logic for the friends menu in the game. This includes displaying lists of players and friends,
    /// handling friend interactions (adding/removing friends, gifting hearts), and updating the UI based on player data
    /// and economy changes. The class also provides search functionality to filter players and friends by name.
    /// </summary>
    public class FriendsMenuUIController : MonoBehaviour
    {
        [SerializeField] private HubUIController m_HubUIController;
        [SerializeField] private FriendsMenuView m_FriendsMenuView;
        [SerializeField] private FriendsClient m_FriendsClient;
        [SerializeField] private FriendsManager m_FriendsManager;
        [SerializeField] private RandomProfilePicturesSO m_PremadePortraits;
        
        [SerializeField] private Sprite m_ActiveTabButtonSprite;
        [SerializeField] private Sprite m_DisabledTabButtonSprite;
        
        private PlayerDataManager m_PlayerDataManager;
        private PlayerEconomyManager m_PlayerEconomyManager;
        
        private bool m_IsAllPlayersListInitialized = false;
        private bool m_IsFriendsListInitialized = false;
        private string m_DefaultSearchMessage = "Search Players";
        
        private HashSet<Button> m_DisabledHeartButtons = new HashSet<Button>();
        
        private bool IsAllPlayersTabActive => m_FriendsMenuView.AllPlayersListView.style.display == DisplayStyle.Flex;
        
        public event Action<Player> FriendAdded;
        public event Action<Player> FriendRemoved;
        public event Action<string> GiftingHeart;
        
        private void Start()
        {
            InitializeDependencies();
            SetupEventHandlers();
            SetupSearchField();
        }

        private void InitializeDependencies()
        {
            m_PlayerEconomyManager = GameSystemLocator.Get<PlayerEconomyManager>();
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();

            if (m_PlayerDataManager == null)
            {
                Logger.LogError("PlayerDataManager not initialized");
                return;
            }
            
            if (m_FriendsClient == null) m_FriendsClient = GetComponent<FriendsClient>();
            if (m_FriendsMenuView == null) m_FriendsMenuView = GetComponent<FriendsMenuView>();
            m_FriendsMenuView.Initialize(m_PlayerDataManager.PlayerDataLocal, m_PlayerEconomyManager.PlayerEconomyDataLocal);
        }

        private void SetupEventHandlers()
        {
            if (m_PlayerDataManager == null)
            {
                Logger.LogError("PlayerDataManager not initialized");
                return;
            }
            
            m_PlayerDataManager.LocalPlayerDataUpdated += UpdateTopBarStats;
            m_PlayerDataManager.NoGiftHeartsLeftPopup += ShowNoGiftHeartsLeftPopup;
            m_PlayerEconomyManager.LocalEconomyDataUpdated += UpdateTopBarCurrency;
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated += m_FriendsMenuView.ShowInfinityHearts;
            
            m_FriendsManager.AllPlayersListUpdated += HandlePlayerListUpdate;
            m_FriendsManager.FriendsListUpdated += HandleFriendsListUpdate;
            m_FriendsClient.HeartGiftGiven += ReEnableHeartButtons;
            m_FriendsClient.FriendsMenuDataInitialized += ShowAllPlayerTab;
            
            m_FriendsMenuView.PopUp_TimedButton.clicked += m_FriendsMenuView.HidePopUp;
            m_FriendsMenuView.AllPlayerTabButton.clicked += ShowAllPlayerTab;
            m_FriendsMenuView.FriendsTabButton.clicked += ShowFriendsTab;
            m_FriendsMenuView.CloseMenuButton.clicked += HideFriendsMenu;
        }

        private void SetupSearchField()
        {
            if (m_FriendsMenuView.PlayerSearchField == null)
            {
                Logger.LogError("FriendMenu PlayerSearchField not initialized");
                return;
            }
            
            m_FriendsMenuView.PlayerSearchField.RegisterValueChangedCallback(HandleSearchValueChanged);
            
            m_FriendsMenuView.PlayerSearchField.value = m_DefaultSearchMessage;
            m_FriendsMenuView.PlayerSearchField.RegisterCallback<FocusInEvent>(evt => 
            {
                if (m_FriendsMenuView.PlayerSearchField.value == m_DefaultSearchMessage)
                    m_FriendsMenuView.PlayerSearchField.value = string.Empty;
            });
    
            m_FriendsMenuView.PlayerSearchField.RegisterCallback<FocusOutEvent>(evt => 
            {
                if (string.IsNullOrWhiteSpace(m_FriendsMenuView.PlayerSearchField.value))
                    m_FriendsMenuView.PlayerSearchField.value = m_DefaultSearchMessage;
            });
            
            m_FriendsMenuView.DisableSearchGiftHeartButton();
        }

        private void HandlePlayerListUpdate(List<Player> players)
        {
            if (players == null) return;
            
            if (!m_IsAllPlayersListInitialized)
            {
                SetupAllPlayersListView(players);
                m_IsAllPlayersListInitialized = true;
            }
            else
            {
                // Create a new list to force the ListView to recognize the change
                m_FriendsMenuView.AllPlayersListView.itemsSource = new List<Player>(players);
                m_FriendsMenuView.AllPlayersListView.itemsSource = players;
            }
            
            m_FriendsMenuView.AllPlayersListView.Rebuild();
        }

        private void HandleFriendsListUpdate(List<Player> friends)
        {
            friends ??= new List<Player>();
            // Logger.Log($"Handling friends list update with {friends.Count} friends");
            
            if (!m_IsFriendsListInitialized)
            {
                SetupFriendsListView(friends);
                m_IsFriendsListInitialized = true;
            }
            else
            {
                m_FriendsMenuView.FriendsListView.itemsSource = friends;
                m_FriendsMenuView.FriendsListView.Rebuild();
            }
            
            if (friends.Count <= 0 && m_FriendsMenuView.FriendsListView.style.display == DisplayStyle.Flex)
            {
                m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.None;
                m_FriendsMenuView.ShowNoFriendsNotice();
            }
            else
            {
                m_FriendsMenuView.HideNoFriendsNotice();
            }
        }
        
        private void SetupAllPlayersListView(List<Player> players)
        {
            var listView = m_FriendsMenuView.AllPlayersListView;
            SetupListViewBase(listView, players);
    
            listView.bindItem = (element, index) =>
            {
                var currentPlayers = listView.itemsSource as List<Player>;
                if (currentPlayers == null || index >= currentPlayers.Count) return;
        
                BindPlayerItemUI(element, currentPlayers[index], false);
            };
        }
        
        private void SetupFriendsListView(List<Player> friends)
        {
            var listView = m_FriendsMenuView.FriendsListView;
            SetupListViewBase(listView, friends);  
    
            listView.bindItem = (element, index) =>
            {
                var currentFriends = listView.itemsSource as List<Player>;
                if (currentFriends == null || index >= currentFriends.Count) return;
        
                BindPlayerItemUI(element, currentFriends[index], true); 
            };
        }
        
        private void SetupListViewBase(ListView listView, List<Player> items)
        {
            listView.itemsSource = items;
            listView.makeItem = () => listView.itemTemplate.Instantiate();
            listView.fixedItemHeight = 200;
            listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
        }
        
        private void BindPlayerItemUI(VisualElement element, Player player, bool isFriendsList)
        {
            if (player?.PlayerPortrait == null) return;

            var playerPortrait = element.Q<VisualElement>("PlayerPortrait");
            playerPortrait.dataSource = player.PlayerPortrait;

            SetupPortraitBackground(playerPortrait, player.PlayerPortrait);
            BindPlayerLabels(element, player);
    
            // Setup appropriate buttons based on list type
            if (isFriendsList)
            {
                SetupFriendListViewButtons(element, player);
            }
            else
            {
                SetupListViewButtons(element, player);
            }
        }
        
        private void SetupPortraitBackground(VisualElement playerPortrait, ProfilePicture portrait)
        {
            if (portrait.Type == "custom")
            {
                var texture = CustomPortraitTextureCache.GetTexture(portrait.ImageData);
                if (texture == null)
                {
                    playerPortrait.style.backgroundImage = new StyleBackground(m_PremadePortraits.ProfilePictures[0]);
                }
                playerPortrait.style.backgroundImage = new StyleBackground(texture);
            }
            else
            {
                var binding = new DataBinding()
                {
                    dataSource = portrait,
                    dataSourcePath = new PropertyPath("ImageId")
                };

                binding.sourceToUiConverters.AddConverter((ref int imageId) => 
                    new StyleBackground(m_PremadePortraits.ProfilePictures[imageId]));

                playerPortrait.SetBinding("style.backgroundImage", binding);
            }
        }
        
        private void SetupListViewButtons(VisualElement element, Player player)
        {
            // Gift heart button
            var giveHeartButton = element.Q<Button>("GiveHeartButton");
            if (giveHeartButton != null)
            {
                // Remove existing handlers and set new one
                giveHeartButton.clickable = new Clickable(() => HandleGiftHeartButtonClicked(giveHeartButton, player));
            }
            
            // Add friend button
            var addFriendButton = element.Q<Button>("AddFriendButton");
            if (addFriendButton != null)
            {
                addFriendButton.clickable = new Clickable(() => HandleAddFriendClicked(player));
            }
        }

        private void SetupFriendListViewButtons(VisualElement element, Player friend)
        {
            var giveHeartButton = element.Q<Button>("GiveHeartButton");
            if (giveHeartButton != null)
            {
                giveHeartButton.clickable = new Clickable(() => HandleGiftHeartButtonClicked(giveHeartButton, friend));
            }

            var removeFriendButton = element.Q<Button>("RemoveFriendButton");
            if (removeFriendButton != null)
            {
                removeFriendButton.clickable = new Clickable(() => HandleRemoveFriendClicked(friend));
            }
        }

        private void BindPlayerLabels(VisualElement element, Player player)
        {
            // Bind name label
            var nameLabel = element.Q<Label>("PlayerNameLabel");
            nameLabel.SetBinding("text", new DataBinding()
            {
                dataSource = player,
                dataSourcePath = new PropertyPath("DisplayName")
            });

            // Bind ID label
            var playerIDLabel = element.Q<Label>("PlayerIDLabel");
            playerIDLabel.SetBinding("text", new DataBinding()
            {
                dataSource = player,
                dataSourcePath = new PropertyPath("PlayerId")
            });
        }
        
        private void HandleGiftHeartButtonClicked(Button heartButton, Player player)
        {
            heartButton.SetEnabled(false);
            m_DisabledHeartButtons.Add(heartButton);
            var playerId = player.PlayerId;
            Logger.Log($"Giving heart to player: {player.DisplayName}");
            GiftingHeart?.Invoke(playerId);
        }
        
        private void ReEnableHeartButtons()
        {
            foreach (var button in m_DisabledHeartButtons)
            {
                button.SetEnabled(true);
            }
            m_DisabledHeartButtons.Clear();
        }

        private void ShowNoGiftHeartsLeftPopup()
        {
            m_FriendsMenuView.ShowPopUpTimed("No gift hearts left, come back tomorrow!");
        }
        
        private void HandleAddFriendClicked(Player player)
        {
            Logger.Log($"Adding friend {player.DisplayName} at player id {player.PlayerId}");
            FriendAdded?.Invoke(player);
        }

        private void HandleRemoveFriendClicked(Player player)
        {
            Logger.Log($"Handling removing friend");
            FriendRemoved?.Invoke(player);
        }

        private void UpdateTopBarStats(PlayerData playerData)
        {
            m_FriendsMenuView.SetTopBarHearts(playerData.Hearts, playerData.GiftHearts);
        }

        private void UpdateTopBarCurrency(PlayerEconomyData economyData)
        {
            m_FriendsMenuView.SetTopBarCoins(economyData.Currencies["COIN"]);
        }

        private void ShowAllPlayerTab()
        {
            m_FriendsMenuView.PlayerSearchField.value = m_DefaultSearchMessage;
            m_FriendsMenuView.AllPlayersListView.style.display = DisplayStyle.Flex;
            m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.None;
            
            m_FriendsMenuView.FriendsTabButton.style.backgroundImage = new StyleBackground(m_DisabledTabButtonSprite);
            m_FriendsMenuView.AllPlayerTabButton.style.backgroundImage = new StyleBackground(m_ActiveTabButtonSprite);
            
            m_FriendsMenuView.HideNoFriendsNotice();
            ClearSearch();
        }

        private void ShowFriendsTab()
        {
            m_FriendsMenuView.PlayerSearchField.value = m_DefaultSearchMessage;
            m_FriendsMenuView.AllPlayersListView.style.display = DisplayStyle.None;
            m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.Flex;
            
            m_FriendsMenuView.FriendsTabButton.style.backgroundImage = new StyleBackground(m_ActiveTabButtonSprite);
            m_FriendsMenuView.AllPlayerTabButton.style.backgroundImage = new StyleBackground(m_DisabledTabButtonSprite);

            if (m_FriendsManager.Friends.Count <= 0)
            {
                m_FriendsMenuView.ShowNoFriendsNotice();
                m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.None;
            }
            else
            {
                m_FriendsMenuView.HideNoFriendsNotice();
            }
            ClearSearch();
        }

        private void HandleSearchValueChanged(ChangeEvent<string> evt)
        {
            var searchTerm = evt.newValue.ToLower();
            var isDefaultSearch = string.IsNullOrWhiteSpace(searchTerm) || 
                string.Equals(searchTerm.ToLower(), m_DefaultSearchMessage.ToLower());
    
            var sourceList = IsAllPlayersTabActive ? m_FriendsManager.AllPlayers : m_FriendsManager.Friends;
            var targetListView = IsAllPlayersTabActive ? m_FriendsMenuView.AllPlayersListView : m_FriendsMenuView.FriendsListView;
    
            List<Player> filteredList;
            if (isDefaultSearch)
            {
                filteredList = sourceList;
                m_FriendsMenuView.DisableSearchGiftHeartButton();
            }
            else
            {
                filteredList = sourceList.Where(p => p.DisplayName.ToLower().Contains(searchTerm)).ToList();
                if (filteredList.Count == 1)
                {
                    m_FriendsMenuView.EnableSearchGiftHeartButton();
                    SetupSearchGiftHeartButtonEvent(filteredList[0]);
                }
                else
                {
                    m_FriendsMenuView.DisableSearchGiftHeartButton();
                }
            }
    
            targetListView.itemsSource = filteredList;
            targetListView.Rebuild();
    
            UpdateListViewVisibility(filteredList);
        }

        private void ClearSearch()
        {
            m_FriendsMenuView.PlayerSearchField.value = m_DefaultSearchMessage;
            // Create a change event
            var evt = ChangeEvent<string>.GetPooled(null, m_DefaultSearchMessage);
            HandleSearchValueChanged(evt);
        }
        
        // Handles special case: "No Friends Notice" should not be shown when the player is searching
        private void UpdateListViewVisibility(List<Player> filteredList)
        {
            // Only need to handle visibility for friends list
            if (!IsAllPlayersTabActive)
            {
                if (filteredList.Count <= 0)
                {
                    m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.None;
                    m_FriendsMenuView.ShowNoFriendsNotice();
                }
                else
                {
                    m_FriendsMenuView.FriendsListView.style.display = DisplayStyle.Flex;
                    m_FriendsMenuView.HideNoFriendsNotice();
                }
            }
        }

        private void SetupSearchGiftHeartButtonEvent(Player playerInfo)
        {
            m_FriendsMenuView.SearchGiftHeartButton.clickable = new Clickable(() => HandleGiftHeartButtonClicked(m_FriendsMenuView.SearchGiftHeartButton, playerInfo));
        }

        private void HideFriendsMenu()
        {
            m_FriendsMenuView.FriendsMenu.style.display = DisplayStyle.None;
            m_HubUIController.ShowMainHub();
        }
        
        private void OnDisable()
        {
            RemoveUIEventHandlers();
            RemoveManagerEventHandlers();
            CustomPortraitTextureCache.Clear();
        }

        private void RemoveUIEventHandlers()
        {
            if (m_FriendsMenuView == null || m_FriendsMenuView.AllPlayerTabButton == null)
            {
                return;
            }
            
            m_FriendsMenuView.AllPlayerTabButton.clicked -= ShowAllPlayerTab;
            m_FriendsMenuView.FriendsTabButton.clicked -= ShowFriendsTab;
            m_FriendsMenuView.CloseMenuButton.clicked -= HideFriendsMenu;
            m_FriendsMenuView.PopUp_TimedButton.clicked -= m_FriendsMenuView.HidePopUp;
        }

        private void RemoveManagerEventHandlers()
        {
            // Prevents unnecessary subscription errors if PlayerHub scene is loaded first
            if (m_PlayerDataManager == null)
            {
                return;
            }
            m_PlayerDataManager.LocalPlayerDataUpdated -= UpdateTopBarStats;
            m_PlayerEconomyManager.LocalEconomyDataUpdated -= UpdateTopBarCurrency;
            m_PlayerEconomyManager.InfiniteHeartStatusUpdated -= m_FriendsMenuView.ShowInfinityHearts;
            m_FriendsClient.HeartGiftGiven -= ReEnableHeartButtons;
            m_FriendsClient.FriendsMenuDataInitialized -= ShowAllPlayerTab;
            m_PlayerDataManager.NoGiftHeartsLeftPopup -= ShowNoGiftHeartsLeftPopup;
            m_FriendsManager.AllPlayersListUpdated -= HandlePlayerListUpdate;
            m_FriendsManager.FriendsListUpdated -= HandleFriendsListUpdate;
        }
    }
}
