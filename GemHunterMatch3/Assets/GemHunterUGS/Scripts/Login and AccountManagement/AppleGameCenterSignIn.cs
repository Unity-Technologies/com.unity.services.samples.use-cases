 using System;
 using System.Threading.Tasks;
 using Unity.Services.Authentication;
 using Unity.Services.Core;
 using UnityEngine;
 using Logger = GemHunterUGS.Scripts.Utilities.Logger;
 
#if UNITY_IOS
namespace GemHunterUGS.Scripts.Login_and_AccountManagement
{
    public class AppleGameCenterSignIn : MonoBehaviour
    {

    private string m_Signature;
    private string m_TeamPlayerID;
    private string m_Salt;
    private string m_PublicKeyUrl;
    private ulong m_Timestamp;

    // Retrieve user credentials
    public async Task AuthenticatePlayerWithGameCenter()
    {
        if (!GKLocalPlayer.Local.IsAuthenticated)
        {
            // Perform the authentication.
            var player = await GKLocalPlayer.Authenticate();
            Logger.Log($"GameKit Authentication: player {player}");

            // Grab the display name.
            var localPlayer = GKLocalPlayer.Local;
            Logger.Log($"Local Player: {localPlayer.DisplayName}");

            // Fetch the items.
            var fetchItemsResponse = await GKLocalPlayer.Local.FetchItems();

            m_Signature = Convert.ToBase64String(fetchItemsResponse.GetSignature());
            m_TeamPlayerID = localPlayer.TeamPlayerId;
            Logger.Log($"Team Player ID: {m_TeamPlayerID}");

            m_Salt = Convert.ToBase64String(fetchItemsResponse.GetSalt());
            m_PublicKeyUrl = fetchItemsResponse.PublicKeyUrl;
            m_Timestamp = fetchItemsResponse.Timestamp;

            Logger.Log($"GameKit Authentication: signature => {m_Signature}");
            Logger.Log($"GameKit Authentication: publickeyurl => {m_PublicKeyUrl}");
            Logger.Log($"GameKit Authentication: salt => {m_Salt}");
            Logger.Log($"GameKit Authentication: Timestamp => {m_Timestamp}");
        }
        else
        {
            Logger.Log("AppleGameCenter player already logged in.");
        }
    }

    public void StartSignInOrLink()
    {
        SignInOrLinkWithAppleGameCenter();
    }

    private async void SignInOrLinkWithAppleGameCenter()
    {
        // Note:
        // Check string.IsNullOrEmpty(AuthenticationService.Instance.PlayerInfo.GetAppleGameCenterId())
        // if you want to unlink account

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await SignInWithAppleGameCenterAsync(m_Signature, m_TeamPlayerID, m_PublicKeyUrl, m_Salt, m_Timestamp);
        }
        else
        {
            await LinkWithAppleGameCenterAsync(m_Signature, m_TeamPlayerID, m_PublicKeyUrl, m_Salt, m_Timestamp);
        }
    }

    async Task SignInWithAppleGameCenterAsync(string signature, string teamPlayerId, string publicKeyURL, string salt, ulong timestamp)
    {
        try
        {
            await AuthenticationService.Instance.SignInWithAppleGameCenterAsync(signature, teamPlayerId, publicKeyURL, salt, timestamp);
            Logger.Log("SignIn is successful.");
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Logger.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Logger.LogException(ex);
        }
    }

    async Task LinkWithAppleGameCenterAsync(string signature, string teamPlayerId, string publicKeyURL, string salt, ulong timestamp)
    {
        try
        {
            await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(signature, teamPlayerId, publicKeyURL, salt, timestamp);
            Logger.Log("Link is successful.");
        }
        catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
        {
            // Prompt the player with an error message.
            Logger.LogError("This user is already linked with another account. Log in instead.");
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Logger.LogException(ex);
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Logger.LogException(ex);
        }
    }
}
#endif
