using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace CloudAIMiniGame
    {
        public class CloudAIMiniGameSceneManager : MonoBehaviour
        {
            public const int k_PlayfieldSize = 3;

            public CloudAIMiniGameSampleView sceneView;

            UpdatedState m_UpdatedState;


            async void Start()
            {
                try
                {
                    await UnityServices.InitializeAsync();

                    // Check that scene has not been unloaded while processing async wait to prevent throw.
                    if (this == null) return;

                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                        if (this == null) return;
                    }

                    Debug.Log($"Player id:{AuthenticationService.Instance.PlayerId}");

                    // Economy configuration should be refreshed every time the app initializes.
                    // Doing so updates the cached configuration data and initializes for this player any items or
                    // currencies that were recently published.
                    // 
                    // It's important to do this update before making any other calls to the Economy or Remote Config
                    // APIs as both use the cached data list. (Though it wouldn't be necessary to do if only using Remote
                    // Config in your project and not Economy.)
                    await EconomyManager.instance.RefreshEconomyConfiguration();
                    if (this == null) return;

                    // Get current state. If this fails because of a Cloud Code error, the Cloud Code Manager will
                    // handle it, then throw a Result-Unavailable exception which prevents further processing here.
                    m_UpdatedState = await CloudCodeManager.instance.CallGetStateEndpoint();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.SetInteractable();

                    Debug.Log("Initialization and signin complete.");
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public async Task PlayfieldButtonPressed(Coord coord)
            {
                try
                {
                    Debug.Log($"Placing Piece at {coord}");

                    sceneView.SetInteractable(false);

                    sceneView.ShowInProgress(coord, true);

                    sceneView.ShowAiTurn();

                    m_UpdatedState = await CloudCodeManager.instance.CallValidatePlayerMoveAndRespondEndpoint(coord);
                    if (this == null) return;

                    if (m_UpdatedState.isGameOver)
                    {
                        sceneView.ShowGameOverPopup(m_UpdatedState.status);
                    }

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    { 
                        sceneView.ShowInProgress(coord, false);
                        sceneView.SetInteractable();
                    }
                }
            }

            public async void OnNewGameButton()
            {
                try
                {
                    Debug.Log("Starting new game.");

                    sceneView.SetInteractable(false);

                    m_UpdatedState = await CloudCodeManager.instance.CallStartNewGameEndpoint();
                    if (this == null) return;

                    Debug.Log($"Starting State: {m_UpdatedState}");

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.SetInteractable();

                    Debug.Log("New game started.");
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    {
                        sceneView.SetInteractable();
                    }
                }
            }

            public async void OnResetGameButton()
            {
                try
                {
                    Debug.Log("Reset game button pressed.");

                    sceneView.SetInteractable(false);

                    await CloudSaveService.Instance.Data.ForceDeleteAsync("CLOUD_AI_GAME_STATE");
                    if (this == null) return;

                    m_UpdatedState = await CloudCodeManager.instance.CallGetStateEndpoint();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.SetInteractable();
                }
                catch (CloudCodeResultUnavailableException)
                {
                    // Exception already handled by CloudCodeManager
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
