using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace LootBoxes
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
}
