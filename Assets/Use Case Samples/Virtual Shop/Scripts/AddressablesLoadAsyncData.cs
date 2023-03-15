using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Unity.Services.Samples.VirtualShop
{
    public class AddressablesLoadAsyncData
    {
        public List<string> ids { get; private set; } =
            new List<string>();

        public List<AsyncOperationHandle<Sprite>> handles { get; private set; } =
            new List<AsyncOperationHandle<Sprite>>();

        public List<Task<Sprite>> tasks { get; private set; } =
            new List<Task<Sprite>>();

        public void Add(string spriteAddress)
        {
            Add(spriteAddress, spriteAddress);
        }

        public void Add(string id, string spriteAddress)
        {
            if (!string.IsNullOrEmpty(spriteAddress) && !ids.Contains(id))
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(spriteAddress);

                ids.Add(id);
                handles.Add(handle);
                tasks.Add(handle.Task);
            }
        }
    }
}
