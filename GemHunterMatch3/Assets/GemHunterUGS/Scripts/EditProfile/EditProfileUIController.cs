using System;
using System.Collections;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerDataManagement;
using GemHunterUGS.Scripts.PlayerHub;
using Unity.Services.CloudCode.GeneratedBindings.GemHunterUGSCloud.Models;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.EditProfile
{
    /// <summary>
    /// Handles the UI and logic for editing a player's profile, including display name and profile picture.
    /// </summary>
    public class EditProfileUIController : MonoBehaviour
    {
        private RandomProfilePicturesSO m_RandomProfilePictures;
        
        [SerializeField]
        private EditProfileView m_EditProfileView;
        [SerializeField]
        private HubUIController m_HubUIController;
        
        private PlayerDataManager m_PlayerDataManager;
        
        public event Action UploadingCustomImageMobile;
        public event Action<Sprite, int> NewPremadeProfilePictureSelected;
        public event Action<string> SavingProfileEdits;
        
        private int m_SelectedImageId;
        private Action[] m_ProfilePictureHandlers;
        private float m_KeyboardOffset = -325f;
        
        private void Start()
        {
            m_PlayerDataManager = GameSystemLocator.Get<PlayerDataManager>();
            m_RandomProfilePictures = m_PlayerDataManager.RandomProfilePicturesSO;
            
            m_EditProfileView.Initialize();
            SetupEventHandlers();
        }
        
        private void SetupEventHandlers()
        {
            m_EditProfileView.CloseEditProfileButton.clicked += HandleCloseEditProfile;
            m_EditProfileView.CloseEditProfileFromPictureSelectionButton.clicked += HandleCloseEditProfile;
            
            m_EditProfileView.UploadPictureButton.clicked += UploadCustomImage;
            m_EditProfileView.RandomPictureButton.clicked += HandlePremadePicturePanelSelected;
            m_EditProfileView.SelectPictureDoneButton.clicked += HandleSelectPremadePictureComplete;
            m_EditProfileView.SelectPictureCancelButton.clicked += HandleCancelSelectPremadePicture;
            
            m_EditProfileView.NameEditDoneButton.clicked += HandleNameEditDone;
            m_EditProfileView.EditUsernameButton.clicked += HandleOpenEditUsername;
            m_EditProfileView.NameEditCancelButton.clicked += HandleCloseEditUsername;
            
            m_PlayerDataManager.ProfilePictureUpdated += UpdatePlayerProfilePictureUI;
            m_PlayerDataManager.LocalPlayerDataUpdated += HandlePlayerDataUpdate;
            
            m_EditProfileView.DisplayNameTextField.RegisterValueChangedCallback(evt => HandleDisplayNameChanged(evt.newValue));
            
            SetupProfilePictureButtons();
        }

        private void HandleDisplayNameChanged(string newDisplayName)
        {
            if (IsDisplayNameValid(newDisplayName))
            {
                m_EditProfileView.HidePopUpForNameRequirement();
                Logger.Log($"New name entered: {newDisplayName}");
            }
            else 
            {
                m_EditProfileView.ShowPopUpForNameRequirement();
            }
        }
        
        private void SetupProfilePictureButtons()
        {
            m_ProfilePictureHandlers = new Action[m_EditProfileView.ProfilePictureButtons.Count];

            for (int i = 0; i < m_EditProfileView.ProfilePictureButtons.Count; i++)
            {
                var button = m_EditProfileView.ProfilePictureButtons[i];
                var buttonIndex = i;
                button.style.backgroundImage = new StyleBackground(m_RandomProfilePictures.ProfilePictures[buttonIndex]);
                m_ProfilePictureHandlers[buttonIndex] = () => HandleSelectPremadePicture(buttonIndex);
                
                button.clicked += m_ProfilePictureHandlers[buttonIndex];
            }
        }
        
        public void OpenEditProfile()
        {
            Sprite profilePicture = m_PlayerDataManager.ProfileSprite;
            string displayName = m_PlayerDataManager.PlayerDataLocal.DisplayName;
            string playerId = m_PlayerDataManager.PlayerId;
            
            if (profilePicture == null)
            {
                Logger.LogWarning($"No profile picture saved on PlayerDataManager");
            }
            
            m_EditProfileView.SetProfilePicture(m_PlayerDataManager.ProfileSprite);
            m_EditProfileView.UpdateProfileInfo(displayName, playerId);
            
            m_SelectedImageId = m_PlayerDataManager.ProfilePictureData.ImageId;
            m_EditProfileView.IndicatePictureSelectedCheck(m_SelectedImageId);
            m_EditProfileView.OpenEditProfile();
        }
        
        private void HandleCloseEditProfile()
        {
            Logger.LogDemo("Closing edit profile");
            m_EditProfileView.CloseEditProfile();
            m_HubUIController.ShowMainHub();
        }
        
        private void HandleOpenEditUsername()
        {
            m_EditProfileView.ShowEditUsername();
            m_EditProfileView.SetKeyboardOffset(m_KeyboardOffset);
        }
        
        private void HandleCloseEditUsername()
        {
            m_EditProfileView.ResetKeyboardOffset();
            m_EditProfileView.ShowEditProfile();
        }
        
        
        private void UploadCustomImage()
        {
            #if UNITY_ANDROID
                UploadingCustomImageMobile?.Invoke();
                return;
            #endif
            
            Logger.LogWarning("Use third party asset for uploading custom image on iOS");
        }
        
        private void HandlePremadePicturePanelSelected()
        {
            m_EditProfileView.ShowSelectPremadePicturePanel();
        }
        
        private void HandleSelectPremadePicture(int index)
        {
            if (index >= 0 && index < m_EditProfileView.ProfilePictureButtons.Count)
            {
                m_SelectedImageId = index;
                m_EditProfileView.SetProfilePicture(m_RandomProfilePictures.ProfilePictures[m_SelectedImageId]);
                m_EditProfileView.IndicatePictureSelectedCheck(m_SelectedImageId);
            }
            else
            {
                Logger.LogWarning($"Invalid profile picture index: {index}");
            }
        }

        private void HandleSelectPremadePictureComplete()
        {
            Sprite newProfilePic = m_RandomProfilePictures.ProfilePictures[m_SelectedImageId];
            m_EditProfileView.ShowEditProfile();
            NewPremadeProfilePictureSelected?.Invoke(newProfilePic, m_SelectedImageId);
        }

        private void HandleCancelSelectPremadePicture()
        {
            // Back to picture options
            m_EditProfileView.CloseSelectPremadePicturePanel();
            m_EditProfileView.ShowEditProfile();
        }
        
        private void HandleNameEditDone()
        {
            string newName = m_EditProfileView.DisplayNameTextField.value;
            if (IsDisplayNameValid(newName))
            {
                SavingProfileEdits?.Invoke(newName);
                m_EditProfileView.HidePopUpForNameRequirement();
                m_HubUIController.ShowPopUpTimed($"Hi {newName}, your profile has been updated!");
                HandleCloseEditProfile();
            }
            else
            {
                m_EditProfileView.ShowPopUpForNameRequirement();
                Logger.LogWarning("Invalid name length. Please enter a name between 4 and 16 characters.");
            }
        }

        private bool IsDisplayNameValid(string newName)
        {
            return newName.Length is >= 4 and <= 16;
        }
        
        private void UpdatePlayerProfilePictureUI(Sprite sprite)
        {
            if (sprite == null)
            {
                Logger.LogWarning("Received null sprite in UpdatePlayerProfilePictureUI");
                return;
            }
            m_EditProfileView.SetProfilePicture(sprite);
        }

        private void HandlePlayerDataUpdate(PlayerData playerData)
        {
            m_EditProfileView.UpdateProfileInfo(playerData.DisplayName, m_PlayerDataManager.PlayerId);
        }

        private void OnDisable()
        {
            RemoveManagerEventHandlers();
            RemoveUIEventHandlers();
            RemoveProfilePictureButtonHandlers();
        }

        private void RemoveManagerEventHandlers()
        {
            if (m_PlayerDataManager == null)
            {
                return;
            }
            
            m_PlayerDataManager.ProfilePictureUpdated -= UpdatePlayerProfilePictureUI;
            m_PlayerDataManager.LocalPlayerDataUpdated -= HandlePlayerDataUpdate;
        }
        
        private void RemoveUIEventHandlers()
        {
            // Prevents unnecessary subscription errors if PlayerHub scene is loaded first
            if (m_EditProfileView == null || m_EditProfileView.CloseEditProfileButton == null)
            {
                return;
            }
            
            m_EditProfileView.CloseEditProfileButton.clicked -= HandleCloseEditProfile;
            m_EditProfileView.CloseEditProfileFromPictureSelectionButton.clicked -= HandleCloseEditProfile;
            
            m_EditProfileView.UploadPictureButton.clicked -= UploadCustomImage;
            m_EditProfileView.RandomPictureButton.clicked -= HandlePremadePicturePanelSelected;
            m_EditProfileView.SelectPictureDoneButton.clicked -= HandleSelectPremadePictureComplete;
            m_EditProfileView.SelectPictureCancelButton.clicked -= HandleCancelSelectPremadePicture;
            
            m_EditProfileView.NameEditDoneButton.clicked -= HandleNameEditDone;
            m_EditProfileView.EditUsernameButton.clicked -= HandleOpenEditUsername;
            m_EditProfileView.NameEditCancelButton.clicked -= HandleCloseEditUsername;
        }
        
        private void RemoveProfilePictureButtonHandlers()
        {
            if (m_ProfilePictureHandlers == null) return;

            for (int i = 0; i < m_EditProfileView.ProfilePictureButtons.Count; i++)
            {
                if (m_ProfilePictureHandlers[i] != null)
                {
                    m_EditProfileView.ProfilePictureButtons[i].clicked -= m_ProfilePictureHandlers[i];
                }
            }
            
            m_ProfilePictureHandlers = null;
        }
    }
}
