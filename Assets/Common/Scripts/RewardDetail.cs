using System;
using UnityEngine;

namespace Unity.Services.Samples
{
    [Serializable]
    public struct RewardDetail
    {
        public string id;
        public long quantity;

        // Sprite to use in reward (usually icon for Currency or Inventory Item).
        // Note: if not specified, reward will fall back to use the Sprite Addressable Address instead.
        public Sprite sprite;

        // If "Sprite" is not specified, this specifies the Sprite Addressable Address to use for the icon.
        public string spriteAddress;

        public override string ToString()
        {
            return $"{quantity} {id}";
        }
    }
}
