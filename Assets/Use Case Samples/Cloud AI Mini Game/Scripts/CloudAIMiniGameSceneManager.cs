using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
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

                    // Get current state. If this fails because of a Cloud Code error, the Cloud Code Manager will
                    // handle it, then throw a Result-Unavailable exception which prevents furth processing here.
                    m_UpdatedState = await CloudCodeManager.instance.CallGetStateEndpoint();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();

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

                    sceneView.EnableButtons(false);

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
                        sceneView.EnableButtons();
                    }
                }
            }

            public async void OnNewGameButton()
            {
                try
                {
                    Debug.Log("Starting new game.");

                    sceneView.EnableButtons(false);

                    m_UpdatedState = await CloudCodeManager.instance.CallStartNewGameEndpoint();
                    if (this == null) return;

                    Debug.Log($"Starting State: {m_UpdatedState}");

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();

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
                        sceneView.EnableButtons();
                    }
                }
            }

            public async void OnResetGameButton()
            {
                try
                {
                    Debug.Log("Reset game button pressed.");

                    sceneView.EnableButtons(false);

                    await SaveData.ForceDeleteAsync("CLOUD_AI_GAME_STATE");
                    if (this == null) return;

                    m_UpdatedState = await CloudCodeManager.instance.CallGetStateEndpoint();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();
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
