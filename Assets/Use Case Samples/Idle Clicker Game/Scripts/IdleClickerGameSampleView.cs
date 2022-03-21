using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace IdleClickerGame
    {
        public class IdleClickerGameSampleView : MonoBehaviour
        {
            const int k_PlayfieldSize = IdleClickerGameSceneManager.k_PlayfieldSize;

            public MessagePopup messagePopup;

            public Button resetGameButton;

            bool m_EnabledFlag = false;

            GridEntity[,] m_GridEntity = new GridEntity[k_PlayfieldSize, k_PlayfieldSize];


            public void EnableButtons(bool flag = true)
            {
                m_EnabledFlag = flag;

                resetGameButton.interactable = flag;

                foreach (var gridEntity in m_GridEntity)
                {
                    gridEntity.button.interactable = flag;
                }
            }

            public void ShowState(List<Coord> obstacles, List<FactoryInfo> factories)
            {
                Vector2 coord;
                for (coord.x = 0; coord.x < k_PlayfieldSize; coord.x++)
                {
                    for (coord.y = 0; coord.y < k_PlayfieldSize; coord.y++)
                    {
                        SetGridEntityState(coord, GridEntity.GridContents.Empty);
                    }
                }

                foreach (var obstacle in obstacles)
                {
                    SetGridEntityState(new Vector2(obstacle.x, obstacle.y), GridEntity.GridContents.Obstacle);
                }

                foreach (var factory in factories)
                {
                    SetGridEntityState(new Vector2(factory.x, factory.y), GridEntity.GridContents.Factory);
                }
            }

            public void SetGridEntityState(Vector2 gridLocation, GridEntity.GridContents buttonState)
            {
                var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
                gridEntity.SetGridContents(buttonState);
            }

            public void ShowInProgress(Vector2 gridLocation, bool flag)
            {
                var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
                gridEntity.ShowInProgress(flag);
            }

            public void ShowPurchaseAnimation(Vector2 gridLocation)
            {
                var gridEntity = m_GridEntity[(int)gridLocation.x, (int)gridLocation.y];
                gridEntity.ShowPurchaseAnimation();
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

            public void ShowSpaceOccupiedErrorPopup()
            {
                messagePopup.Show("Unable to place piece.", "Space already occupied.\n\n" +
                    "Please ensure target space is empty when placing a Well.");
            }

            public void ShowVirtualPurchaseFailedErrorPopup()
            {
                messagePopup.Show("Unable to place piece.", "Virtual purchase failed.\n\n" +
                    "Please ensure that you have sufficient funds when purchasing a Well.");
            }
        }
    }
}
