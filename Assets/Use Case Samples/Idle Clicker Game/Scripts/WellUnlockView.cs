using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class WellUnlockView : MonoBehaviour
    {
        public string unlockKey;
        public TextMeshProUGUI wellLockedStatusText;
        public Slider wellLockedProgressSlider;
        public GameObject lockedState;
        public string wellUnlockedText;

        public void ShowStatus()
        {
            var unlockCountRequired = UnlockManager.unlockCountRequired;

            wellLockedProgressSlider.maxValue = unlockCountRequired;
            wellLockedProgressSlider.value = unlockCountRequired;

            lockedState.SetActive(false);

            if (!string.IsNullOrEmpty(unlockKey))
            {
                var unlockedCount = UnlockManager.instance.GetUnlockedCount(unlockKey);

                if (unlockedCount < unlockCountRequired)
                {
                    wellLockedStatusText.text = $"{unlockedCount}/{unlockCountRequired}";

                    wellLockedProgressSlider.value = unlockedCount;

                    lockedState.SetActive(true);
                }
                else
                {
                    wellLockedStatusText.text = wellUnlockedText;
                }
            }
        }
    }
}
