using System;
using UnityEngine;
using Unity.Services.LevelPlay;
using Unity.Services.Authentication;
using GemHunterUGS.Scripts.Core;
using GemHunterUGS.Scripts.PlayerEconomyManagement;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Ads
{
    /// <summary>
    /// Manages rewarded video ads using the LevelPlay SDK, handling initialization,
    /// ad state management, and reward validation through cloud functions
    /// </summary>
public class AdRewardManagerClient : MonoBehaviour
{
    private const string k_AndroidAppKey = "enterappkey";
    private const string k_AppleAppKey = "enterappid";
    
    // Cooldown between rewarded ad completions: provides UX pacing, ensures another ad is ready if user wants
    private const float k_RewardCooldownSeconds = 3;
    
    // Dependencies
    
    private PlayerEconomyManager m_PlayerEconomyManager;
    private PlayerEconomyManagerClient m_PlayerEconomyClient;
    private CloudBindingsProvider m_BindingsProvider;
    
    // Anti-cheat tracking

    private string m_LastAdToken;
    private DateTime m_LastAdCompletionTime;
    
    // Ad Unit/Placement 

    [SerializeField]
    private string m_AdUnitIdAndroid = "xxxx";
    [SerializeField]
    private string m_AdUnitIdApple = "xxx";

    private int m_LoadRetryCount = 0;
    private const int k_MaxRetryAttempts = 3;
    private const float k_RetryDelaySeconds = 5f;
    
    // Runtime State

    private bool m_IsInitialized;
    private LevelPlayRewardedAd m_RewardedAd;
    
    // Convenience events for UI updates
    public event Action<bool> AdSuccessfullyCompleted;
    public event Action<bool> AdAvailable;
    
    private void OnEnable()
    {
        RegisterSDKEvents();
    }
    
    private void Start()
    {
        m_BindingsProvider = GameSystemLocator.Get<CloudBindingsProvider>();
        m_PlayerEconomyClient = GameSystemLocator.Get<PlayerEconomyManagerClient>();
        m_PlayerEconomyManager = GameSystemLocator.Get<PlayerEconomyManager>();
        
        AuthenticationService.Instance.SignedIn += InitializeAds;
        
        if (AuthenticationService.Instance.IsSignedIn && !m_IsInitialized)
        {
            InitializeAds();
        }
    }
    
    #region SDK Initialization
    
    private void RegisterSDKEvents()
    {
        LevelPlay.OnInitSuccess += OnSDKInitialized;
        LevelPlay.OnInitFailed += OnSDKInitializationFailed;
    }
    
    /// <summary>
    /// Initializes the ad SDK with the appropriate platform app key.
    /// </summary>
    private void InitializeAds()
    {
        string appKey = GetPlatformAppKey();
        
        string userId = AuthenticationService.Instance.PlayerId;

        // Only if you want to use test suite, launched in OnSDKInitialized
        LevelPlay.SetMetaData("is_test_suite", "enable");

        // Initialize SDK - this will trigger OnSDKInitialized callback
        // Note: Rewarded ads require legacy format specification during init
        com.unity3d.mediation.LevelPlayAdFormat[] legacyAdFormats = new[] { com.unity3d.mediation.LevelPlayAdFormat.REWARDED };
        LevelPlay.Init(appKey, userId, legacyAdFormats);

        LevelPlay.SetPauseGame(true);
    }
    
    /// <summary>
    /// Gets the appropriate app key based on the current platform.
    /// </summary>
    /// <returns>The platform-specific app key for IronSource</returns>
    private string GetPlatformAppKey()
    {
        #if UNITY_ANDROID
            return k_AndroidAppKey;
        #elif UNITY_IPHONE
            return k_AppleAppKey;
        #else
            Logger.LogWarning("Unexpected platform for ads");
            return "unexpected_platform";
        #endif
    }
    
    /// <summary>
    /// Callback when the LevelPlay SDK is initialized successfully.
    /// Creates the rewarded ad object, registers to ad events, and loads the first ad.
    /// </summary>
    private void OnSDKInitialized(LevelPlayConfiguration configuration)
    {
        if (m_IsInitialized)
        {
            Logger.LogWarning("SDK already initialized");
            return;
        }

        m_IsInitialized = true;
        Logger.LogDemo("LevelPlay SDK initialized successfully");

#if DEVELOPMENT_BUILD
        LaunchTestSuite();
        Logger.Log("Launching test suite");
#endif
        m_LoadRetryCount = 0;
        
        CreateRewardedAd();
        RegisterToAdEvents();
        LoadRewardedAd();
    }
    
    /// <summary>
    /// Opens the test suite for debugging ad integration.
    /// Only used in editor or development builds.
    /// </summary>
    private void LaunchTestSuite()
    {
        LevelPlay.LaunchTestSuite();
    }
    
    /// <summary>
    /// Callback when the LevelPlay SDK initialization fails.
    /// </summary>
    private void OnSDKInitializationFailed(LevelPlayInitError error)
    {
        Logger.LogError($"LevelPlay SDK initialization failed: {error.ErrorMessage}");
    }
    
    public bool IsRewardedVideoAvailable()
    {
        bool isAvailable = m_RewardedAd.IsAdReady();
        Logger.LogDemo($"Checking rewarded video availability: {isAvailable}");
        return isAvailable;
    }
    
    #endregion

    #region Ad Management
    
    /// <summary>
    /// Creates the rewarded ad object and registers to its events.
    /// </summary>
    private void CreateRewardedAd()
    {
#if UNITY_ANDROID
        m_RewardedAd = new LevelPlayRewardedAd(m_AdUnitIdAndroid);
        return;
#endif
#if UNITY_IOS
        m_RewardedAd = new LevelPlayRewardedAd(m_AdUnitIdAndroid);
        return;
#endif
        Logger.LogWarning("Platform not supported for rewarded ads");
    }

    /// <summary>
    /// Registers to all the rewarded ad events.
    /// </summary>
    private void RegisterToAdEvents()
    {
        // Load events
        m_RewardedAd.OnAdLoaded += HandleAdLoadedSuccessfully;
        m_RewardedAd.OnAdLoadFailed += HandleAdLoadFailed;

        // Display events
        m_RewardedAd.OnAdDisplayed += HandleAdDisplayed;
        m_RewardedAd.OnAdDisplayFailed += HandleAdFailedToDisplay;

        // Reward event
        m_RewardedAd.OnAdRewarded += ProcessAdReward;

        // Completion events
        m_RewardedAd.OnAdClosed += HandleAdClosed;
        m_RewardedAd.OnAdClicked += HandleAdClicked;
        m_RewardedAd.OnAdInfoChanged += HandleAdInfoChanged;
    }
    
    /// <summary>
    /// Loads a rewarded ad. Note: When we call LoadRewardedAd(), we're saying "try the whole waterfall"
    /// </summary>
    private void LoadRewardedAd()
    {
        if (m_RewardedAd != null)
        {
            m_RewardedAd.LoadAd();
        }
    }
    
    /// <summary>
    /// Shows a rewarded video ad with optional placement name.
    /// If placementName is null or empty, shows ad without placement tracking.
    /// </summary>
    /// <param name="placementName">Optional placement name for analytics and capping. Can be null.</param>
    private void ShowRewardedAd(string placementName = null)
    {
        if (string.IsNullOrEmpty(placementName))
        {
            Logger.LogDemo("Showing ad without placement");
            m_RewardedAd.ShowAd();
        }
        else
        {
            Logger.LogDemo($"Showing ad with placement: {placementName}");
            m_RewardedAd.ShowAd(placementName);
        }
    }
    #endregion

    #region Public Interface
    /// <summary>
    /// User-facing method to show a rewarded ad when a button is clicked.
    /// Checks availability before showing the ad.
    /// </summary>
    /// <param name="placementName">Optional placement name for analytics and capping. Can be null.</param>
    public void ClickShowAdReward(string placementName = null)
    {
        if (CanShowAd(placementName))
        {
            ShowRewardedAd(placementName);
        }
        else
        {
            Logger.LogWarning($"Cannot show ad for placement: {placementName ?? "default"}");
        }
    }
    
    /// <summary>
    /// Checks if a rewarded ad can currently be shown for the given placement.
    /// </summary>
    /// <param name="placementName">Optional placement name to check for capping. Can be null.</param>
    /// <returns>True if ad can be shown, false otherwise</returns>
    public bool CanShowAd(string placementName = null)
    {
        if (!m_IsInitialized)
        {
            Logger.LogWarning("SDK not initialized");
            return false;
        }

        if (m_RewardedAd == null)
        {
            Logger.LogWarning("Rewarded ad object not created");
            return false;
        }

        bool isAdReady = m_RewardedAd.IsAdReady();
        bool isCooldownExpired = HasCooldownExpired();
        
        // Only check placement capping if a placement name is provided
        bool isPlacementCapped = false;
        if (!string.IsNullOrEmpty(placementName))
        {
            isPlacementCapped = LevelPlayRewardedAd.IsPlacementCapped(placementName);
            if (isPlacementCapped)
                Logger.LogWarning($"Placement '{placementName}' has reached its capping limit");
        }

        if (!isAdReady)
            Logger.LogWarning("Ad not ready - still loading or no inventory available");

        return isAdReady && isCooldownExpired && !isPlacementCapped;
    }
    #endregion
    
     #region Helper Methods

    /// <summary>
    /// Gets remaining cooldown time in seconds, or 0 if cooldown expired
    /// </summary>
    public float GetRemainingCooldownSeconds()
    {
        if (m_LastAdCompletionTime == default)
            return 0f;

        TimeSpan timeSinceLastAd = DateTime.UtcNow - m_LastAdCompletionTime;
        float remaining = k_RewardCooldownSeconds - (float)timeSinceLastAd.TotalSeconds;
        return Math.Max(0f, remaining);
    }

    /// <summary>
    /// Simple cooldown unrelated to capping/pacing.
    /// </summary>
    /// <returns>True for cooldown expired, false if still in cooldown</returns>
    private bool HasCooldownExpired()
    {
        float remaining = GetRemainingCooldownSeconds();

        if (remaining > 0f)
        {
            Logger.Log($"Ad still on cooldown for {remaining:F1} seconds");
            return false;
        }

        return true;
    }

    #endregion
    
    #region Ad Event Callbacks

    // Load events

    private void HandleAdLoadedSuccessfully(LevelPlayAdInfo adInfo)
    {
        // Reset retry counter on successful load
        m_LoadRetryCount = 0;
        
        AdAvailable?.Invoke(true);
        Logger.Log($"Rewarded ad loaded: {adInfo.AdNetwork}");
    }

    private void HandleAdLoadFailed(LevelPlayAdError error)
    {
        Logger.LogError($"Rewarded ad failed to load: {error.ErrorMessage} (Code: {error.ErrorCode})");

        // Increment retry counter
        m_LoadRetryCount++;
        
        if (m_LoadRetryCount >= k_MaxRetryAttempts)
        {
            Logger.LogWarning($"Max retry attempts ({k_MaxRetryAttempts}) reached. Stopping ad loading.");
            AdAvailable?.Invoke(false);
            return;
        }
        
        // Different retry strategies based on actual LevelPlay error codes
        // Note: Some error codes may not apply to format - https://developers.is.com/ironsource-mobile/air/supersonic-sdk-error-codes/
        switch (error.ErrorCode)
        {
            case 509: // Tried waterfall, all networks say "no inventory"
                Logger.LogWarning($"No ads to show, retrying in {k_RetryDelaySeconds}s (attempt {m_LoadRetryCount}/{k_MaxRetryAttempts})");
                Invoke(nameof(LoadRewardedAd), k_RetryDelaySeconds); // Longer delay for no inventory
                break;

            case 520:
                Logger.LogWarning("No internet connection");
                AdAvailable?.Invoke(false);
                // Could implement connectivity-based retry logic here
                break;

            case 524:
                Logger.LogWarning("Placement is capped, will not retry loading");
                AdAvailable?.Invoke(false);
                // Don't retry - placement is capped
                break;

            case 526:
                Logger.LogWarning("Ad unit has reached daily cap, will not retry");
                AdAvailable?.Invoke(false);
                // Don't retry - daily cap reached
                break;

            // 1022: Cannot show an Rewarded Video (RV) while another RV is showing
            // 1023: Show RV called when there are no available ads to show, check IsAdReady before calling ShowAd

            default:
                Logger.LogWarning($"Unknown error {error.ErrorCode}, retrying in 2s (attempt {m_LoadRetryCount}/{k_MaxRetryAttempts})");
                Invoke(nameof(LoadRewardedAd), 2f); // Standard retry
                break;
        }
    }

    // Display events

    private void HandleAdDisplayed(LevelPlayAdInfo adInfo)
    {
        Logger.Log("Rewarded ad displayed");
    }

    private void HandleAdFailedToDisplay(LevelPlayAdDisplayInfoError error)
    {
        Logger.LogError($"Rewarded ad failed to display: {error}");
        AdSuccessfullyCompleted?.Invoke(false);
    }

    // Reward Events

    private async void ProcessAdReward(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        try
        {
            Logger.LogDemo($"🎁 Validating ad for reward: {reward.Name} reward amount: {reward.Amount}");
            
            // Create a unique token for this ad view
            string adToken = GenerateAdToken(adInfo, reward);
            DateTime completionTime = DateTime.UtcNow;

            // Update tracking variables (for client-side validation if needed)
            m_LastAdToken = adToken;
            m_LastAdCompletionTime = completionTime;

            // Call cloud code to validate and grant reward
            // This prevents client-side reward manipulation
            var playerEconomyData = await m_BindingsProvider.GemHunterBindings.HandleGrantVideoAdReward(adToken);

            m_PlayerEconomyClient.HandleEconomyUpdate(playerEconomyData);

            // TODO: Pass reward info to event for UI display (coins earned popup)
            AdSuccessfullyCompleted?.Invoke(true);

            Logger.LogDemo($"Ad reward granted successfully: {reward.Name} x{reward.Amount}");
        }
        catch (Exception e)
        {
            Logger.LogWarning($"Failed to validate ad reward: {e.Message}");
            AdSuccessfullyCompleted?.Invoke(false);
        }
    }

    /// <summary>
    /// Generates a unique validation token for this ad completion event.
    /// While not cryptographically secure, this token system prevents simple 
    /// replay attacks and enables server-side validation of ad completion timing.
    /// </summary>
    /// <param name="adInfo">Information about the rewarded ad that was shown</param>
    /// <returns>A unique token string for server validation</returns>
    private string GenerateAdToken(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        // Validate required data is present
        if (adInfo == null)
        {
            throw new ArgumentException("Ad info cannot be null for token generation");
        }

        if (reward == null)
        {
            throw new ArgumentException("Reward info cannot be null for token generation");
        }

        if (string.IsNullOrEmpty(adInfo.InstanceId))
        {
            throw new ArgumentException("Ad instance ID cannot be null or empty for token generation");
        }

        // Create token with ad info, reward data, and timestamp
        string timestamp = DateTime.UtcNow.Ticks.ToString();
        string adData = $"{adInfo.InstanceId}_{adInfo.InstanceName}_{adInfo.AdNetwork}";
        string rewardData = $"{reward.Name}_{reward.Amount}";

        string adToken = $"{timestamp}_{adData}_{rewardData}";

        return adToken;
    }

    // Completion events

    private void HandleAdClosed(LevelPlayAdInfo adInfo)
    {
        Logger.Log("Rewarded ad closed");

        // Load another ad for next time
        LoadRewardedAd();
    }

    private void HandleAdClicked(LevelPlayAdInfo adInfo)
    {
        Logger.Log("Rewarded ad clicked");
    }

    private void HandleAdInfoChanged(LevelPlayAdInfo adInfo)
    {
        Logger.Log($"Updated ad info - Network: {adInfo.AdNetwork}, Instance: {adInfo.InstanceId}");
        // Could trigger UI updates here
    }

    #endregion
    
    // Cleanup

    private void OnDisable()
    {
        RemoveEventHandlers();
    }

    private void RemoveEventHandlers()
    {
        AuthenticationService.Instance.SignedIn -= InitializeAds;

        // Remove SDK level events
        LevelPlay.OnInitSuccess -= OnSDKInitialized;
        LevelPlay.OnInitFailed -= OnSDKInitializationFailed;

        // Remove ad-specific events
        if (m_RewardedAd != null)
        {
            m_RewardedAd.OnAdLoaded -= HandleAdLoadedSuccessfully;
            m_RewardedAd.OnAdLoadFailed -= HandleAdLoadFailed;
            m_RewardedAd.OnAdDisplayed -= HandleAdDisplayed;
            m_RewardedAd.OnAdDisplayFailed -= HandleAdFailedToDisplay;
            m_RewardedAd.OnAdRewarded -= ProcessAdReward;
            m_RewardedAd.OnAdClosed -= HandleAdClosed;
            m_RewardedAd.OnAdClicked -= HandleAdClicked;
            m_RewardedAd.OnAdInfoChanged -= HandleAdInfoChanged;
        }
    }
}
}