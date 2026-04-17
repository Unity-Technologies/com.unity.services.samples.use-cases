
using System;
using System.Threading.Tasks;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.EditProfile
{
    /// <summary>
    /// Handles image upload functionality for Android platform, communicating with a native
    /// Android activity for image selection and processing.
    /// </summary>
    /// <remarks>
    /// This class interfaces with the custom ImagePickerActivity in the Android native code.
    /// It provides a Task-based interface for image selection with timeout handling and
    /// proper cleanup of native resources.
    ///
    /// Note: Detailed debug logging is maintained in this custom implementation to aid in
    /// troubleshooting -- this is an in-house solution that won't be maintained like a third-party package.
    ///
    /// Consider using "Native Gallery for Android & iOS" by yasirkula in the Unity Asset Store: 
    /// https://assetstore.unity.com/packages/tools/integration/native-gallery-for-android-ios-112630
    /// </remarks>
    public class AndroidImageUploader : MonoBehaviour
    {
        #if UNITY_ANDROID
        private static bool s_IsAndroid;
        private TaskCompletionSource<string> m_ImageSelectionTask;

        private const int k_TimeoutMinutes = 2;
        private const string k_ImagePickerActivityPath = "com.GemHunter.imagepicker.ImagePickerActivity";
        
        private void Awake()
        {
            s_IsAndroid = Application.platform == RuntimePlatform.Android;
        }
        
        /// <summary>
        /// Initiates the image upload process from the device gallery
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// - success: Whether the upload was successful
        /// - base64Image: The image data as a base64 string if successful
        /// - errorMessage: Error details if the upload failed
        /// </returns>
        public async Task<(bool success, string base64Image, string errorMessage)> UploadImage()
        {
            if (!s_IsAndroid)
            {
                return (false, null, "Not running on Android platform");
            }

            try
            {
                string base64Image = await SelectImageFromGallery();
                if (string.IsNullOrEmpty(base64Image))
                {
                    return (false, null, "No image selected");
                }

                return (true, base64Image, null);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in UploadImage: {e}");
                return (false, null, $"Upload failed: {e.Message}");
            }
        }

        /// <summary>
        /// Launches the native Android image picker activity and waits for a result
        /// </summary>
        /// <returns>The selected image as a base64 string, or null if selection failed</returns>
        private async Task<string> SelectImageFromGallery()
        {
            m_ImageSelectionTask = new TaskCompletionSource<string>();

            try
            {
                // Logger.Log("[ImageUpload] Starting image selection...");
                LaunchNativeImagePicker();
                
                // Logger.Log("[ImageUpload] Waiting for result...");
                return await WaitForImageSelection();
            }
            catch (Exception e)
            {
                Logger.LogError($"[ImageUpload] Error: {e.Message}\nStack trace: {e.StackTrace}");
                m_ImageSelectionTask.TrySetException(e);
                return null;
            }
        }
        
        private void LaunchNativeImagePicker()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", 
                    currentActivity, 
                    new AndroidJavaClass(k_ImagePickerActivityPath));
                currentActivity.Call("startActivity", intent);
            }
        }
        
        private async Task<string> WaitForImageSelection()
        {
            using (var timeoutCancellationSource = new System.Threading.CancellationTokenSource())
            {
                var completedTask = await Task.WhenAny(
                    m_ImageSelectionTask.Task,
                    Task.Delay(TimeSpan.FromMinutes(k_TimeoutMinutes), timeoutCancellationSource.Token)
                );

                if (completedTask != m_ImageSelectionTask.Task)
                {
                    Logger.LogError("[ImageUpload] Image selection timed out");
                    m_ImageSelectionTask.TrySetCanceled();
                    return null;
                }

                timeoutCancellationSource.Cancel();
                var result = await m_ImageSelectionTask.Task;
                Logger.Log($"[ImageUpload] Got base64 image, length: {result?.Length ?? 0}");
                return result;
            }
        }
        
        /// <summary>
        /// Callback method invoked by the native Android code when an image is selected
        /// </summary>
        /// <param name="base64Image">The selected image encoded as a base64 string</param>
        public void OnImageSelected(string base64Image)
        {
            // Logger.Log($"<[ImageUpload] OnImageSelected called with base64 length: {base64Image?.Length ?? 0}");
            if (m_ImageSelectionTask != null && !m_ImageSelectionTask.Task.IsCompleted)
            {
                m_ImageSelectionTask.SetResult(base64Image);
                // Logger.Log($"[ImageUpload] Image selection task completed");
            }
            else
            {
                Logger.LogWarning($"ImageUpload] OnImageSelected called but task is null or already completed");
            }
        }
        #endif
    }
}
