using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class IdleClickerGameSampleView : MonoBehaviour
    {
        const int k_PlayfieldSize = IdleClickerGameSceneManager.k_PlayfieldSize;

        // Names of Wells. Note that element 0 is empty because the Wells are 1-indexed throughout
        // the code so you could look up Well names k_WellNames[wellLevel].
        static readonly string[] k_WellNames = { "", "Wood", "Bronze", "Silver", "Gold" };

        public IdleClickerGameSceneManager sceneManager;

        public WellUnlockView[] wellUnlockViews;

        public MessagePopup messagePopup;

        public Button resetGameButton;

        public GameObject invalidDragIcon;
        public TextMeshProUGUI invalidDragText;
        public TextMeshProUGUI invalidDragTextShadow;

        public GameObject dragWellParentGameObject;

        public GameObject statusPanelGray;

        public CanvasGroup objectivesListCanvasGroup;

        public bool didDrag { get; private set; }

        bool m_EnabledFlag;
        readonly GridEntity[,] m_GridEntity = new GridEntity[k_PlayfieldSize, k_PlayfieldSize];

        GridEntity m_CurrentGridEntity;
        GridEntity m_DragGridEntity;
        GameObject m_DragObject;

        public void Update()
        {
            if (m_DragObject != null)
            {
                var mousePosition = Input.mousePosition;
                m_DragObject.transform.position = mousePosition;

                var showErrorFlag = false;
                var hoverErrorText = "";
                if (m_CurrentGridEntity != null)
                {
                    if (m_CurrentGridEntity.gridLocation != m_DragGridEntity.gridLocation)
                    {
                        showErrorFlag = !sceneManager.IsDragValid(m_DragGridEntity.gridLocation,
                            m_CurrentGridEntity.gridLocation, out hoverErrorText);
                        didDrag = true;
                    }
                }

                if (showErrorFlag)
                {
                    invalidDragIcon.SetActive(true);
                    invalidDragText.text = hoverErrorText;
                    invalidDragTextShadow.text = hoverErrorText;
                    invalidDragIcon.transform.position = mousePosition;
                }
                else
                {
                    invalidDragIcon.SetActive(false);
                }
            }
        }

        public void StartDragging(GridEntity dragGridEntity, GameObject dragObject)
        {
            m_DragGridEntity = dragGridEntity;
            m_DragObject = Instantiate(dragObject, dragWellParentGameObject.transform, true);
            didDrag = false;
        }

        public async void OnPointerExit()
        {
            try
            {
                // Disable drag-n-drop by clearing the 'drop' location.
                m_CurrentGridEntity = null;

                await StopDragging();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task StopDragging()
        {
            try
            {
                invalidDragIcon.SetActive(false);

                // Destroy the drag object, if any.
                if (m_DragObject != null)
                {
                    Destroy(m_DragObject);
                    m_DragObject = null;
                }

                // Remember drag and drop locations and clear them.
                var dragGridEntity = m_DragGridEntity;
                m_DragGridEntity = null;
                var dropGridEntity = m_CurrentGridEntity;
                m_CurrentGridEntity = null;

                // If we were dragging a Well then permit drag-n-drop behavior.
                // Note: If drag and drop are the same then ignore it (it will be processed as
                //       a GridEntity Button Press event instead.
                if (dragGridEntity != null && dropGridEntity != null &&
                    dragGridEntity != dropGridEntity)
                {
                    await sceneManager.PlayfieldWellDragEvent(dragGridEntity.gridLocation,
                        dropGridEntity.gridLocation);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void SetActiveGridEntity(GridEntity gridEntity)
        {
            m_CurrentGridEntity = gridEntity;
        }

        public void ClearActiveGridEntity(GridEntity gridEntity)
        {
            if (ReferenceEquals(gridEntity, m_CurrentGridEntity))
            {
                m_CurrentGridEntity = null;
            }
        }

        public void SetInteractable(bool isInteractable = true)
        {
            m_EnabledFlag = isInteractable;

            resetGameButton.interactable = isInteractable;

            foreach (var gridEntity in m_GridEntity)
            {
                gridEntity.button.interactable = isInteractable;
            }
        }

        public void ShowState(List<Coord> obstacles, List<WellInfo>[] allWells)
        {
            Vector2 coord;
            for (coord.x = 0; coord.x < k_PlayfieldSize; coord.x++)
            {
                for (coord.y = 0; coord.y < k_PlayfieldSize; coord.y++)
                {
                    SetGridEntityState(coord, GridEntity.GridContents.Empty);
                }
            }

            for (var i = 0; i < IdleClickerGameSceneManager.k_NumWellLevels; i++)
            {
                var wellsByLevel = allWells[i];
                var gridContents = GridEntity.WellLevelToGridContents(i);

                foreach (var well in wellsByLevel)
                {
                    SetGridEntityState(new Vector2(well.x, well.y), gridContents);
                }
            }

            foreach (var obstacle in obstacles)
            {
                SetGridEntityState(new Vector2(obstacle.x, obstacle.y), GridEntity.GridContents.Obstacle);
            }

            foreach (var wellUnlockView in wellUnlockViews)
            {
                wellUnlockView.ShowStatus();
            }
        }

        public void SetGridEntityState(Vector2 gridLocation, GridEntity.GridContents buttonState)
        {
            var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
            gridEntity.SetGridContents(buttonState);
        }

        public void ShowUiHighlight(Vector2 gridLocation, bool flag)
        {
            var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
            gridEntity.ShowUiHighlight(flag);
        }

        public void ShowInProgress(Vector2 gridLocation, bool flag)
        {
            var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
            gridEntity.ShowInProgress(flag);
        }

        public void ShowPurchaseAnimation(Vector2 gridLocation, int wellLevel)
        {
            var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
            gridEntity.ShowPurchaseAnimation(wellLevel);
        }

        public void ShowGenerateAnimation(Vector2 gridLocation)
        {
            var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
            gridEntity.ShowGenerateAnimation();
        }

        public void RegisterGridEntity(GridEntity gridEntity)
        {
            m_GridEntity[(int)gridEntity.gridLocation.x, (int)gridEntity.gridLocation.y] = gridEntity;
        }

        public void ShowCloudSaveMissingPopup()
        {
            messagePopup.Show("CLOUD SAVE DATA MISSING", "A Cloud Save error has occurred.\n\n" +
                "Please restart sample to fix this issue.");
        }

        public void ShowSpaceOccupiedErrorPopup()
        {
            messagePopup.Show("UNABLE TO PLACE PIECE",
                "Please place new Wells in an empty space.");
        }

        public void ShowInsufficientFundsPopup(int wellLevel)
        {
            if (wellLevel == 1)
            {
                ShowPlaceWellInsufficentFundsPopup();
            }
            else
            {
                ShowMergeWellsInsufficientFundsPopup(wellLevel);
            }
        }

        void ShowPlaceWellInsufficentFundsPopup()
        {
            var wellLevel = 1;
            var wellName = k_WellNames[wellLevel];
            var wellCost = IdleClickerGameSceneManager.GetWellCost(wellLevel);
            var currentWater = EconomyManager.instance.GetCurrencyBalance("WATER");
            var waterNeeded = wellCost - currentWater;

            // Since we may have updated water total after checking that there was enough, we need
            // to ensure this message makes sense by always showing a positive number in the popup.
            if (waterNeeded < 1)
            {
                waterNeeded = 1;
            }

            messagePopup.Show("NOT ENOUGH WATER",
                $"{wellName} Wells cost {wellCost} Water.\n\n" +
                $"You need {waterNeeded} more Water drops\n" +
                $"to place a {wellName} Well.");
        }

        void ShowMergeWellsInsufficientFundsPopup(int wellLevel)
        {
            var upgradedWellName = k_WellNames[wellLevel];
            var mergeWellsName = k_WellNames[wellLevel - 1];
            var nextLevelCost = IdleClickerGameSceneManager.GetWellCost(wellLevel);
            var currentWater = EconomyManager.instance.GetCurrencyBalance("WATER");
            var waterNeeded = nextLevelCost - currentWater;

            // Since we may have updated water total after checking that there was enough, we need
            // to ensure this message makes sense by always showing a positive number in the popup.
            if (waterNeeded < 1)
            {
                waterNeeded = 1;
            }

            messagePopup.Show("NOT ENOUGH WATER",
                $"{upgradedWellName} Wells cost {nextLevelCost} Water.\n\n" +
                $"You need {waterNeeded} more Water drops to merge\n" +
                $"two {mergeWellsName} Wells into a {upgradedWellName} Well.");
        }

        public void ShowVirtualPurchaseFailedErrorPopup()
        {
            messagePopup.Show("UNABLE TO PLACE PIECE", "Virtual purchase failed unexpectedly.");
        }

        public void ShowWellNotFoundPopup()
        {
            messagePopup.Show("WELL NOT FOUND", "No well found at specified location.");
        }

        public void ShowInvalidDragPopup()
        {
            messagePopup.Show("UNABLE TO DRAG WELL",
                "When dragging well, you must move it to a valid new location.");
        }

        public void ShowWellsDifferentLevelPopup()
        {
            messagePopup.Show("WELLS CANNOT BE MERGED",
                "Only Wells of the same type can be merged.");
        }

        public void ShowMaxLevelPopup()
        {
            messagePopup.Show("WELLS CANNOT BE MERGED", "Gold Wells are the best type so cannot be merged.");
        }

        public void ShowInvalidLocationPopup()
        {
            messagePopup.Show("INVALID LOCATION SPECIFIED", "A location specified was invalid.");
        }

        public void ShowWellLockedPopup()
        {
            var mergedWellLevel = sceneManager.lastDropWell.wellLevel + 1;

            // Check the count needed to unlock the desired Well.
            // Note that we use the string "Well_LevelX" as the key instead of Well level integers so the
            // Unlock Manager could support any arbitrary accomplishments, not just Wells being unlocked.
            var countNeeded = UnlockManager.instance.GetCountNeeded($"Well_Level{mergedWellLevel}");
            var mergedWellName = k_WellNames[mergedWellLevel];
            var wellPlurality = countNeeded > 1 ? "Wells" : "Well";

            messagePopup.Show(mergedWellName.ToUpper() + " WELL IS LOCKED",
                $"You must merge {countNeeded} more {k_WellNames[mergedWellLevel - 2]} " +
                $"{wellPlurality} to unlock {mergedWellName} Wells.");
        }

        public void ShowUnexpectedErrorPopup()
        {
            messagePopup.Show("UNEXPECTED ERROR", "An unexpected error occurred.");
        }
    }
}
