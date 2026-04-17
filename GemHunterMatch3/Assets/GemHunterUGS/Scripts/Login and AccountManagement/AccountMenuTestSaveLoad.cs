using System;
using System.Collections.Generic;
using Unity.Services.CloudSave;
using UnityEngine;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class AccountMenuTestSaveLoad : MonoBehaviour
    {
        private AccountManagementView m_View;
        
        private const string k_TestTextKey = "TEST_TEXT_KEY";

        public void Initialize(AccountManagementView view)
        {
            m_View = view;
            SetupEventListeners();
        }

        private void SetupEventListeners()
        {
            m_View.TestSaveButton.clicked += SaveTestText;
            m_View.TestLoadButton.clicked += LoadTestText;
        }

        private async void SaveTestText()
        {
            try
            {
                var textToSave = m_View.TestingTextField.value;
                var data = new Dictionary<string, object>
                {
                    { k_TestTextKey, textToSave }
                };
                
                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                m_View.TestingTextField.value = "Text Saved!";
                
                Logger.LogDemo($"Successfully saved text: {textToSave}");
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to save text: {e.Message}");
            }
        }

        private async void LoadTestText()
        {
            try
            {
                var results = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { k_TestTextKey });

                if (results.TryGetValue(k_TestTextKey, out var item))
                {
                    var loadedText = item.Value.GetAs<string>();
                    m_View.TestingTextField.value = loadedText;
                    Logger.LogDemo($"Successfully loaded text: {loadedText}");
                }
                else
                {
                    Logger.LogWarning("No saved text found");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to load text: {e.Message}");
            }
        }

        private void OnDisable()
        {
            m_View.TestSaveButton.clicked -= SaveTestText;
            m_View.TestLoadButton.clicked -= LoadTestText;
        }
    }
}
