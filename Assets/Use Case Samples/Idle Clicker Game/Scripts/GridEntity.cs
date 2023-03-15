using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.IdleClickerGame
{
    public class GridEntity : MonoBehaviour
    {
        public IdleClickerGameSceneManager sceneManager;
        public IdleClickerGameSampleView sceneView;

        public Vector2 gridLocation;

        public Button button { get; private set; }

        public GameObject allPiecesParent;
        public GameObject obstacle;
        public GameObject[] wells;

        public GameObject highlight;
        public GameObject inProgress;
        public GameObject purchaseToast;
        public TextMeshProUGUI purchaseToastText;
        public GameObject generateToast;

        Animator m_PurchaseAnimator;
        Animator m_GenerateAnimator;

        static readonly GridContents[] k_WellLevelToGridContents =
        {
            GridContents.Well_Level1,
            GridContents.Well_Level2,
            GridContents.Well_Level3,
            GridContents.Well_Level4
        };

        public GridContents buttonState { get; private set; } = GridContents.Empty;

        public enum GridContents
        {
            Empty,
            Obstacle,
            Well_Level1,
            Well_Level2,
            Well_Level3,
            Well_Level4
        }

        public static GridContents WellLevelToGridContents(int wellLevel)
        {
            return k_WellLevelToGridContents[wellLevel];
        }

        void Start()
        {
            button = GetComponent<Button>();

            m_PurchaseAnimator = purchaseToast.GetComponent<Animator>();
            m_GenerateAnimator = generateToast.GetComponent<Animator>();

            sceneView.RegisterGridEntity(this);
        }

        public void OnPointerEnter()
        {
            highlight.SetActive(true);
            sceneView.SetActiveGridEntity(this);
        }

        public void OnPointerExit()
        {
            highlight.SetActive(false);
            sceneView.ClearActiveGridEntity(this);
        }

        public void OnPointerDown()
        {
            if (button.interactable)
            {
                if (buttonState >= GridContents.Well_Level1 &&
                    buttonState <= GridContents.Well_Level4)
                {
                    sceneView.StartDragging(this, allPiecesParent);
                }
            }
        }

        public async void OnPointerUp()
        {
            try
            {
                await sceneView.StopDragging();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async void OnButtonPressed()
        {
            try
            {
                await sceneManager.PlayfieldButtonPressed(gridLocation);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void ShowInProgress(bool flag)
        {
            inProgress.SetActive(flag);
        }

        public void SetGridContents(GridContents gridContents)
        {
            buttonState = gridContents;

            obstacle.SetActive(gridContents == GridContents.Obstacle);

            for (var i = 0; i < IdleClickerGameSceneManager.k_NumWellLevels; i++)
            {
                wells[i].SetActive(gridContents == k_WellLevelToGridContents[i]);
            }
        }

        public void ShowUiHighlight(bool showFlag)
        {
            highlight.SetActive(showFlag);
        }

        public void ShowInProgressImage(bool showFlag)
        {
            inProgress.SetActive(showFlag);
        }

        public void ShowPurchaseAnimation(int wellLevel)
        {
            var wellCost = IdleClickerGameSceneManager.GetWellCost(wellLevel);
            purchaseToast.SetActive(true);
            m_PurchaseAnimator.SetTrigger("ToastPop");
            purchaseToastText.text = (-wellCost).ToString();
            Invoke(nameof(HidePurchaseAnimation), 1f);
        }

        void HidePurchaseAnimation()
        {
            purchaseToast.SetActive(false);
        }

        public void ShowGenerateAnimation()
        {
            generateToast.SetActive(true);
            m_GenerateAnimator.SetTrigger("ToastPop");
            Invoke(nameof(HideGenerateAnimation), .5f);
        }

        void HideGenerateAnimation()
        {
            generateToast.SetActive(false);
        }

        public override string ToString()
        {
            return $"{buttonState} at ({gridLocation.x:0},{gridLocation.y:0})";
        }

        void OnDestroy()
        {
            HidePurchaseAnimation();
            HideGenerateAnimation();
            button.onClick.RemoveListener(OnButtonPressed);
        }
    }
}
