using GemHunterUGS.Scripts.Core;
using UnityEngine;
namespace GemHunterUGS.Scripts.PlayerHub
{
    public class LevelCompleteUIController : MonoBehaviour
    {
        [SerializeField]
        private LevelCompleteView m_LevelCompleteView;
        private GameManagerUGS m_GameManagerUGS;
    
        private void Start()
        {
            m_GameManagerUGS = GameSystemLocator.Get<GameManagerUGS>();

            if (m_LevelCompleteView == null)
            {
                m_LevelCompleteView = GetComponent<LevelCompleteView>();
            }
            m_LevelCompleteView.Initialize();
            
            m_GameManagerUGS.GameplayLevelWon += HandleShowLevelComplete;
            m_LevelCompleteView.AcceptRewardButton.clicked += HandleHideLevelComplete;
            m_LevelCompleteView.CloseAcceptScreenButton.clicked += HandleHideLevelComplete;
        }

        private void HandleShowLevelComplete()
        {
            m_LevelCompleteView.ShowLevelCompleteScreen(true);
        }

        private void HandleHideLevelComplete()
        {
            m_LevelCompleteView.ShowLevelCompleteScreen(false);
        }

        private void OnDisable()
        {
            m_GameManagerUGS.GameplayLevelWon -= HandleShowLevelComplete;
            m_LevelCompleteView.AcceptRewardButton.clicked -= HandleHideLevelComplete;
            m_LevelCompleteView.CloseAcceptScreenButton.clicked -= HandleHideLevelComplete;
        }
    }
}
