using System.Collections;
using System.Collections.Generic;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Unity.Properties;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
namespace GemHunterUGS.Scripts.Friends
{
    public class FriendsMenuView : MonoBehaviour
    {
        [SerializeField] private UIDocument m_UIDocument;

        public VisualElement FriendsMenu { get; set; }
        private VisualElement m_TopBar;
        private VisualElement m_NoFriendsContainer;
        public TextField PlayerSearchField { get; private set; }

        public Button AllPlayerTabButton { get; private set; }
        public Button FriendsTabButton { get; private set; }
        
        public Button CloseMenuButton { get; private set; }
        public Button FriendMenuInfoButton { get; private set; }
        
        public Button SearchGiftHeartButton { get; private set; }
        
        private VisualElement m_PopUp_Timed;
        public Button PopUp_TimedButton { get; private set; }
        
        private Label m_CoinsLabel;
        private Label m_HeartLabel;
        private Label m_InfinityHeartLabel;
        
        public ListView FriendsListView;
        public ListView AllPlayersListView;
        
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public void Initialize(PlayerData playerData, PlayerEconomyData playerEconomy)
        {
            FriendsMenu = m_UIDocument.rootVisualElement.Q("FriendsMenu");
            var root = m_UIDocument.rootVisualElement;
            m_TopBar = FriendsMenu.Q<VisualElement>("TopBar");
            
            PlayerSearchField = FriendsMenu.Q<TextField>("PlayerSearchField");
            
            AllPlayerTabButton = FriendsMenu.Q<Button>("AllPlayersButton");
            FriendsTabButton = FriendsMenu.Q<Button>("FriendsButton");
            m_NoFriendsContainer = FriendsMenu.Q<VisualElement>("NoFriendsContainer");
            
            m_CoinsLabel = m_TopBar.Q<Label>("CoinsLabel");
            m_HeartLabel = m_TopBar.Q<Label>("HeartLabel");
            m_InfinityHeartLabel = m_TopBar.Q<Label>("InfinityHeartLabel");
            CloseMenuButton = m_TopBar.Q<Button>("CloseFriendsMenuButton");
            FriendMenuInfoButton = m_TopBar.Q<Button>("FriendMenuInfoButton");
            
            SearchGiftHeartButton = FriendsMenu.Q<Button>("SearchGiftHeartButton");
            
            m_PopUp_Timed = root.Q<VisualElement>("PopUp_Timed");
            PopUp_TimedButton = m_PopUp_Timed.Q<Button>("PopUpButton");
            
            FriendsListView = FriendsMenu.Q<ListView>("FriendsList");
            AllPlayersListView = FriendsMenu.Q<ListView>("AllPlayersList");
            
            SetupBindings(playerData, playerEconomy);
        }
        
        private void SetupBindings(PlayerData playerData, PlayerEconomyData economyData)
        {
            var coinBinding = new DataBinding()
            {
                dataSource = economyData,
                dataSourcePath = new PropertyPath("Currencies"),
                bindingMode = BindingMode.ToTarget
            };
            
            coinBinding.sourceToUiConverters.AddConverter((ref Dictionary<string,int> currencies) => 
                currencies[PlayerEconomyManager.k_Coin].ToString());
            
            m_CoinsLabel.SetBinding("text", coinBinding);
            
            m_HeartLabel.dataSource = playerData;
            m_HeartLabel.SetBinding("text", new DataBinding()
            {
                dataSourcePath = new PropertyPath("Hearts")
            });
        }

        public void ShowNoFriendsNotice()
        {
            m_NoFriendsContainer.style.display = DisplayStyle.Flex;
        }

        public void HideNoFriendsNotice()
        {
            m_NoFriendsContainer.style.display = DisplayStyle.None;
        }

        public void SetTopBarHearts(int hearts, int giftHearts)
        {
            m_HeartLabel.text = hearts.ToString();
            SearchGiftHeartButton.text = giftHearts.ToString() + " Hearts";
        }

        public void DisableSearchGiftHeartButton()
        {
            SearchGiftHeartButton.SetEnabled(false);
            SearchGiftHeartButton.clickable = null;
        }
        
        public void EnableSearchGiftHeartButton()
        {
            SearchGiftHeartButton.SetEnabled(true);
        }
        
        public void SetTopBarCoins(int coins)
        {
            m_CoinsLabel.text = coins.ToString();
        }
        
        public void ShowInfinityHearts(bool visible)
        {
            m_InfinityHeartLabel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        public void ShowPopUpTimed(string text)
        {
            m_PopUp_Timed.style.display = DisplayStyle.Flex;
            PopUp_TimedButton.text = text;
            StartCoroutine(HidePopUpAfterWait());
        }
        
        private IEnumerator HidePopUpAfterWait()
        {
            yield return new WaitForSeconds(1.35f);
            HidePopUp();
        }

        public void HidePopUp()
        {
            PopUp_TimedButton.text = string.Empty;
            m_PopUp_Timed.style.display = DisplayStyle.None;
        }
    }
}
