using System.Collections.Generic;
using UnityEngine;

namespace SeasonalEvents
{
    public class RewardPopupManager : MonoBehaviour
    {
        public RewardDisplayView view;

        public void Show(List<RewardDetail> rewards)
        {
            view.PopulateView(rewards);
            gameObject.SetActive(true);
        }

        public async void CollectRewards()
        {
            await CloudCodeManager.instance.CallGrantEventRewardEndpoint();
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
