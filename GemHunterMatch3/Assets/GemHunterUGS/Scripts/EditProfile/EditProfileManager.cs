using System;
using System.Threading.Tasks;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.Utilities;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.EditProfile
{
    /// <summary>
    /// Manages profile editing functionality, including display name changes and profile picture updates.
    /// Handles both pre-made profile pictures and custom image uploads on Android devices.
    /// </summary>
    /// <remarks>
    /// The image upload functionality is only available on Android platforms and uses a custom
    /// AndroidImageUploader implementation.
    ///
    /// For your own projects, consider using "Native Gallery for Android & iOS" by yasirkula in the Unity Asset Store: 
    /// https://assetstore.unity.com/packages/tools/integration/native-gallery-for-android-ios-112630
    /// </remarks>
    public class EditProfileManager : MonoBehaviour
    {
        #if UNITY_ANDROID
        [SerializeField]
        private AndroidImageUploader m_ImageUploader;
        #endif        

        private PlayerDataManager m_PlayerDataManager;
        private EditProfileUIController m_EditProfileUIController;
        
        public event Action<string> NewCustomProfilePictureSelected;

        private void Start()
        {
            InitializeDependencies();
            SetupEventHandlers();
        }

        private void InitializeDependencies()
        {
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_EditProfileUIController = GetComponent<EditProfileUIController>();
            
            #if UNITY_ANDROID
            m_ImageUploader = GetComponentInChildren<AndroidImageUploader>();
            #endif  
        }

        private void SetupEventHandlers()
        {
            m_EditProfileUIController.SavingProfileEdits += UpdateDisplayName;
            m_EditProfileUIController.NewPremadeProfilePictureSelected += PreparePreMadeProfilePicture;
            m_EditProfileUIController.UploadingCustomImageMobile += StartUploadPictureMobile;
        }

        private void UpdateDisplayName(string displayName)
        {
            if (displayName == null)
            {
                return;
            }
            m_PlayerDataManager.HandleUpdateDisplayName(displayName);
        }
        
        private void PreparePreMadeProfilePicture(Sprite profilePicture, int id)
        {
            var newProfilePicture = new ProfilePicture
            {
                Type = "pre-made",
                ImageData = " ",
                ImageId = id,
            };
                
            UpdatePremadeProfilePicture(newProfilePicture);
        }

        private void UpdatePremadeProfilePicture(ProfilePicture newProfilePicture)
        {
            m_PlayerDataManager.OverwriteProfilePicture(newProfilePicture);
        }

        private async void StartUploadPictureMobile()
        {
            #if UNITY_ANDROID
            await UploadPictureMobile();
            #else
            Logger.LogWarning("Use third-party asset for iOS image uploading");
            #endif
        }

        #if UNITY_ANDROID
        private async Task UploadPictureMobile()
        {
            Logger.LogDemo("[ProfileManager] Starting image upload...");
            var (success, base64Image, errorMessage) = await m_ImageUploader.UploadImage();

            if (!success)
            {
                Logger.LogError($"Failed to upload image: {errorMessage}");
                return;
            }
    
            Logger.LogVerbose($"[ProfileManager] Upload successful, base64 length: {base64Image?.Length}");
            // Log the first 100 chars of base64 string to verify format
            Logger.LogVerbose($"[ProfileManager] Base64 string starts with: {base64Image?.Substring(0, Math.Min(100, base64Image?.Length ?? 0))}");
    
            Sprite newSprite = base64Image.ConvertBase64ToSprite();
            Logger.LogVerbose($"[ProfileManager] Sprite conversion success: {newSprite != null}");
            
            if (newSprite == null)
            {
                Logger.LogError("Failed to convert image to sprite");
                return;
            }
            
            var newProfilePicture = new ProfilePicture
            {
                Type = "custom",
                ImageData = base64Image,
                ImageId = 0,
            };
            Logger.LogDemo("⚡ New profile picture uploaded");
            m_PlayerDataManager.OverwriteProfilePicture(newProfilePicture);
            NewCustomProfilePictureSelected?.Invoke(base64Image);
        }
        #endif

        private void OnDisable()
        {
            if (m_EditProfileUIController == null)
            {
                return;
            }
            
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            m_EditProfileUIController.SavingProfileEdits -= UpdateDisplayName;
            m_EditProfileUIController.NewPremadeProfilePictureSelected -= PreparePreMadeProfilePicture;
            m_EditProfileUIController.UploadingCustomImageMobile -= StartUploadPictureMobile;
        }
    }
}
