using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Services.Samples.OverTheAirContent
{
    public class OverTheAirContentSceneManager : MonoBehaviour
    {
        public OverTheAirContentSampleView sceneView;

        string m_DownloadedContentPrefabAddress;
        GameObject m_DownloadedContentGameObject;
        AsyncOperationHandle m_LoadPrefabHandle;
        AsyncOperationHandle<GameObject> m_InstantiatePrefabHandle;

        async void Start()
        {
            try
            {
                await InitializeUnityServices();

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                await DownloadContentCatalog();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task InitializeUnityServices()
        {
            await UnityServices.InitializeAsync();
            if (this == null) return;

            Debug.Log("Services Initialized.");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing in...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (this == null) return;
            }

            Debug.Log($"Player id: {AuthenticationService.Instance.PlayerId}");

            await RemoteConfigManager.instance.FetchConfigs();
        }

        async Task DownloadContentCatalog()
        {
            sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.Initializing);

            var remoteCatalogAddress = RemoteConfigManager.instance.cloudCatalogAddress;

            var handle = Addressables.LoadContentCatalogAsync(remoteCatalogAddress, false);
            await handle.Task;
            if (this == null) return;

            switch (handle.Status)
            {
                case AsyncOperationStatus.None:
                    Debug.Log("Catalog Download: None");
                    break;

                case AsyncOperationStatus.Succeeded:
                    Debug.Log("Catalog Download: Succeeded");

                    sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.SampleBegin);

                    break;

                case AsyncOperationStatus.Failed:
                    Debug.Log("Catalog Download: Failed");
                    Addressables.Release(handle);
                    throw handle.OperationException;

                default:
                    Addressables.Release(handle);
                    throw new ArgumentOutOfRangeException();
            }

            Addressables.Release(handle);
        }

        IEnumerator DownloadNewContent()
        {
            sceneView.UpdateProgressBarText("Downloading New Content");
            sceneView.UpdateProgressBarCompletion(0f);

            var newContentAddresses = RemoteConfigManager.instance.GetNewContentAddresses();

            m_DownloadedContentPrefabAddress = newContentAddresses[0];

            var cacheCheckHandle = Addressables.GetDownloadSizeAsync(newContentAddresses);
            while (!cacheCheckHandle.IsDone)
            {
                yield return null;
            }

            var downloadSize = cacheCheckHandle.Result;

            Addressables.Release(cacheCheckHandle);

            if (downloadSize > 0)
            {
                var dependenciesHandle = Addressables.DownloadDependenciesAsync(newContentAddresses, Addressables.MergeMode.UseFirst);
                while (!dependenciesHandle.IsDone)
                {
                    yield return null;
                    sceneView.UpdateProgressBarCompletion(dependenciesHandle.PercentComplete);
                }

                switch (dependenciesHandle.Status)
                {
                    case AsyncOperationStatus.None:
                        Debug.Log("Content Download Result: None");
                        break;

                    case AsyncOperationStatus.Succeeded:
                        Debug.Log("Content Download Result: Succeeded");
                        break;

                    case AsyncOperationStatus.Failed:
                        Debug.Log("Content Download Result: Failed");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Addressables.Release(dependenciesHandle);

                sceneView.downloadProgressLabel.text = "Download Complete";
            }
            else
            {
                sceneView.UpdateProgressBarCompletion(1f);

                sceneView.downloadProgressLabel.text = "Using Cached Assets";
            }

            sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.LauncherDownloadComplete);
        }

        public void OnBeginButtonPressed()
        {
            sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.LauncherBegin);

            StartCoroutine(DownloadNewContent());
        }

        public async void OnPlayButtonPressed()
        {
            try
            {
                sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.LoadingDownloadedContent);

                await LoadDownloadedContentPrefab();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task LoadDownloadedContentPrefab()
        {
            m_LoadPrefabHandle = Addressables.LoadAssetAsync<GameObject>(m_DownloadedContentPrefabAddress);
            await m_LoadPrefabHandle.Task;
            if (this == null) return;

            m_InstantiatePrefabHandle = Addressables.InstantiateAsync(m_DownloadedContentPrefabAddress, sceneView.downloadedContentContainer);
            await m_InstantiatePrefabHandle.Task;
            if (this == null) return;

            if (m_InstantiatePrefabHandle.Result != null)
            {
                m_DownloadedContentGameObject = m_InstantiatePrefabHandle.Result;

                if (m_InstantiatePrefabHandle.Result.transform is RectTransform prefabRectTransform)
                {
                    prefabRectTransform.SetSiblingIndex(0);
                }
            }

            sceneView.TransitionToState(OverTheAirContentSampleView.ViewState.SampleComplete);

            Addressables.Release(m_LoadPrefabHandle);
        }

        public async void OnRestartSampleButtonPressed(bool clearCache)
        {
            try
            {
                Destroy(m_DownloadedContentGameObject);

                Addressables.Release(m_InstantiatePrefabHandle);

                if (clearCache)
                {
                    Caching.ClearCache();
                }

                await DownloadContentCatalog();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
