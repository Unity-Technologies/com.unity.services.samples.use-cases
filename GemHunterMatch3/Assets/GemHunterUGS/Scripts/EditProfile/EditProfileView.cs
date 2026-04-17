using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;
namespace GemHunterUGS.Scripts.EditProfile
{
    /// <summary>
    /// Manages the UI elements and visual states for the profile editing interface.
    /// Handles profile picture display, name editing, and pre-made picture selection panels.
    /// </summary>
    public class EditProfileView : MonoBehaviour
    {
        [SerializeField]
        private Sprite m_ProfilePicPlaceholder;
        [SerializeField]
        private UIDocument m_EditProfileDocument;
        
        // Main Containers
        private VisualElement m_Root;
        private VisualElement m_PlayerDisplayName;
        private VisualElement m_EditProfileParentContainer;
        private VisualElement m_EditProfileContainer;
        private VisualElement m_ChangePictureContainer;
        private VisualElement m_ChangeNameContainer;
        private VisualElement m_SelectPictureContainer;

        // Profile Elements
        public VisualElement ProfilePicture { get; private set; }
        public VisualElement ProfilePictureInSelectPicture { get; private set; }
        private Label m_PlayerNameLabel;
        private Label m_PlayerIdLabel;
        
        // Input
        public TextField DisplayNameTextField { get; private set; }
        
        // Buttons
        public Button CloseEditProfileButton { get; private set; }
        public Button UploadPictureButton { get; private set; }
        public Button RandomPictureButton { get; private set; }
        public Button EditUsernameButton { get; private set; }
        public Button NameEditDoneButton { get; private set; }
        public Button NameEditCancelButton { get; private set; }
        
        // Premade Picture Selection
        public Button CloseEditProfileFromPictureSelectionButton { get; private set; }
        public Button SelectPictureDoneButton { get; private set; }
        public Button SelectPictureCancelButton { get; private set; }
        public List<Button> ProfilePictureButtons { get; private set; }
        private List<VisualElement> m_SelectedChecks;
        
        // Popup
        private VisualElement m_PopUp_ProfileName;
        public Button CloseProfileNamePopUpButton { get; private set; }

        public void Initialize()
        {
            m_EditProfileDocument = GetComponent<UIDocument>();
            m_Root = m_EditProfileDocument.rootVisualElement;
            m_EditProfileParentContainer = m_Root.Q<VisualElement>("EditProfileParentContainer");
            m_EditProfileContainer = m_EditProfileParentContainer.Q<VisualElement>("EditProfileContainer");
            
            ProfilePicture = m_EditProfileContainer.Q<VisualElement>("ProfilePicture");
            m_PlayerNameLabel = m_EditProfileContainer.Q<Label>("PlayerNameLabel");
            m_PlayerIdLabel = m_EditProfileContainer.Q<Label>("PlayerIdLabel");
            
            CloseEditProfileButton = m_EditProfileParentContainer.Q<Button>("CloseButton");
            
            EditUsernameButton = m_EditProfileParentContainer.Q<Button>("EditUsernameButton");
            m_ChangePictureContainer = m_EditProfileParentContainer.Q<VisualElement>("ChangePictureContainer");
            UploadPictureButton = m_EditProfileParentContainer.Q<Button>("UploadPictureButton");
            RandomPictureButton = m_EditProfileParentContainer.Q<Button>("RandomPictureButton");

            m_ChangeNameContainer = m_EditProfileParentContainer.Q<VisualElement>("ChangeNameContainer");
            DisplayNameTextField = m_EditProfileParentContainer.Q<TextField>("PlayerNameTextField");
            NameEditDoneButton = m_EditProfileParentContainer.Q<Button>("NameEditDoneButton");
            NameEditCancelButton = m_EditProfileParentContainer.Q<Button>("NameEditCancelButton");
            
            m_PopUp_ProfileName = m_Root.Q<VisualElement>("PopUp_ProfileName");
            CloseProfileNamePopUpButton = m_Root.Q<Button>("CloseProfileNamePopUpButton");
            
            m_EditProfileParentContainer.style.transitionProperty = new List<StylePropertyName> { "translate" };
            m_EditProfileParentContainer.style.transitionDuration = new List<TimeValue> { new TimeValue(0.25f) };
            m_EditProfileParentContainer.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOut) };
            
