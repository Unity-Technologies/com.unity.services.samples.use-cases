using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.OverTheAirContent
{
    public class OverTheAirContentSampleView : MonoBehaviour
    {
        const float k_AnchorNoWidth = 0f;
        const float k_AnchorFullHeight = 1f;

        public enum ViewState
        {
            Initializing,
            SampleBegin,
            LauncherBegin,
            LauncherDownloadComplete,
            LoadingDownloadedContent,
            SampleComplete,
        }

        ViewState m_ViewState = ViewState.SampleBegin;

        public GameObject initializingPanel;
        public GameObject startupPanel;
        public GameObject launcherPanel;
        public GameObject downloadedContentPanel;
        public Transform downloadedContentContainer;
        public Button playButton;
        public Button restartSampleButtonKeepCache;
        public Button restartSampleButtonClearCache;
        public RectTransform downloadProgressBarTransform;
        public TextMeshProUGUI downloadProgressLabel;
        public TextMeshProUGUI enjoyText;
        public TextMeshProUGUI loadingContentText;

        void Start()
        {
            TransitionToState(ViewState.Initializing);
        }

        public void UpdateProgressBarText(string newText)
        {
            downloadProgressLabel.text = newText;
        }

        public void UpdateProgressBarCompletion(float progress)
        {
            downloadProgressBarTransform.anchorMax = new Vector2(progress, k_AnchorFullHeight);
        }

        public void TransitionToState(ViewState newViewState)
        {
            m_ViewState = newViewState;

            TurnOffEverything();

            switch (m_ViewState)
            {
                case ViewState.Initializing:
                    initializingPanel.SetActive(true);
                    break;

                case ViewState.SampleBegin:
                    startupPanel.gameObject.SetActive(true);
                    break;

                case ViewState.LauncherBegin:
                    downloadProgressBarTransform.anchorMax = new Vector2(k_AnchorNoWidth, k_AnchorFullHeight);
                    launcherPanel.SetActive(true);
                    break;

                case ViewState.LauncherDownloadComplete:
                    launcherPanel.SetActive(true);
                    playButton.interactable = true;
                    break;

                case ViewState.LoadingDownloadedContent:
                    downloadedContentPanel.SetActive(true);
                    loadingContentText.gameObject.SetActive(true);
                    break;

                case ViewState.SampleComplete:
                    downloadedContentPanel.SetActive(true);
                    enjoyText.gameObject.SetActive(true);
                    restartSampleButtonKeepCache.gameObject.SetActive(true);
                    restartSampleButtonClearCache.gameObject.SetActive(true);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void TurnOffEverything()
        {
            initializingPanel.SetActive(false);
            startupPanel.SetActive(false);
            launcherPanel.SetActive(false);
            downloadedContentPanel.SetActive(false);
            restartSampleButtonKeepCache.gameObject.SetActive(false);
            restartSampleButtonClearCache.gameObject.SetActive(false);
            playButton.interactable = false;
            enjoyText.gameObject.SetActive(false);
            loadingContentText.gameObject.SetActive(false);
        }
    }
}
