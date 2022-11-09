using UnityEngine;

namespace Unity.Services.Samples.RewardedAds
{
    public class RewardedAdBoosterArrowManager : MonoBehaviour
    {
        public RewardedAdsSceneManager sceneManager;
        public Animator animator;


        // This callback is triggered by the Arrow_swing animation at the keyframes where the arrow moves
        // from one wedge of the rewarded ad booster to the next.
        public void OnRewardedAdBoosterActiveWedgeChanged(
            RewardedAdsSceneManager.RewardedAdBoosterWedge newActiveWedge)
        {
            sceneManager.ChangeRewardedAdBoosterMultiplier(newActiveWedge);
        }

        public void Stop()
        {
            animator.speed = 0;
        }

        public void Start()
        {
            animator.speed = 1;
        }
    }
}
