using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Services.Samples.ProjectInbox
{
    public class AddressablesManager : MonoBehaviour
    {
        // This manager class can't be static because if it holds references to handles when the OTA use case is
        // interacted with, the OTA use case can't clear the cache.
        public static AddressablesManager instance { get; private set; }

        public Dictionary<string, (Sprite sprite, AsyncOperationHandle<Sprite> handle)>
            addressableSpriteContent { get; } =
            new Dictionary<string, (Sprite sprite, AsyncOperationHandle<Sprite> handle)>();

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
            }
            else
            {
                instance = this;
            }
        }

        public async Task DownloadContentCatalog()
        {
            var remoteCatalogAddress = RemoteConfigManager.ccdContentUrl;

            var handle = Addressables.LoadContentCatalogAsync(remoteCatalogAddress, false);
            await handle.Task;
            if (this == null) return;

            switch (handle.Status)
            {
                case AsyncOperationStatus.None:
                    Debug.Log("Addressable Catalog Download: None");
                    break;

                case AsyncOperationStatus.Succeeded:
                    Debug.Log("Addressable Catalog Download: Succeeded");
                    break;

                case AsyncOperationStatus.Failed:
                    Debug.Log("Addressable Catalog Download: Failed");
                    Addressables.Release(handle);
                    throw handle.OperationException;

                default:
                    Addressables.Release(handle);
                    throw new ArgumentOutOfRangeException();
            }

            Addressables.Release(handle);
        }

        public async void LoadImageForMessage(string imageAddress, string messageId)
        {
            try
            {
                if (string.IsNullOrEmpty(imageAddress) || addressableSpriteContent.ContainsKey(messageId))
                {
                    return;
                }

                var imageLoadHandle = Addressables.LoadAssetAsync<Sprite>(imageAddress);
                await imageLoadHandle.Task;
                if (this == null) return;

                var sprite = imageLoadHandle.Result;

                if (!(sprite is null))
                {
                    addressableSpriteContent.Add(messageId, (sprite, imageLoadHandle));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"There was a problem downloading the image for message {messageId}: {e}");
            }
        }

        public void TryReleaseHandle(string spriteContentKey)
        {
            if (addressableSpriteContent.TryGetValue(spriteContentKey, out var spriteContent))
            {
                Addressables.Release(spriteContent.handle);
                addressableSpriteContent.Remove(spriteContentKey);
            }
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                foreach (var spriteContent in addressableSpriteContent.Values)
                {
                    Addressables.Release(spriteContent.handle);
                }

                instance = null;
            }
        }
    }
}
