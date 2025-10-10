using System;
using System.Collections;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;
using GemHunterUGS.Scripts.Utilities;
using Logger = GemHunterUGS.Scripts.Utilities.Logger;

namespace GemHunterUGS.Scripts.Core
{
    /// <summary>
    /// Monitors and reports network connectivity status for both general internet access and Unity Gaming Services availability.
    /// Provides connectivity state management and error interpretation for network operations.
    /// 
    /// Note: This demo implements basic connectivity handling - production games should implement more robust
    /// offline functionality and recovery strategies using the status information this class provides.
    /// </summary>
    public class NetworkConnectivityHandler : MonoBehaviour, IDisposable
    {
        [SerializeField] private string m_UgsCheckUrl = "https://services.api.unity.com/";
        
        public bool IsOnline => CurrentStatus == ConnectivityStatus.Online;
        public ConnectivityStatus CurrentStatus { get; private set; } = ConnectivityStatus.Online;

        private bool m_LastKnownOnlineState = false;

        private const float m_CheckInterval = 10f;
        private Coroutine m_PeriodicCheck;
        private bool m_IsCheckingConnectivity;
        private WaitForSeconds m_PeriodicCheckWait;
        private const string k_StatusLogEmoji = "🚦";

        /// <summary>
        /// Fired when online/offline status changes. 
        /// Callback receives true when online, false when offline.
        /// </summary>
        public event Action<bool> OnlineStatusChanged;

        public enum ConnectivityStatus
        {
            Online,
            Offline,
            RateLimitExceeded,
            InvalidRequest,
            Forbidden,
            NotFound,
            Conflict,
            TokenExpired,
            InvalidToken,
            RequestRejected,
            UnknownError
        }
        
        private void Start()
        {
            m_PeriodicCheckWait = new WaitForSeconds(m_CheckInterval);
        }
        
        private void SetOnlineStatus(bool isOnline)
        {
            if (isOnline == m_LastKnownOnlineState)
            {
                return;
            }
            
            m_LastKnownOnlineState = isOnline;
            ConnectivityStatus status = isOnline ? ConnectivityStatus.Online : ConnectivityStatus.Offline;
            UpdateConnectivityStatus(status);
            
            HandleConnectivityChange(isOnline);
            
            // Notify other systems of connectivity change
            OnlineStatusChanged?.Invoke(isOnline);
        }
        
        private void HandleConnectivityChange(bool isOnline)
        {
            StopPeriodicOnlineCheck();
            if (!isOnline)
            {
                m_PeriodicCheck = StartCoroutine(PeriodicOnlineCheck());
            }
        }
        
        private void StopPeriodicOnlineCheck()
        {
            m_IsCheckingConnectivity = false;
            if (m_PeriodicCheck != null)
            {
                StopCoroutine(m_PeriodicCheck);
                m_PeriodicCheck = null;
            }
        }
        
        private IEnumerator PeriodicOnlineCheck()
        {
            m_IsCheckingConnectivity = true;
            while (m_IsCheckingConnectivity)
            {
                yield return m_PeriodicCheckWait;
                Logger.LogDemo("NetworkConnectivity: Checking network...");
                
                if (IsOnlineReachable())
                {
                    StartCoroutine(CheckUGSAvailability());
                }
                else
                {
                    SetOnlineStatus(false);
                }
            }
        }
        
        /// <summary>
        /// Reachable != connected -- Note from docs: "Do not use this property to determine the actual connectivity.
        /// For example, the device can be connected to a hot spot, but not have the actual route to the network"
        /// </summary>
        /// <returns>True if the device believes it has a network connection, false otherwise.</returns>
        private bool IsOnlineReachable()
        {
            return Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork;
        }
        
        private IEnumerator CheckUGSAvailability()
        {
            using UnityWebRequest webRequest = UnityWebRequest.Get(m_UgsCheckUrl);
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                SetOnlineStatus(true);
            }
            else
            {
                SetOnlineStatus(false);
            }
        }

        public void HandleNetworkException(Exception ex)
        {
            ConnectivityStatus status;

            if (ex is RequestFailedException requestFailedException)
            {
                status = InterpretHttpError(requestFailedException.ErrorCode);
            }
            else if (ex is TimeoutException)
            {
                status = ConnectivityStatus.Offline;
            }
            else
            {
                status = ConnectivityStatus.UnknownError;
            }

            Logger.LogWarning($"Handling network error: {GetErrorDescription(status)}. Exception: {ex.Message}");

            // Assuming that an error means things are offline...
            SetOnlineStatus(false);
            UpdateConnectivityStatus(status);
        }
        
        private void UpdateConnectivityStatus(ConnectivityStatus status)
        {
            CurrentStatus = status;
            Logger.LogDemo($"{k_StatusLogEmoji} Connectivity status changed to: {GetErrorDescription(status)}");
        }
        
        private ConnectivityStatus InterpretHttpError(long statusCode)
        {
            return statusCode switch
            {
                CommonErrorCodes.TransportError => ConnectivityStatus.Offline,
                CommonErrorCodes.Timeout => ConnectivityStatus.Offline,
                CommonErrorCodes.ServiceUnavailable => ConnectivityStatus.Offline,
                CommonErrorCodes.TooManyRequests => ConnectivityStatus.RateLimitExceeded,
                CommonErrorCodes.InvalidRequest => ConnectivityStatus.InvalidRequest,
                CommonErrorCodes.Forbidden => ConnectivityStatus.Forbidden,
                CommonErrorCodes.NotFound => ConnectivityStatus.NotFound,
                CommonErrorCodes.Conflict => ConnectivityStatus.Conflict,
                CommonErrorCodes.TokenExpired => ConnectivityStatus.TokenExpired,
                CommonErrorCodes.InvalidToken => ConnectivityStatus.InvalidToken,
                CommonErrorCodes.RequestRejected => ConnectivityStatus.RequestRejected,
                CommonErrorCodes.Unknown => ConnectivityStatus.UnknownError,
                _ => ConnectivityStatus.UnknownError
            };
        }

        // Additional helper method to get a description of the error
        private static string GetErrorDescription(ConnectivityStatus status)
        {
            return status switch
            {
                ConnectivityStatus.Online => "Connected to the internet.",
                ConnectivityStatus.Offline => "No internet connection available.",
                ConnectivityStatus.RateLimitExceeded => "Too many requests. Please try again later.",
                ConnectivityStatus.InvalidRequest => "The request was invalid.",
                ConnectivityStatus.Forbidden => "Access to the resource is forbidden.",
                ConnectivityStatus.NotFound => "The requested resource was not found.",
                ConnectivityStatus.Conflict => "There was a conflict with the current state of the resource.",
                ConnectivityStatus.TokenExpired => "The authentication token has expired.",
                ConnectivityStatus.InvalidToken => "The authentication token is invalid.",
                ConnectivityStatus.RequestRejected => "The request was rejected.",
                ConnectivityStatus.UnknownError => "An unknown error occurred.",
                _ => "An unknown error occurred."
            };
        }

        public void Dispose()
        {
            StopPeriodicOnlineCheck();
        }
    }
}
