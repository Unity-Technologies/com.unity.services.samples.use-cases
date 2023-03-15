using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class IdleClickerGameSceneManager : MonoBehaviour
    {
        public const int k_PlayfieldSize = 5;
        public const int k_NumWellLevels = 4;

        const string k_WellGrantCurrency = "WATER";
        const int k_WellCostPerLevel = 100;

        public IdleClickerGameSampleView sceneView;
        readonly List<WellInfo>[] m_AllWells = new List<WellInfo>[k_NumWellLevels];
        List<Coord> m_Obstacles;

        // Remember last Well we attempted to merge with so we can give an appropriate error, if necessary.
        public WellInfo lastDropWell { get; private set; }

        async void Start()
        {
            try
            {
                await UnityServices.InitializeAsync();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null)
                    return;

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    if (this == null)
                        return;
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
                if (this == null)
                    return;

                await GetUpdatedState();
                if (this == null)
                    return;

                await EconomyManager.instance.RefreshCurrencyBalances();
                if (this == null)
                    return;

                ShowStateAndStartSimulating();

                sceneView.SetInteractable();

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
                if (this == null)
                    return;

                UpdateState(updatedState);
                Debug.Log($"Starting State: {updatedState}");
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

        public async Task PlayfieldButtonPressed(Vector2 coord)
        {
            Debug.Log($"Placing Well at {coord}");

            try
            {
                // Check for error in placement and ignore request if invalid.
                // Note: This method will show an error popup, if necessary.
                if (!ValidatePlaceWell(coord))
                {
                    return;
                }

                sceneView.SetInteractable(false);
                SimulatedCurrencyManager.instance.StopRefreshingCurrencyBalances();

                sceneView.ShowInProgress(coord, true);

                await PlaceWell(coord);
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

                    sceneView.SetInteractable();
                }
            }
        }

        async Task PlaceWell(Vector2 coord)
        {
            try
            {
                var updatedState = await CloudCodeManager.instance.CallPlaceWellEndpoint(coord);
                if (this == null)
                    return;

                UpdateState(updatedState);
                Debug.Log($"Place Well new state: {updatedState}");

                sceneView.ShowPurchaseAnimation(coord, 1);
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

        public async Task PlayfieldWellDragEvent(Vector2 dragLocation, Vector2 dropLocation)
        {
            Debug.Log($"Dragging Well from {dragLocation} to {dropLocation}");

            try
            {
                // Check for drag error and ignore request if invalid
                // Note: This method will show an error popup, if necessary.
                if (!ValidateDragWell(dragLocation, dropLocation))
                {
                    return;
                }

                sceneView.SetInteractable(false);
                SimulatedCurrencyManager.instance.StopRefreshingCurrencyBalances();

                sceneView.ShowUiHighlight(dragLocation, true);
                sceneView.ShowInProgress(dropLocation, true);

                if (TryFindWell(dropLocation, out var dropWell))
                {
                    lastDropWell = dropWell;

                    await MergeWells(dragLocation, dropLocation);
                }
                else
                {
                    await DragWell(dragLocation, dropLocation);
                }
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

                    sceneView.ShowUiHighlight(dragLocation, false);
                    sceneView.ShowInProgress(dropLocation, false);

                    sceneView.SetInteractable();
                }
            }
        }

        public async Task MergeWells(Vector2 dragLocation, Vector2 dropLocation)
        {
            try
            {
                var updatedState = await CloudCodeManager.instance.CallMergeWellsEndpoint(dragLocation,
                    dropLocation);
                if (this == null)
                    return;

                UpdateState(updatedState);

                sceneView.ShowPurchaseAnimation(dropLocation, lastDropWell.wellLevel + 1);

                Debug.Log($"Merge Wells new state: {updatedState}");
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

        public async Task DragWell(Vector2 dragLocation, Vector2 dropLocation)
        {
            try
            {
                var updatedState = await CloudCodeManager.instance.CallMoveWellEndpoint(dragLocation,
                    dropLocation);
                if (this == null)
                    return;

                UpdateState(updatedState);
                Debug.Log($"Drag Well new state: {updatedState}");
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

        void UpdateState(IdleClickerResult updatedState)
        {
            EconomyManager.instance.SetCurrencyBalance(k_WellGrantCurrency, updatedState.currencyBalance);
            SimulatedCurrencyManager.instance.UpdateServerTimestampOffset(updatedState.timestamp);

            m_AllWells[0] = SetWellLevels(updatedState.wells_level1, 1);
            m_AllWells[1] = SetWellLevels(updatedState.wells_level2, 2);
            m_AllWells[2] = SetWellLevels(updatedState.wells_level3, 3);
            m_AllWells[3] = SetWellLevels(updatedState.wells_level4, 4);

            m_Obstacles = updatedState.obstacles;

            UnlockManager.instance.SetUnlockCounters(updatedState.unlockCounters);
        }

        // Set all well levels in list of wells.
        // Returns original list to simplify setting member fields.
        List<WellInfo> SetWellLevels(List<WellInfo> wells, int setWellLevel)
        {
            for (int i = 0; i < wells.Count; i++)
            {
                var well = wells[i];
                wells[i] = new WellInfo
                {
                    x = well.x,
                    y = well.y,
                    timestamp = well.timestamp,
                    wellLevel = setWellLevel
                };
            }

            return wells;
        }

        bool ValidatePlaceWell(Vector2 coord)
        {
            var isWell = TryFindWell(coord, out var _);
            var isObstacle = IsObstacleAt(coord);

            if (isWell || isObstacle)
            {
                // This will show a space-occupied popup unless the player started dragging then
                // returned the well to the starting location.
                if (isObstacle || !sceneView.didDrag)
                {
                    sceneView.ShowSpaceOccupiedErrorPopup();
                }

                return false;
            }

            if (EconomyManager.instance.GetCurrencyBalance(k_WellGrantCurrency) < k_WellCostPerLevel)
            {
                sceneView.ShowInsufficientFundsPopup(1);

                return false;
            }

            return true;
        }

        bool ValidateDragWell(Vector2 dragLocation, Vector2 dropLocation)
        {
            return IsDragValid(dragLocation, dropLocation, out var _, true);
        }

        public bool IsDragValid(Vector2 dragLocation, Vector2 dropLocation,
            out string hoverErrorString, bool showErrorFlag = false)
        {
            hoverErrorString = "";

            if (dragLocation == dropLocation)
            {
                if (showErrorFlag)
                {
                    // Error is unexpected because we shouldn't call this method with drag & drop the same.
                    sceneView.ShowUnexpectedErrorPopup();
                }

                return false;
            }

            if (!TryFindWell(dragLocation, out var dragWell))
            {
                if (showErrorFlag)
                {
                    // Error is unexpected because we should never try to drag a well from a location that doen't have a well
                    sceneView.ShowUnexpectedErrorPopup();
                }

                return false;
            }

            if (IsObstacleAt(dropLocation))
            {
                if (showErrorFlag)
                {
                    sceneView.ShowSpaceOccupiedErrorPopup();
                }

                return false;
            }

            if (!TryFindWell(dropLocation, out var dropWell))
            {
                return true;
            }

            // Remember the Well being dropped upon in case we need to show an error.
            lastDropWell = dropWell;

            if (dragWell.wellLevel != dropWell.wellLevel)
            {
                if (showErrorFlag)
                {
                    sceneView.ShowWellsDifferentLevelPopup();
                }

                return false;
            }

            if (dragWell.wellLevel >= k_NumWellLevels)
            {
                hoverErrorString = "MAX";
                if (showErrorFlag)
                {
                    sceneView.ShowMaxLevelPopup();
                }

                return false;
            }

            var nextWellLevel = dragWell.wellLevel + 1;
            var wellCost = GetWellCost(nextWellLevel);

            // Check if the desired well is unlocked.
            // Note that we use the string "Well_LevelX" as the key instead of Well level integers so the
            // Unlock Manager could support any arbitrary accomplishments, not just Wells being unlocked.
            if (!UnlockManager.instance.IsUnlocked($"Well_Level{nextWellLevel}"))
            {
                hoverErrorString = "LOCKED";
                if (showErrorFlag)
                {
                    sceneView.ShowWellLockedPopup();
                }

                return false;
            }

            if (EconomyManager.instance.GetCurrencyBalance(k_WellGrantCurrency) < wellCost)
            {
                hoverErrorString = (-wellCost).ToString();
                if (showErrorFlag)
                {
                    sceneView.ShowInsufficientFundsPopup(nextWellLevel);
                }

                return false;
            }

            return true;
        }

        bool TryFindWell(Vector2 location, out WellInfo well)
        {
            foreach (var wellsOfLevel in m_AllWells)
            {
                var i = wellsOfLevel.FindIndex(well => well.x == location.x && well.y == location.y);
                if (i >= 0)
                {
                    well = wellsOfLevel[i];
                    return true;
                }
            }

            well = default;
            return false;
        }

        bool IsObstacleAt(Vector2 location)
        {
            return m_Obstacles.FindIndex(obstacle => obstacle.x == location.x && obstacle.y == location.y) > -1;
        }

        public async void OnResetGameButton()
        {
            try
            {
                Debug.Log("Reset game button pressed.");

                sceneView.SetInteractable(false);
                SimulatedCurrencyManager.instance.StopRefreshingCurrencyBalances();

                await CloudCodeManager.instance.CallResetEndpoint();
                if (this == null)
                    return;

                await GetUpdatedState();
                if (this == null)
                    return;

                ShowStateAndStartSimulating();

                sceneView.SetInteractable();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void ShowStateAndStartSimulating()
        {
            sceneView.ShowState(m_Obstacles, m_AllWells);

            SimulatedCurrencyManager.instance.StartRefreshingCurrencyBalances(m_AllWells);
        }

        public static int GetWellCost(int wellLevel)
        {
            return wellLevel * k_WellCostPerLevel;
        }
    }
}
