using System;
using UnityEngine;
namespace GemHunterUGS.Scripts.Utilities
{
    /// <summary>
    /// Provides extension methods for converting between different image formats used in profile picture handling.
    /// Base64 strings are used to efficiently store and transmit image data as text, allowing custom player
    /// profile pictures to be saved and synced across devices. Base64 encoding converts binary image data
    /// into ASCII text that can be safely stored in databases and JSON.
    /// 
    /// The class handles:
    /// - Converting base64 strings to Unity Texture2D objects
    /// - Converting Texture2D objects to base64 strings
    /// - Converting between Texture2D and Sprite formats
    /// - Ensuring proper texture readability and format compatibility
    /// </summary>
    public static class TextureExtensions
    {
        public static Texture2D ConvertBase64ToTexture2D(this string base64)
        {
            if (string.IsNullOrEmpty(base64))
            {
                Logger.LogError("Base64 string is null or empty.");
                return null;
            }

            try
            {
                byte[] imageData = Convert.FromBase64String(base64);

                // First create a temporary texture to get the actual dimensions
                Texture2D tempTexture = new Texture2D(2, 2);
                if (!tempTexture.LoadImage(imageData))
                {
                    Logger.LogError("Failed to load image into temporary texture");
                    return null;
                }

                // Round dimensions up to nearest multiple of 4
                int width = (tempTexture.width + 3) & ~3;
                int height = (tempTexture.height + 3) & ~3;

                Logger.LogVerbose($"Original dimensions: {tempTexture.width}x{tempTexture.height}, Adjusted: {width}x{height}");

                // Create the final texture with the adjusted dimensions
                Texture2D finalTexture = new Texture2D(width, height, TextureFormat.RGBA32, true); // Use RGBA32 instead of ETC2
                finalTexture.filterMode = FilterMode.Bilinear;

                // Copy pixels from temp texture
                Color[] pixels = tempTexture.GetPixels();
                finalTexture.SetPixels(pixels);
                finalTexture.Apply(true);

                UnityEngine.Object.Destroy(tempTexture);

                Logger.LogVerbose($"Created texture: format={finalTexture.format}, mipmaps={finalTexture.mipmapCount}");
                return finalTexture;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error converting base64 to Texture2D: {e.Message}\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        public static string ConvertTextureToBase64(this Texture2D texture)
        {
            try
            {
                Texture2D readableTexture = EnsureTextureIsReadable(texture);
                byte[] bytes = readableTexture.EncodeToPNG();
                if (readableTexture != texture)
                {
                    UnityEngine.Object.Destroy(readableTexture);
                }
                return Convert.ToBase64String(bytes);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error converting Texture2D to base64: {e.Message}");
                return null;
            }
        }
        
        public static Sprite ConvertTextureToSprite(this Texture2D texture)
        {
            if (texture == null)
            {
                Logger.LogError("Texture is null.");
                return null;
            }

            try
            {
                // Create with explicit atlas parameters
                var spriteAtlas = new Texture2D(
                    2048, // Larger than our texture
                    2048,
                    texture.format,
                    true);

                // Fill with transparent pixels
                var clearPixels = spriteAtlas.GetPixels();
                for (int i = 0; i < clearPixels.Length; i++)
                    clearPixels[i] = Color.clear;
                spriteAtlas.SetPixels(clearPixels);

                // Copy our texture into atlas with offset
                int offset = 100; // Give some padding
                spriteAtlas.SetPixels(
                    offset, 
                    offset, 
                    texture.width, 
                    texture.height, 
                    texture.GetPixels());
                spriteAtlas.Apply(true);

                Sprite sprite = Sprite.Create(
                    spriteAtlas,
                    new Rect(offset, offset, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    Vector4.zero,
                    false
                );

                // Set explicit name
                sprite.name = $"custom_profile_pic_{DateTime.Now.Ticks}";
        
                Logger.LogVerbose($"Created sprite in atlas: name={sprite.name}, rect={sprite.rect}, offset=({offset},{offset})");
                return sprite;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error converting Texture2D to Sprite: {e.Message}\nStack trace: {e.StackTrace}");
                return null;
            }
        }

        public static Sprite ConvertBase64ToSprite(this string base64)
        {
            Logger.LogVerbose("Starting ConvertBase64ToSprite");
            var texture = base64.ConvertBase64ToTexture2D();
            if (texture == null)
            {
                Logger.LogError("Failed to convert base64 to texture");
                return null;
            }

            Logger.LogVerbose($"Successfully created texture, converting to sprite");
            var sprite = texture.ConvertTextureToSprite();
            if (sprite == null)
            {
                Logger.LogError("Failed to convert texture to sprite");
            }
            else
            {
                Logger.LogVerbose($"Successfully created sprite of size {sprite.rect.width}x{sprite.rect.height}");
            }

            // Clean up the temporary texture
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            return sprite;
        }


        private static Texture2D EnsureTextureIsReadable(Texture2D texture)
        {
            if (texture.isReadable)
            {
                return texture;
            }

            // Create a readable copy of the texture
            RenderTexture tmp = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear
            );

            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D readableTexture = new Texture2D(texture.width, texture.height);
            readableTexture.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            readableTexture.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);

            return readableTexture;
        }
    }
}
