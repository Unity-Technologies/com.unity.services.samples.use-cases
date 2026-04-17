using System.Threading.Tasks;
using GemHunterUGS.Scripts.LoadingScreen;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Handles scene loading operations with loading screen management.
    /// Provides smooth progress updates and ensures proper scene initialization
    /// through controlled loading phases and scene activation.
    /// 
    /// Key features:
    /// - Loading screen integration
    /// - Progress bar smoothing
    /// - Support for single and additive scene loading
    /// - Safe scene activation with initialization checks
    /// </summary>
    public class SceneLoader
    {
        private readonly LoadingScreenUIController m_LoadingScreenController;
        
        // Scene loading shows real progress up to 90%, then smoothly animates the final 10%
        private const float k_LoadProgressThreshold = 0.9f;
        private const float k_FinalProgressSpeed = 2f;
        
        public SceneLoader(LoadingScreenUIController loadingScreenController)
        {
            m_LoadingScreenController = loadingScreenController;
        }
        
        public Task LoadSceneAdditive(string sceneName) => LoadScene(sceneName, LoadSceneMode.Additive);

        public async Task LoadScene(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            Logger.Log($"Loading scene: {sceneName}");
            m_LoadingScreenController.HandleSceneLoading();

            var loadOperation = SceneManager.LoadSceneAsync(sceneName, mode);
            if (loadOperation == null)
            {
                Logger.LogError($"Failed to load scene {sceneName}");
                return;
            }

            loadOperation.allowSceneActivation = false;
            await HandleLoadingProgress(loadOperation);
            await WaitForSceneActivation(loadOperation);
            
            // First yield ensures scene is loaded
            await Task.Yield();
            // Second yield ensures Awake/Start have completed
            await Task.Yield();
            
            m_LoadingScreenController.HideLoadingScreen();
        }

        private async Task HandleLoadingProgress(AsyncOperation loadOperation)
        {
            float progress = 0f;
            
            // Phase 1: Show real loading progress up to threshold
            while (loadOperation.progress < k_LoadProgressThreshold)
            {
                progress = loadOperation.progress / k_LoadProgressThreshold;
                m_LoadingScreenController.UpdateLoadingProgress(progress);
                await Task.Yield();
            }
            
            // Phase 2: Smoothly animate remaining progress
            while (progress < 1f)
            {
                progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime * k_FinalProgressSpeed);
                m_LoadingScreenController.UpdateLoadingProgress(progress);
                await Task.Yield();
            }
            
            m_LoadingScreenController.UpdateLoadingProgress(1f);
        }
        
        private async Task WaitForSceneActivation(AsyncOperation loadOperation)
        {
            loadOperation.allowSceneActivation = true;
            while (!loadOperation.isDone)
            {
                await Task.Yield();
            }
        }

        public async Task LoadGameLevel(int levelIndex)
        {
            string sceneName = SceneUtility.GetScenePathByBuildIndex(levelIndex);
            sceneName = System.IO.Path.GetFileNameWithoutExtension(sceneName);
            await LoadScene(sceneName);
        }
    }
}
