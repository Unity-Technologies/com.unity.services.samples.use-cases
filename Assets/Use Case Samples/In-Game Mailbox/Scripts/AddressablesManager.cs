using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Services.Samples.InGameMailbox
{
    public class AddressablesManager : MonoBehaviour
    {
        public static AddressablesManager instance { get; private set; }

        // Dictionary of all economy items (Currencies and Items) to associated Sprite.
        // Note: this dictionary allows for lookup of the icon associated with any Currency
        // or Inventory Item that has Custom Data correctly setup on the Economy Service.
        public Dictionary<string, Sprite> preloadedSpritesByEconomyId { get; } =
            new Dictionary<string, Sprite>();

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

        // Read all Currencies and Items to preload all associated Sprite Addresses from Custom data.
        public async Task PreloadAllEconomySprites()
        {
            var economyAddressableHandles = new Dictionary<string, AsyncOperationHandle<Sprite>>();
            var addressableLoadTasks = new List<Task<Sprite>>();

            foreach (var economySpriteAddress in EconomyManager.instance.economySpriteAddresses)
            {
                // the Key of economySpriteAddress is the Economy item's id and the Value is the spriteAddress
                var handle = Addressables.LoadAssetAsync<Sprite>(economySpriteAddress.Value);
                economyAddressableHandles[economySpriteAddress.Key] = handle;
                addressableLoadTasks.Add(handle.Task);
            }

            // Wait for all Addressables to be loaded.
            await Task.WhenAll(addressableLoadTasks);

            // Check that scene has not been unloaded while processing async wait to prevent throw.
            if (this == null) return;

            // Iterate all Addressables loaded and save off the Sprites into our Dictionary.
            AddAddressablesSpritesToDictionary(economyAddressableHandles);
        }

        void AddAddressablesSpritesToDictionary(Dictionary<string, AsyncOperationHandle<Sprite>> economyHandles)
        {
            preloadedSpritesByEconomyId.Clear();

            foreach (var economyHandle in economyHandles)
            {
                var economyId = economyHandle.Key;
                var handle = economyHandle.Value;

                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    preloadedSpritesByEconomyId[economyId] = handle.Result;
                }
                else
                {
                    Debug.LogError($"A sprite could not be found for the address {economyId}." +
                        $" Addressables exception: {handle.OperationException}");
                }
            }
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
