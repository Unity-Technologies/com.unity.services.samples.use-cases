using System;
using System.Threading.Tasks;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.EditProfile
{
    /// <summary>
    /// Handles cloud communication for profile editing operations, including display name changes
    /// and profile picture updates (both pre-made and custom).
    /// </summary>
    /// <remarks>
    /// This client coordinates with Cloud Code for profile updates while maintaining
    /// local state synchronization. Debug logging is maintained for tracking cloud operations.
    /// </remarks>
    public class EditProfileManagerClient : MonoBehaviour
    {
        [SerializeField]
        private EditProfileManager m_EditProfileManager;
        
        private CloudBindingsProvider m_BindingsProvider;
        private EditProfileUIController m_EditProfileUIController;
        private PlayerDataManagerClient m_PlayerDataManagerClient;
        
        private void Start()
        {
            InitializeDependencies();
            SetupEventHandlers();
        }

        private void InitializeDependencies()
        {
            m_PlayerDataManagerClient = GameSystemLocator.Get<PlayerDataManagerClient>();
            m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
            m_EditProfileUIController = GetComponent<EditProfileUIController>();
        }

        private void SetupEventHandlers()
        {
            m_EditProfileUIController.SavingProfileEdits += StartUpdateDisplayName;
            m_EditProfileUIController.NewPremadeProfilePictureSelected += UpdatePremadeProfilePicture;
            m_EditProfileManager.NewCustomProfilePictureSelected += StartUpdateProfilePictureCustom;
        }
        
        private async void StartUpdateDisplayName(string displayName)
        {
            bool isSuccessful = await UpdateDisplayName(displayName);
            if (isSuccessful)
            {
                m_PlayerDataManagerClient.UpdateDisplayName(displayName);
            }
        }

        private async Task<bool> UpdateDisplayName(string newName)
        {
            try
            {
                await m_BindingsProvider.GemHunterBindings.ChangeDisplayName(newName);
                return true;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error updating display name: {e.Message}");
                return false;
            }
        }

        private void UpdatePremadeProfilePicture(Sprite profilePicture, int imageId)
        {
            UpdateProfilePicturePremade(imageId);
        }
        
        private async void UpdateProfilePicturePremade(int imageId)
        {
            try
            {
                var request = new ProfilePictureChangeRequest()
                {
                    Type = "pre-made",
                    ImageData = " ",
                    ImageId = imageId
                };
                bool isUpdateSuccessful = await m_BindingsProvider.GemHunterBindings.ChangeProfilePicture(request);
                if (isUpdateSuccessful)
                {
                    Logger.Log("pre-made profile picture updated");
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Update premade profile picture failed {e}");
            }
        }

        private async void StartUpdateProfilePictureCustom(string base64Image)
        {
            bool isSuccessful = await UpdateProfilePictureCustom(base64Image);
            if (!isSuccessful)
            {
                Logger.LogWarning($"Error updating user profile picture in cloud. Base64 string length: {base64Image?.Length ?? 0}");
                return;
            }
            Logger.Log("Successfully updated profile picture in cloud");
        }

        private async Task<bool> UpdateProfilePictureCustom(string profilePicture)
        {
            try
            {
                var request = new ProfilePictureChangeRequest()
                {
                    Type = "custom",
                    ImageData = profilePicture,
                    ImageId = 0
                };
                return await m_BindingsProvider.GemHunterBindings.ChangeProfilePicture(request);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error updating profile picture: {e.Message}");
                return false;
            }
        }
        
        private void OnDisable()
        {
            if (m_EditProfileManager == null || m_EditProfileUIController == null)
            {
                return;
            }
            
            RemoveEventHandlers();
        }

        private void RemoveEventHandlers()
        {
            m_EditProfileUIController.SavingProfileEdits -= StartUpdateDisplayName;
            m_EditProfileUIController.NewPremadeProfilePictureSelected -= UpdatePremadeProfilePicture;
            m_EditProfileManager.NewCustomProfilePictureSelected -= StartUpdateProfilePictureCustom;
        }
    }
}
