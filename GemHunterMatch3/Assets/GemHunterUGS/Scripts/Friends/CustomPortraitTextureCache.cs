using System.Collections.Generic;
using GemHunterUGS.Scripts.Utilities;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.Friends
{
    /// <summary>
    /// Simple cache for player portrait textures. Since the game currently only displays a maximum
    /// of 25 players at once, this cache provides a straightforward way to avoid
    /// recreating textures for the same base64 strings multiple times.
    /// </summary>
    public class CustomPortraitTextureCache
    {
        private static readonly Dictionary<string, Texture2D> textureCache = new();
        
        
        /// <summary>
        /// Gets a texture from the cache or creates a new one if it doesn't exist.
        /// </summary>
        /// <param name="base64">The base64 encoded string representation of the texture</param>
        /// <returns>The cached or newly created Texture2D, or null if creation fails</returns>
        public static Texture2D GetTexture(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            
            if (!textureCache.TryGetValue(base64, out var texture))
            {
                texture = base64.ConvertBase64ToTexture2D();
                if (texture != null)
                {
                    textureCache[base64] = texture;
                }
                else
                {
                    Logger.LogWarning($"Failed to create texture from base64 string");
                }
            }
            return texture;
        }

        public static void Clear()
        {
            foreach (var texture in textureCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            textureCache.Clear();
        }
    }
}
