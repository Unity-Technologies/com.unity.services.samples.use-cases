using System.Collections.Generic;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace SeasonalEvents
    {
        public class RewardPopupManager : MonoBehaviour
        {
            public RewardDisplayView rewardDisplayView;

            public void Show(List<RewardDetail> rewards)
            {
                rewardDisplayView.PopulateView(rewards);
                gameObject.SetActive(true);
            }

            public async void CollectRewards()
            {
                await CloudCodeManager.instance.CallGrantEventRewardEndpoint();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                Close();
            }

            void Close()
            {
                var currentGameObject = gameObject;
                currentGameObject.SetActive(false);
                Destroy(currentGameObject);
            }
        }
    }
}
