using System;
using Unity.Services.Core;
using Unity.Services.Mediation;
using UnityEngine;

namespace Unity.Services.Samples.RewardedAds
{
    public class MediationManager : MonoBehaviour
    {
        public static MediationManager instance { get; private set; }

        public RewardedAdsSceneManager sceneManager;

        public bool isAdReady { get; private set; }

        const int k_MaxAdLoadAttempts = 5;
        const string k_AndroidAdUnitId = "RewardedAds_Android";
        const string k_IOSAdUnitId = "RewardedAds_iOS";

        IRewardedAd m_RewardedAd;
        int m_BonusRewardMultiplier;
        int m_AdLoadAttempts;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
            }
            else
            {
                instance = this;
            }
        }

        public void LoadRewardedAd()
        {
            // Mediation supports Android and iOS builds. In editor it will always show a fake ad, regardless of
            // the AdUnitId passed to the RewardedAd constructor. For all other platforms it will instantiate an
            // UnsupportedRewardedAd type when the RewardedAd constructor is called. This allows the rest of your
            // mediation code to be written in a platform agnostic manner without having to worry about which
            // platform your building for.
            //
            // Here we instantiate a rewarded ad object with different Ad Unit Ids for Android and iOS.
            // Alternatively, you could give both platforms' Ad Ids the same name, then you would not need
            // the platform check.
            if (Application.platform == RuntimePlatform.Android)
            {
                m_RewardedAd = new RewardedAd(k_AndroidAdUnitId);
            }
            else
            {
                m_RewardedAd = new RewardedAd(k_IOSAdUnitId);
            }

            m_RewardedAd.OnLoaded += OnAdLoaded;
            m_RewardedAd.OnFailedLoad += OnAdFailedToLoad;
            m_RewardedAd.OnShowed += OnAdShown;
            m_RewardedAd.OnFailedShow += OnAdFailedToShow;
            m_RewardedAd.OnUserRewarded += OnUserRewarded;
            m_RewardedAd.OnClosed += OnAdClosed;
            LoadAdIfNotTooManyAttempts();
        }

        void LoadAdIfNotTooManyAttempts()
        {
            // MaxAdLoadAttempts is an arbitrary number of attempts, in place to prevent an infinite loop of
            // attempting to load and then failing to load in cases where the issue faced is not transient.
            if (m_AdLoadAttempts < k_MaxAdLoadAttempts)
            {
                m_AdLoadAttempts++;
                m_RewardedAd.LoadAsync();
            }
            else
            {
                Debug.Log("Mediation is having trouble loading ads. " +
                    "Too many unsuccessful attempts have been made.");
            }
        }

        void OnAdLoaded(object sender, EventArgs args)
        {
            // The isAdReady flag will signal to the view that it can show a watch rewarded ad button.
            isAdReady = true;

            // Resetting m_AdLoadAttempts since an ad was successfully loaded.
            m_AdLoadAttempts = 0;
        }

        void OnAdFailedToLoad(object sender, LoadErrorEventArgs args)
        {
            // The isAdReady flag will signal to the view that no ad is
            // available and watch ad buttons should be hidden.
            isAdReady = false;
            switch (args.Error)
            {
                case LoadError.SdkNotInitialized:
                    Debug.Log("Mediation SDK failed to initialize properly. " +
                        $"Unity Services State: {UnityServices.State}");
                    break;

                case LoadError.NetworkError:
                    Debug.Log("A network error is preventing mediation from loading ads. Retrying. " +
                        $"Error Message: {args.Message}");
                    LoadAdIfNotTooManyAttempts();
                    break;

                case LoadError.NoFill:
                    Debug.Log($"The ad request could not be filled. Retrying. Error Message: {args.Message}");
                    LoadAdIfNotTooManyAttempts();
                    break;

                case LoadError.Unknown:
                default:
                    Debug.Log("An unknown error occurred while attempting to load an ad. Retrying. " +
                        $"Error Message: {args.Message}");
                    LoadAdIfNotTooManyAttempts();
                    break;
            }
        }

        void OnAdShown(object sender, EventArgs args)
        {
            // Prepare for next ad show by loading a new ad for this id.
            LoadAdIfNotTooManyAttempts();
        }

        async void OnUserRewarded(object sender, RewardEventArgs args)
        {
            try
            {
                await CloudCodeManager.instance.CallGrantLevelEndRewardsEndpoint(false, m_BonusRewardMultiplier);
            }
            catch (Exception e)
            {
                Debug.Log("A problem occurred while trying to reward user for ad watch: " + e);
            }
        }

        void OnAdFailedToShow(object sender, ShowErrorEventArgs args)
        {
            switch (args.Error)
            {
                case ShowError.AdNotLoaded:
                    Debug.Log("There was a problem showing the loaded ad. " +
                        "Loading a new one, please try again.");
                    LoadAdIfNotTooManyAttempts();
                    break;

                case ShowError.AdNetworkError:
                    Debug.Log("There is a problem with the network providing this ad. " +
                        $"Error Message: {args.Message}");
                    break;

                case ShowError.InvalidActivity:
                    Debug.Log("Invalid activity detected when trying to show ad. " +
                        $"Error Message: {args.Message}");
                    break;

                case ShowError.Unknown:
                default:
                    Debug.Log("An unknown error occurred when trying to show your ad. " +
                        $"Error Message: {args.Message}");
                    break;
            }
        }

        void OnAdClosed(object sender, EventArgs e)
        {
            sceneManager.m_IsAdClosed = true;
            Debug.Log("Ad is closed.");
        }

        public void ShowAd(int bonusRewardMultiplier)
        {
            // Ensure the ad has loaded, then show it.
            if (m_RewardedAd.AdState != AdState.Loaded)
            {
                Debug.Log("Attempted to show an ad that wasn't loaded.");
                LoadAdIfNotTooManyAttempts();
                return;
            }

            isAdReady = false;
            m_BonusRewardMultiplier = bonusRewardMultiplier;
            m_RewardedAd.ShowAsync();
        }
    }
}
