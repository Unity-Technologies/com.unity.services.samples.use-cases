using UnityEngine;
using UnityEngine.EventSystems;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Handles the initialization and event system switching between the two integrated demos:
    /// 1. The UGS (Unity Gaming Services) meta-game demo
    /// 2. The Match3 gameplay demo
    /// 
    /// Each demo has its own GameManager and EventSystem. This class ensures proper initialization
    /// and handles switching between their respective event systems when transitioning to gameplay.
    /// </summary>
    public class GameplayBootstrap : MonoBehaviour
    {
        [SerializeField] 
        private Match3.GameManager m_GameManagerPrefab;
        [SerializeField]
        private EventSystem m_UGSEventSystem;
        [SerializeField]
        private EventSystem m_GameEventSystem;
        
        public void InitializeGameplayManager()
        {
            m_UGSEventSystem.enabled = false;

            if (Match3.GameManager.Instance == null)
            {
                Instantiate(m_GameManagerPrefab);
                m_GameEventSystem = GetComponentInChildren<EventSystem>();
            }

            if (m_GameEventSystem != null)
            {
                m_GameEventSystem.enabled = true;
            }
        }
    }
}
