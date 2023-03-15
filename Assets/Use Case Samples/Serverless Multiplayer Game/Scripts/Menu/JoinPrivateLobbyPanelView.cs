using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class JoinPrivateLobbyPanelView : PanelViewBase
    {
        const int k_GameCodeLength = 6;

        readonly Regex k_AlphaNumeric = new Regex("^[A-Z0-9]*$");

        [SerializeField]
        TMP_InputField gameCodeInputField;

        [SerializeField]
        TextMeshProUGUI invalidGameCodeText;

        [SerializeField]
        Button joinButton;

        public string gameCode => gameCodeInputField.text;

        public bool isGameCodeValid => gameCode.Length == k_GameCodeLength && k_AlphaNumeric.IsMatch(gameCode);

        void Start()
        {
            gameCodeInputField.onValidateInput += (input, charIndex, addedChar) => char.ToUpper(addedChar);
        }

        public void ClearGameCode()
        {
            gameCodeInputField.text = "";
        }

        public void OnJoinPrivateCodeChanged()
        {
            UpdateJoinButton();
        }

        public override void SetInteractable(bool isInteractable)
        {
            base.SetInteractable(isInteractable);

            // This is updated after the base class sets the default state
            // so we can disable the Join button if the join code is invalid.
            UpdateJoinButton();
        }

        void UpdateJoinButton()
        {
            joinButton.interactable = isInteractable && isGameCodeValid;

            invalidGameCodeText.enabled = !k_AlphaNumeric.IsMatch(gameCode);
        }
    }
}
