using System.Collections.Generic;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    public class RewardDisplayView : MonoBehaviour
    {
        public RewardItemView rewardPrefab;

        public void PopulateView(List<RewardDetail> rewards)
        {
            ClearView();

            foreach (var reward in rewards)
            {
                var rewardItemView = InstantiateRewardItem();
                rewardItemView.SetQuantity(reward.quantity);

                if (reward.sprite != null)
                {
                    rewardItemView.SetIcon(reward.sprite);
                }
                else if (!string.IsNullOrEmpty(reward.spriteAddress))
                {
                    rewardItemView.LoadIconFromAddress(reward.spriteAddress);
                }
            }
        }

        void ClearView()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        RewardItemView InstantiateRewardItem()
        {
            var rewardItem = Instantiate(rewardPrefab, transform, false);
            rewardItem.transform.localScale = Vector3.one;
            rewardItem.gameObject.SetActive(false);

            return rewardItem;
        }
    }
}
