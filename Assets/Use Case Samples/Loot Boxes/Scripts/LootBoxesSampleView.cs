using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace LootBoxes
    {
        public class LootBoxesSampleView : MonoBehaviour
        {
            public Button grantRandomRewardButton;


            public void Enable()
            {
                grantRandomRewardButton.interactable = true;
            }

            public void Disable()
            {
                grantRandomRewardButton.interactable = false;
            }
        }
    }
}
