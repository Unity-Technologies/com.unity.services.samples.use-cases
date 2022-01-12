using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEditor;
using UnityEngine;

namespace GameOperationsSamples
{
    namespace IdleClickerGame
    {
        public class IdleClickerGameSceneManager : MonoBehaviour
        {
            public const int k_PlayfieldSize = 5;

            public IdleClickerGameSampleView sceneView;

            public MessagePopup messagePopup;

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
                var updatedState = await CloudCode.CallEndpointAsync<UpdatedState>(
                    "IdleClicker_GetUpdatedState", new object());
                if (this == null) return;

                SimulatedCurrencyManager.instance.UpdateServerTimestampOffset(updatedState.timestamp);

                Debug.Log($"Starting State: {updatedState}");

                m_Obstacles = updatedState.obstacles;
                m_Factories = updatedState.factories;
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
                    if (this == null) return;

                    ShowStateAndStartSimulating();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    sceneView.ShowInProgress(coord, false);

                    sceneView.EnableButtons();
                }
            }

            async Task PlaceWell(Vector2 coord)
            {
                var placePieceResult = await CloudCode.CallEndpointAsync<PlacePieceResult>(
                    "IdleClicker_PlaceWell", new CoordParam { coord = { x = (int)coord.x, y = (int)coord.y } });
                if (this == null) return;

                SimulatedCurrencyManager.instance.UpdateServerTimestampOffset(placePieceResult.timestamp);

                m_Obstacles = placePieceResult.obstacles;
                m_Factories = placePieceResult.factories;

                switch (placePieceResult.placePieceResult)
                {
                    case "success":
                        sceneView.ShowPurchaseAnimation(coord);
                        break;

                    case "spaceAlreadyOccupied":
                        messagePopup.Show("Unable to place piece.", "Space already occupied.\n\n" +
                            "Please ensure target space is empty when placing a Well.");
                        break;

                    case "virtualPurchaseFailure":
                        messagePopup.Show("Unable to place piece.", "Virtual purchase failed.\n\n" +
                            "Please ensure that you have sufficient funds when purchasing a Well.");
                        break;
                }


                Debug.Log($"New state: {placePieceResult}");
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
