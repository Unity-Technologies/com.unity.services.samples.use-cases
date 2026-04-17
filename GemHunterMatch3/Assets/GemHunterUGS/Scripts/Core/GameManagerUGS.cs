using System;
using System.Threading.Tasks;
using Match3;
using UnityEngine;
using UnityEngine.SceneManagement;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
using Random = UnityEngine.Random;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Core game manager that handles scene navigation, authentication flow, and gameplay session management.
    /// Serves as a bridge between the meta-game systems (UGS features) and the core Match3 gameplay.
    /// 
    /// Key responsibilities:
    /// - Scene management (Init, MainMenu, PlayerHub, Gameplay)
    /// - Authentication state handling
    /// - Gameplay session lifecycle
    /// </summary>
    public class GameManagerUGS : MonoBehaviour
    {
        [SerializeField]
        private LevelList m_LevelList;
        
        private SceneLoader m_SceneLoader;
        private PlayerAuthenticationManager m_AuthenticationManager;

        private bool m_HasGameInitialized;
        
        private readonly struct SceneInfo
        {
            public readonly string Name;
            public readonly int BuildIndex;
        
            public SceneInfo(string name, int buildIndex)
            {
                Name = name;
                BuildIndex = buildIndex;
            }
        }

        private static readonly SceneInfo InitScene = new("InitGemHunterUGS", 0);
        private static readonly SceneInfo MainMenu = new("MainLogin", 1);
        private static readonly SceneInfo PlayerHub = new("PlayerHub", 2);
        
        public event Action GameplayLevelWon;
        public event Action GameplayReplayLevelLost;
        
        private bool IsCurrentScene(int buildIndex) =>
            SceneManager.GetActiveScene().buildIndex == buildIndex;
        
        public void Initialize(PlayerAuthenticationManager authenticationManager, SceneLoader sceneLoader)
        {
            m_AuthenticationManager = authenticationManager;
            m_SceneLoader = sceneLoader;
            
            HandleStartupAuthentication();
        }
        
        private async void HandleStartupAuthentication()
        {
            await m_AuthenticationManager.SignInCachedPlayerAsync();
    
            if (m_AuthenticationManager.IsSignedIn)
            {
                await RequestLoadPlayerHub();
                return;
            }
    
            await RequestLoadMainMenu();
        }
        
        public async Task RequestLoadPlayerHub()
        {
            if (IsCurrentScene(PlayerHub.BuildIndex))
            {
                Logger.Log("PlayerHub already loaded");
                return;
            }
            await m_SceneLoader.LoadScene(PlayerHub.Name);
        }
        
        public async Task RequestLoadMainMenu()
        {
            await m_SceneLoader.LoadScene(MainMenu.Name);
        }
        
        /// <summary>
        /// Gem Hunter is a demo for 2D mobile gameplay; "Gem Hunter UGS" is a demo for using Unity Gaming Services for meta-game aspects.
        /// We wanted to keep Gem Hunter game architecture as-is. Here we are bridging the two demos so that the game can be played from the Hub and a Star can be earned on level complete.
        /// </summary>
        public async Task StartGameplay()
        {
            Logger.LogDemo($"▶ Starting gameplay scene...");
            
            var gameplayBootstrap = GetComponent<GameplayBootstrap>();
            gameplayBootstrap.InitializeGameplayManager();
            
            int randomLevel = Random.Range(0, m_LevelList.SceneCount);
            await m_SceneLoader.LoadGameLevel(m_LevelList.SceneList[randomLevel]);
            await Task.Yield();

            GameManager.Instance.ReturnToHubWonResult += HandleReturnToHubWin;
            GameManager.Instance.ReturnToHubLostResult += HandleReturnToHubLost;
            GameManager.Instance.ReplayLevelLost += HandleReplayLevelLost;
        }
        
        private async void HandleReturnToHubWin()
        {
            try
            {
                UnsubscribeFromGameEvents();
                PrepareSceneTransition();
            
                await RequestLoadPlayerHub();
                await Task.Yield();
                
                Logger.LogDemo("⚡ GameplayLevelWon");
                GameplayLevelWon?.Invoke();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }
        
        private async void HandleReturnToHubLost()
        {
            try
            {
                UnsubscribeFromGameEvents();
                PrepareSceneTransition();
            
                await RequestLoadPlayerHub();
            }
            catch (Exception e)
            {
                Logger.LogException(e);
            }
        }

        private void PrepareSceneTransition()
        {
            LevelData.Instance.DarkenBackground(true);
            UIHandler.Instance.Display(false);
        }

        private void UnsubscribeFromGameEvents()
        {
            GameManager.Instance.ReturnToHubWonResult -= HandleReturnToHubWin;
            GameManager.Instance.ReturnToHubLostResult -= HandleReturnToHubLost;
            GameManager.Instance.ReplayLevelLost -= HandleReplayLevelLost;
        }

        private void HandleReplayLevelLost()
        {
            Logger.LogDemo("⚡ GameplayReplayLevelLost");
            GameplayReplayLevelLost?.Invoke();
        }
    }
}
