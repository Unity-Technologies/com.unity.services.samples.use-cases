using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.LootBoxesWithCooldown
{
    public class LootBoxesWithCooldownSampleView : MonoBehaviour
    {
        public Button claimLootBoxButton;

        public TextMeshProUGUI claimLootBoxButtonText;

        public void UpdateCooldown(int seconds)
        {
            if (seconds > 0)
            {
                claimLootBoxButton.interactable = false;
                claimLootBoxButtonText.text = seconds > 1
                    ? $"... ready in {seconds} seconds."
                    : "... ready in 1 second.";
            }
            else
            {
                claimLootBoxButton.interactable = true;
                claimLootBoxButtonText.text = "Open Loot Box";
            }
        }

        public void OnClaimingLootBoxes()
        {
            claimLootBoxButtonText.text = "Opening Loot Box";
            claimLootBoxButton.interactable = false;
        }
    }
}
