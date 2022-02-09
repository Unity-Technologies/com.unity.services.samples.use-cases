using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameOperationsSamples
{
    namespace CloudAIMiniGame
    {
        public class GridEntityView : MonoBehaviour
        {
            public CloudAIMiniGameSceneManager sceneManager;
            public CloudAIMiniGameSampleView sceneView;

            public Coord coord;

            public Button button { get; private set; }

            GameObject pieceAiImage;
            GameObject piecePlayerImage;
            GameObject uiHighlightImage;
            GameObject inProgressImage;
            

            public GridContents buttonState { get; private set; } = GridContents.Empty;


            public enum GridContents
            {
                Empty,
                AiPiece,
                PlayerPiece
            }


            void Start()
            {
                button = GetComponent<Button>();

                pieceAiImage = transform.Find("PieceAiImage").gameObject;
                piecePlayerImage = transform.Find("PiecePlayerImage").gameObject;
                uiHighlightImage = transform.Find("UiHighlightImage").gameObject;
                inProgressImage = transform.Find("InProgressImage").gameObject;

                sceneView.RegisterGridEntityView(this);
            }

            public void OnPointerEnter()
            {
                uiHighlightImage.SetActive(button.interactable);
            }

            public void OnPointerExit()
            {
                uiHighlightImage.SetActive(false);
            }

            async public void OnButtonPressed()
            {
                try
                {
                    await sceneManager.PlayfieldButtonPressed(coord);
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

                pieceAiImage.SetActive(gridContents == GridContents.AiPiece);
                piecePlayerImage.SetActive(gridContents == GridContents.PlayerPiece);
            }

            public void ShowUiHighlight(bool showFlag)
            {
                uiHighlightImage.SetActive(showFlag);
            }

            public void ShowInProgressImage(bool showFlag)
            {
                inProgressImage.SetActive(showFlag);
            }

            void OnDestroy()
            {
                button.onClick.RemoveListener(OnButtonPressed);
            }
        }
    }
}
