using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GameOperationsSamples
{
    namespace DailyRewards
    {
        public class DailyRewardsSampleView : MonoBehaviour
        {
            public Button grantRandomRewardButton;

            public TextMeshProUGUI grantRandomRewardButtonText;


            public void UpdateCooldown(int seconds)
            {
                if (seconds > 0)
                {
                    grantRandomRewardButton.interactable = false;
                    grantRandomRewardButtonText.text = seconds > 1
                        ? $"... ready in {seconds} seconds."
                        : "... ready in 1 second.";
                }
                else
                {
                    grantRandomRewardButton.interactable = true;
                    grantRandomRewardButtonText.text = "Claim Daily Reward";
                }
            }

            public void OnClaimingDailyReward()
            {
                grantRandomRewardButtonText.text = "Claiming Daily Reward";
                grantRandomRewardButton.interactable = false;
            }
        }
    }
}
