using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace GameOperationsSamples
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

                    await GetState();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();

                    Debug.Log("Initialization and signin complete.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task GetState()
            {
                m_UpdatedState = await CloudCode.CallEndpointAsync<UpdatedState>(
                    "CloudAi_GetState", new object());
                if (this == null) return;

                Debug.Log($"Starting State: {m_UpdatedState}");
            }

            public async Task PlayfieldButtonPressed(Coord coord)
            {
                try
                {
                    Debug.Log($"Placing Piece at {coord}");

                    sceneView.EnableButtons(false);

                    sceneView.ShowInProgress(coord, true);

                    sceneView.ShowAiTurn();

                    await PlacePlayerPiece(coord);
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);
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

            async Task PlacePlayerPiece(Coord coord)
            {
                m_UpdatedState = await CloudCode.CallEndpointAsync<UpdatedState>(
                    "CloudAi_ValidatePlayerMoveAndRespond", new CoordParam(coord));
                if (this == null) return;

                sceneView.ShowStatusPopupIfNecessary(m_UpdatedState.status);

                Debug.Log($"New state: {m_UpdatedState}");
            }

            public async void OnNewGameButton()
            {
                try
                {
                    Debug.Log("Starting new game.");

                    sceneView.EnableButtons(false);

                    m_UpdatedState = await CloudCode.CallEndpointAsync<UpdatedState>(
                        "CloudAi_StartNewGame", new object());
                    if (this == null) return;

                    Debug.Log($"Starting State: {m_UpdatedState}");

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();

                    Debug.Log("New game started.");
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

                    await GetState();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    sceneView.ShowState(m_UpdatedState);

                    sceneView.EnableButtons();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
