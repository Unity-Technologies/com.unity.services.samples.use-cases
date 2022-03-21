using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

namespace UnityGamingServicesUseCases
{
    namespace IdleClickerGame
    {
        public class IdleClickerGameSceneManager : MonoBehaviour
        {
            public const int k_PlayfieldSize = 5;

            public IdleClickerGameSampleView sceneView;

            List<Coord> m_Obstacles;
            List<FactoryInfo> m_Factories;


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

                    await GetUpdatedState();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    ShowStateAndStartSimulating();

                    sceneView.EnableButtons();

                    Debug.Log("Initialization and signin complete.");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            async Task GetUpdatedState()
            {
                try
                {
                    var updatedState = await CloudCodeManager.instance.CallGetUpdatedStateEndpoint();
                    if (this == null) return;

                    SimulatedCurrencyManager.instance.UpdateServerTimestampOffset(updatedState.timestamp);

                    Debug.Log($"Starting State: {updatedState}");

                    m_Obstacles = updatedState.obstacles;
                    m_Factories = updatedState.factories;
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

            public async Task PlayfieldButtonPressed(int x, int y)
            {
                var coord = new Vector2(x, y);
                Debug.Log($"Placing Well at {coord}");

                try
                {
                    sceneView.EnableButtons(false);
                    SimulatedCurrencyManager.instance.StopRefreshingCurrencyBalances();

                    sceneView.ShowInProgress(coord, true);

                    await PlaceWell(coord);
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    if (this != null)
                    { 
                        ShowStateAndStartSimulating();

                        sceneView.ShowInProgress(coord, false);

                        sceneView.EnableButtons();
                    }
                }
            }

            async Task PlaceWell(Vector2 coord)
            {
                try
                {
                    var placePieceResult = await CloudCodeManager.instance.CallPlaceWellEndpoint(coord);
                    if (this == null) return;

                    SimulatedCurrencyManager.instance.UpdateServerTimestampOffset(placePieceResult.timestamp);

                    m_Obstacles = placePieceResult.obstacles;
                    m_Factories = placePieceResult.factories;

                    sceneView.ShowPurchaseAnimation(coord);

                    Debug.Log($"New state: {placePieceResult}");
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

            public async void OnResetGameButton()
            {
                try
                {
                    Debug.Log("Reset game button pressed.");

                    sceneView.EnableButtons(false);
                    SimulatedCurrencyManager.instance.StopRefreshingCurrencyBalances();

                    await SaveData.ForceDeleteAsync("IDLE_CLICKER_GAME_STATE");
                    if (this == null) return;

                    await GetUpdatedState();
                    if (this == null) return;

                    await EconomyManager.instance.RefreshCurrencyBalances();
                    if (this == null) return;

                    ShowStateAndStartSimulating();

                    sceneView.EnableButtons();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            void ShowStateAndStartSimulating()
            {
                sceneView.ShowState(m_Obstacles, m_Factories);

                SimulatedCurrencyManager.instance.StartRefreshingCurrencyBalances(m_Factories);
            }
        }
    }
}
