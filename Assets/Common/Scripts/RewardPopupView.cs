using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples
{
    public class RewardPopupView : MonoBehaviour
    {
        public TextMeshProUGUI headerText;

        public RewardDisplayView rewardDisplayView;

        public Button closeButton;

        public TextMeshProUGUI closeButtonText;

        public void Show(List<RewardDetail> rewards)
        {
            rewardDisplayView.PopulateView(rewards);
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
