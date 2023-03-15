using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.BattlePass
{
    public class BattlePassSampleView : MonoBehaviour
    {
        public BattlePassView battlePassView;
        public Button inventoryButton;
        public Button gainGemsButton;
        public MessagePopup messagePopup;
        public InventoryPopupView inventoryPopupView;
        public TextMeshProUGUI eventWelcomeText;

        public void SetInteractable(bool isInteractable)
        {
            inventoryButton.interactable = isInteractable;
            gainGemsButton.interactable = isInteractable;

            battlePassView.SetInteractable(isInteractable);
        }

        public void UpdateWelcomeText(string newWelcomeText)
        {
            eventWelcomeText.text = newWelcomeText;
        }

        public async void OnInventoryButtonPressed()
        {
            await inventoryPopupView.Show();
        }

        public void ShowCantAffordBattlePassPopup()
        {
            messagePopup.Show("Not Enough Gems", "You don't have enough gems to be able to afford this season's Battle Pass!");
        }
    }
}
