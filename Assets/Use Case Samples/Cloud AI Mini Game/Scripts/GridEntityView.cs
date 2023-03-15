using System;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.CloudAIMiniGame
{
    public class GridEntityView : MonoBehaviour
    {
        public CloudAIMiniGameSceneManager sceneManager;
        public CloudAIMiniGameSampleView sceneView;

        public Coord coord;

        Button m_Button;
        GridContents m_ButtonState = GridContents.Empty;
        bool m_IsPointerHovering;

        GameObject m_PieceAiImage;
        GameObject m_PiecePlayerImage;
        GameObject m_UiHighlightImage;
        GameObject m_InProgressImage;

        public enum GridContents
        {
            Empty,
            AiPiece,
            PlayerPiece
        }

        void Start()
        {
            m_Button = GetComponent<Button>();

            m_PieceAiImage = transform.Find("PieceAiImage").gameObject;
            m_PiecePlayerImage = transform.Find("PiecePlayerImage").gameObject;
            m_UiHighlightImage = transform.Find("UiHighlightImage").gameObject;
            m_InProgressImage = transform.Find("InProgressImage").gameObject;

            sceneView.RegisterGridEntityView(this);
        }

        public void OnPointerEnter()
        {
            m_UiHighlightImage.SetActive(m_Button.interactable);
            m_IsPointerHovering = true;
        }

        public void OnPointerExit()
        {
            m_UiHighlightImage.SetActive(false);
            m_IsPointerHovering = false;
        }

        public void SetInteractable(bool isInteractable = true)
        {
            m_Button.interactable = isInteractable;
            m_UiHighlightImage.SetActive(m_IsPointerHovering && isInteractable);
        }

        public async void OnButtonPressed()
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
            m_InProgressImage.SetActive(flag);
        }

        public void SetGridContents(GridContents gridContents)
        {
            m_ButtonState = gridContents;

            m_PieceAiImage.SetActive(gridContents == GridContents.AiPiece);
            m_PiecePlayerImage.SetActive(gridContents == GridContents.PlayerPiece);
        }

        public void ShowUiHighlight(bool showFlag)
        {
            m_UiHighlightImage.SetActive(showFlag);
        }

        public void ShowInProgressImage(bool showFlag)
        {
            m_InProgressImage.SetActive(showFlag);
        }

        void OnDestroy()
        {
            m_Button.onClick.RemoveListener(OnButtonPressed);
        }
    }
}
