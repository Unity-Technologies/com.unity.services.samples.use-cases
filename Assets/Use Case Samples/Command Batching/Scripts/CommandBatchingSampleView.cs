using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace CommandBatching
    {
        public class CommandBatchingSampleView : MonoBehaviour
        {
            public GameObject gameOverPopupView;

            [Space] public TextMeshProUGUI xpValueText;
            public TextMeshProUGUI remainingTurnsCountText;
            public TextMeshProUGUI goalsAchievedValueText;

            [Space] public Button defeatRedEnemyButton;
            public Button defeatBlueEnemyButton;
            public Button openChestButton;
            public Button achieveBonusGoalButton;
            public Button gameOverPlayAgainButton;
            public GameObject gameOverButtonLoadingOverlay;

            public void InitializeScene()
            {
                SetInteractable(false);
            }

            public void SetInteractable(bool interactable)
            {
                defeatRedEnemyButton.interactable = interactable;
                defeatBlueEnemyButton.interactable = interactable;
                openChestButton.interactable = interactable && GameStateManager.instance.isOpenChestValidMove;
                achieveBonusGoalButton.interactable =
                    interactable && GameStateManager.instance.isAchieveBonusGoalValidMove;
            }

            public void BeginGame()
            {
                SetInteractable(true);
            }

            public void GameOver()
            {
                SetInteractable(false);
                gameOverPopupView.SetActive(true);
            }

            public void ShowGameOverPlayAgainButton()
            {
                gameOverPlayAgainButton.gameObject.SetActive(true);
                gameOverButtonLoadingOverlay.SetActive(false);
            }

            public void CloseGameOverPopup()
            {
                gameOverPopupView.SetActive(false);
                gameOverButtonLoadingOverlay.SetActive(true);
                gameOverPlayAgainButton.gameObject.SetActive(false);
            }

            public void UpdateGameView()
            {
                remainingTurnsCountText.text = GameStateManager.instance.turnsRemaining.ToString();
                xpValueText.text = GameStateManager.instance.xp.ToString();
                goalsAchievedValueText.text = GameStateManager.instance.goalsAchieved.ToString();

                openChestButton.interactable = GameStateManager.instance.isOpenChestValidMove;
                achieveBonusGoalButton.interactable = GameStateManager.instance.isAchieveBonusGoalValidMove;
            }
        }
    }
}
