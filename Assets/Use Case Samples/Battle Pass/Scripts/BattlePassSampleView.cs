using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace BattlePass
    {
        public class BattlePassSampleView : MonoBehaviour
        {
            public BattlePassView battlePassView;
            public Button inventoryButton;
            public InventoryPopupView inventoryPopupView;
            public TextMeshProUGUI eventWelcomeText;

            public void SetInteractable(bool interactable)
            {
                inventoryButton.interactable = interactable;

                battlePassView.SetInteractable(interactable);
            }

            public void UpdateWelcomeText()
            {
                var welcomeText = "";
            
                if (!string.IsNullOrEmpty(RemoteConfigManager.instance.activeEventName))
                {
                    welcomeText = RemoteConfigManager.instance.activeEventName;
                }
            
                eventWelcomeText.text = welcomeText;
            }

            public async void OnInventoryButtonPressed()
            {
                await inventoryPopupView.Show();
            }
        }
    }
}