            InitializePictureSelection();
            CloseEditProfile();
        }

        private void InitializePictureSelection()
        {
            m_SelectPictureContainer = m_EditProfileParentContainer.Q<VisualElement>("SelectPictureContainer");
            ProfilePictureInSelectPicture = m_SelectPictureContainer.Q<VisualElement>("ProfilePicture");
            CloseEditProfileFromPictureSelectionButton = m_SelectPictureContainer.Q<Button>("CloseButton");
            SelectPictureDoneButton = m_SelectPictureContainer.Q<Button>("SelectPictureDoneButton");
            SelectPictureCancelButton = m_SelectPictureContainer.Q<Button>("SelectPictureCancelButton");
            
            ProfilePictureButtons = new List<Button>();
            m_SelectedChecks = new List<VisualElement>();
            
            for (int i = 1; i <= 9; i++)
            {
                var button = m_SelectPictureContainer.Q<Button>($"ProfilePictureButton{i}");
                if (button != null)
                {
                    ProfilePictureButtons.Add(button);
                    m_SelectedChecks.Add(button.Q<VisualElement>("SelectedCheck"));
                }
                else
                {
                    Logger.LogWarning($"Profile picture button {i} not found in UI Document");
                }
            }
        }
        
        public void OpenEditProfile()
        {
            m_EditProfileParentContainer.style.display = DisplayStyle.Flex;
            ShowEditProfile();
        }

        public void SetProfilePicture(Sprite profilePic)
        {
            if (profilePic == null)
            {
                Logger.LogError("Attempting to set null profile picture");
                return;
            }

            Logger.Log($"SetProfilePicture called - Sprite properties: name={profilePic.name}, rect={profilePic.rect}, " + 
                $"textureNull={profilePic.texture == null}, format={profilePic.texture?.format}, " +
                $"mipmaps={profilePic.texture?.mipmapCount}, filterMode={profilePic.texture?.filterMode}");

            var background = Background.FromSprite(profilePic);
            // Logger.Log($"Background created - IsEmpty={background.IsEmpty()}, texture={background.texture != null}, " +
            //     $"sprite={background.sprite != null}, spriteFormat={background.sprite?.texture?.format}");

            if (ProfilePicture == null)
            {
                Logger.LogError("ProfilePicture VisualElement is null");
                return;
            }

            ProfilePicture.style.backgroundImage = background;
            ProfilePictureInSelectPicture.style.backgroundImage = background;

            Logger.Log($"Background applied to VisualElements. ProfilePicture display={ProfilePicture.style.display.value}");
        }

        public void SetPlaceholderProfilePicture()
        {
            SetProfilePicture(m_ProfilePicPlaceholder);
        }
        
        public void ShowEditProfile()
        {
            m_EditProfileContainer.style.display = DisplayStyle.Flex;
            m_ChangePictureContainer.style.display = DisplayStyle.Flex;
            m_ChangeNameContainer.style.display = DisplayStyle.None;
            m_SelectPictureContainer.style.display = DisplayStyle.None;
            ResetKeyboardOffset();
            HidePopUpForNameRequirement();
        }

        public void ShowSelectPremadePicturePanel()
        {
            m_SelectPictureContainer.style.display = DisplayStyle.Flex;
            m_ChangePictureContainer.style.display = DisplayStyle.None;
            m_EditProfileContainer.style.display = DisplayStyle.None;
            ResetKeyboardOffset();
            HidePopUpForNameRequirement();
        }
        
        public void CloseSelectPremadePicturePanel()
        {
            m_SelectPictureContainer.style.display = DisplayStyle.None;
            m_ChangePictureContainer.style.display = DisplayStyle.None;
            m_EditProfileContainer.style.display = DisplayStyle.Flex;
            ResetKeyboardOffset();
        }
        
        public void ShowEditUsername()
        {
            m_ChangePictureContainer.style.display = DisplayStyle.None;
            m_ChangeNameContainer.style.display = DisplayStyle.Flex;
        }
        
        public void SetKeyboardOffset(float offset)
        {
            m_EditProfileParentContainer.style.translate = new Translate(0, offset);
        }

        public void ResetKeyboardOffset()
        {
            m_EditProfileParentContainer.style.translate = new Translate(0, 0);
        }

        public void IndicatePictureSelectedCheck(int index)
        {
            foreach (var element in m_SelectedChecks)
            {
                element.style.display = DisplayStyle.None;
            }
            m_SelectedChecks[index].style.display = DisplayStyle.Flex;
        }
        
        public void UpdateProfileInfo(string displayName, string playerId)
        {
            DisplayNameTextField.value = displayName;

            m_PlayerNameLabel.text = displayName;
            m_PlayerIdLabel.text = "uid: #" + playerId;
        }
        
        public void CloseEditProfile()
        {
            HidePopUpForNameRequirement();
            ResetKeyboardOffset();
            m_EditProfileContainer.style.display = DisplayStyle.Flex;
            m_ChangePictureContainer.style.display = DisplayStyle.Flex;
            m_SelectPictureContainer.style.display = DisplayStyle.None;
            m_ChangeNameContainer.style.display = DisplayStyle.None;
            m_EditProfileParentContainer.style.display = DisplayStyle.None;
        }

        public void HidePopUpForNameRequirement()
        {
            NameEditDoneButton.SetEnabled(true);
            m_PopUp_ProfileName.style.display = DisplayStyle.None;
        }

        public void ShowPopUpForNameRequirement()
        {
            NameEditDoneButton.SetEnabled(false);
            m_PopUp_ProfileName.style.display = DisplayStyle.Flex;
        }
    }
}
