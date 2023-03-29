using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class ServerlessMultiplayerGameSampleManager : MonoBehaviour
    {
        // Profile name prefix used for unique profiles to permit multiplayer testing using one anonymous account.
        const string k_ProfileNamePrefix = "Profile-";

        public static ServerlessMultiplayerGameSampleManager instance { get; private set; }

        [SerializeField]
        MenuSceneManager menuSceneManager;

        public bool isInitialized { get; private set; }

        // Save off dropdown index so we can restore it on each of the subsequent scenes.
        public int profileDropdownIndex { get; private set; }

        // Reason we returned to the main menu so we can show popup to client if host leaves or player is kicked.
        public enum ReturnToMenuReason
        {
            None,
            PlayerKicked,
            LobbyClosed,
            HostLeftGame
        }

        // Save last reason for returning to the menu. Note that it's stored here because this is one of the few classes
        // that is always present and is responsible for coordinating the user flow for the entire sample.
        public ReturnToMenuReason returnToMenuReason { get; private set; }

        // At the end of the game, we store results here so we can show them when we return to the main menu.
        public bool arePreviousGameResultsSet { get; private set; }

        public GameResultsData previousGameResults { get; private set; }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                instance = this;
            }
        }

        async void Start()
        {
            try
            {
                DontDestroyOnLoad(gameObject);

                ProfanityManager.Initialize();

                var latestProfileIndex = ProfileManager.LookupPreviousProfileIndex();
                await SignInAndInitialize(latestProfileIndex);

                // Check that scene has not been unloaded while processing async wait to prevent throw.
                if (this == null) return;

                await RemoteConfigManager.instance.FetchConfigs();
                if (this == null) return;

                isInitialized = true;

                menuSceneManager.ShowMainMenu();

                Debug.Log("Initialization and signin complete.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task SignInAndInitialize(int profileDropdownIndex = 0)
        {
            try
            {
                this.profileDropdownIndex = profileDropdownIndex;

                // Generate the profile name using the prefix and the dropdown index plus one (1-based indexing)
                var profileName = $"{k_ProfileNamePrefix}{profileDropdownIndex + 1}";
                await AuthenticationManager.SignInAnonymously(profileName, profileDropdownIndex);
                if (this == null) return;

                await CloudSaveManager.instance.LoadAndCacheData();
                if (this == null) return;

                var cloudSavePlayerName = CloudSaveManager.instance.playerStats.playerName;

                // Make sure the player name supplied by Cloud Save is valid (or make it valid now).
                // Note: This is needed when a new profile is used since the player name would not be set.
                await ValidatePlayerName(cloudSavePlayerName);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Task<string> ValidatePlayerName(string playerName)
        {
            try
            {
                if (!ProfanityManager.IsValidPlayerName(playerName))
                {
                    playerName = PlayerNameManager.GenerateRandomName();

                    await CloudSaveManager.instance.SetPlayerName(playerName);
                    if (this == null) return default;

                    Debug.Log($"Initialized playerStats:{CloudSaveManager.instance.playerStats}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return playerName;
        }

        public void SetReturnToMenuReason(ReturnToMenuReason returnToMenuReason)
        {
            this.returnToMenuReason = returnToMenuReason;
        }

        public void ClearReturnToMenuReason()
        {
            this.returnToMenuReason = ReturnToMenuReason.None;
        }

        public void SetPreviousGameResults(GameResultsData results)
        {
            previousGameResults = results;
            arePreviousGameResultsSet = true;
        }

        public void ClearPreviousGameResults()
        {
            arePreviousGameResultsSet = false;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
