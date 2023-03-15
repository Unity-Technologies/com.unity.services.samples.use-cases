using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.LootBoxes
{
    public class LootBoxesSampleView : MonoBehaviour
    {
        public Button grantRandomRewardButton;

        public void SetInteractable(bool isInteractable = true)
        {
            grantRandomRewardButton.interactable = isInteractable;
        }
    }
}
