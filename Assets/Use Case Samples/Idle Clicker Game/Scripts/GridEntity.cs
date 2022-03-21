using System;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGamingServicesUseCases
{
    namespace IdleClickerGame
    {
        public class GridEntity : MonoBehaviour
        {
            public IdleClickerGameSceneManager sceneManager;
            public IdleClickerGameSampleView sceneView;

            public Vector2 gridLocation;

            public Button button { get; private set; }

            GameObject obstacleImage;
            GameObject wellImage;
            GameObject uiHighlightImage;
            GameObject inProgressImage;
            GameObject purchaseToast;
            GameObject generateToast;

            Animator purchaseAnimator;
            Animator generateAnimator;
            

            public GridContents buttonState { get; private set; } = GridContents.Empty;


            public enum GridContents
            {
                Empty,
                Obstacle,
                Factory
            }


            void Start()
            {
                button = GetComponent<Button>();

                obstacleImage = transform.Find("ObstacleImage").gameObject;
                wellImage = transform.Find("WellImage").gameObject;
                uiHighlightImage = transform.Find("UiHighlightImage").gameObject;
                inProgressImage = transform.Find("InProgressImage").gameObject;
                purchaseToast = transform.Find("PurchaseToast").gameObject;
                generateToast = transform.Find("GenerateToast").gameObject;

                purchaseAnimator = purchaseToast.GetComponent<Animator>();
                generateAnimator = generateToast.GetComponent<Animator>();

                sceneView.RegisterGridEntity(this);
            }

            public void OnPointerEnter()
            {
                uiHighlightImage.SetActive(true);
            }

            public void OnPointerExit()
            {
                uiHighlightImage.SetActive(false);
            }

            async public void OnButtonPressed()
            {
                try
                {
                    await sceneManager.PlayfieldButtonPressed((int)gridLocation.x, (int)gridLocation.y);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            public void ShowInProgress(bool flag)
            {
                inProgressImage.SetActive(flag);
            }

            public void SetGridContents(GridContents gridContents)
            {
                this.buttonState = gridContents;

                obstacleImage.SetActive(gridContents == GridContents.Obstacle);
                wellImage.SetActive(gridContents == GridContents.Factory);
            }

            public void ShowUiHighlight(bool showFlag)
            {
                uiHighlightImage.SetActive(showFlag);
            }

            public void ShowInProgressImage(bool showFlag)
            {
                inProgressImage.SetActive(showFlag);
            }

            public void ShowPurchaseAnimation()
            {
                purchaseToast.SetActive(true);
                purchaseAnimator.SetTrigger("ToastPop");
                Invoke(nameof(HidePurchaseAnimation), 1f);
            }

            void HidePurchaseAnimation()
            {
                purchaseToast.SetActive(false);
            }

            public void ShowGenerateAnimation()
            {
                generateToast.SetActive(true);
                generateAnimator.SetTrigger("ToastPop");
                Invoke(nameof(HideGenerateAnimation), .5f);
            }

            void HideGenerateAnimation()
            {
                generateToast.SetActive(false);
            }

            void OnDestroy()
            {
                button.onClick.RemoveListener(OnButtonPressed);
            }
        }
    }
}
