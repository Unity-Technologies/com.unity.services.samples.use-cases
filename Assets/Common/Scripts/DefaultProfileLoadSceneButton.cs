using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples
{
    [RequireComponent(typeof(Button))]
    public class DefaultProfileLoadSceneButton : LoadSceneButton
    {
        const string k_DefaultProfileName = "default";

        void Awake()
        {
            var button = GetComponent<Button>();
            button.onClick.AddListener(OnButtonClick);
        }

        void OnDestroy()
        {
            var button = GetComponent<Button>();
            button.onClick.RemoveListener(OnButtonClick);
        }

        async void OnButtonClick()
        {
            Debug.Log("Restoring default profile login for UGS authentication.");

            AuthenticationService.Instance.SignOut();
            await SwitchProfileToDefault();
            if (this == null) return;

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (this == null) return;

            LoadScene();
            SelectReadmeFileOnProjectWindow();
        }

        static async Task SwitchProfileToDefault()
        {
            AuthenticationService.Instance.SwitchProfile(k_DefaultProfileName);

            var unityAuthenticationInitOptions = new InitializationOptions();
            unityAuthenticationInitOptions.SetProfile(k_DefaultProfileName);
            await UnityServices.InitializeAsync(unityAuthenticationInitOptions);
        }
    }
}
