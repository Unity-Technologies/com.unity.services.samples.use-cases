using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Services.Samples.Tests.Multiplayer
{
    /// <summary>
    /// Drives the Lobby and Relay calls the Serverless Multiplayer Game makes (see LobbyManager and
    /// NetworkServiceManager) against live UGS, with the same option objects and data keys. The sample's
    /// scripts live in Assembly-CSharp, which a test assembly cannot reference, so the calls are mirrored
    /// here rather than made through the managers. Each test signs in a fresh anonymous host.
    /// </summary>
    public class LobbyRelayIntegrationTests
    {
        const string k_HostProfile = "default";
        const string k_GuestProfile = "lobbytestguest";
        const string k_PlayerNameKey = "playerName";
        const string k_IsReadyKey = "isReady";
        const string k_HostNameKey = "hostName";
        const string k_RelayJoinCodeKey = "relayJoinCode";
        const int k_MaxPlayers = 2;

        [UnitySetUp]
        public IEnumerator SignInFreshHost() => Await(async () =>
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            await SignInFreshPlayerAsync(k_HostProfile);
        });

        [UnityTest]
        public IEnumerator Lobby_HostCreatesUpdatesAndDeletes() => Await(async () =>
        {
            var hostId = AuthenticationService.Instance.PlayerId;

            var lobby = await CreateLobbyAsync("host");
            Assert.That(lobby.HostId, Is.EqualTo(hostId));
            Assert.That(lobby.MaxPlayers, Is.EqualTo(k_MaxPlayers));
            Assert.That(lobby.Players, Has.Count.EqualTo(1));
            Assert.That(lobby.Data[k_HostNameKey].Value, Is.EqualTo("host"));
            Assert.That(lobby.Data[k_RelayJoinCodeKey].Value, Is.EqualTo("RELAYCODE"));
            Assert.That(lobby.Players[0].Data[k_PlayerNameKey].Value, Is.EqualTo("host"));
            Assert.That(lobby.Players[0].Data[k_IsReadyKey].Value, Is.EqualTo("False"));

            await LobbyService.Instance.SendHeartbeatPingAsync(lobby.Id);

            var query = await LobbyService.Instance.QueryLobbiesAsync();
            Assert.That(query.Results, Has.Some.Matches<Lobby>(l => l.Id == lobby.Id), "public lobby is listed");

            var ready = await LobbyService.Instance.UpdatePlayerAsync(lobby.Id, hostId,
                new UpdatePlayerOptions { Data = PlayerData("host", isReady: true) });
            Assert.That(ready.Players[0].Data[k_IsReadyKey].Value, Is.EqualTo("True"));

            var fetched = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            Assert.That(fetched.Players[0].Data[k_IsReadyKey].Value, Is.EqualTo("True"));

            await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);

            // Get Lobby allows one call per second per player; the fetch above was less than a second ago.
            await Task.Delay(1100);
            LogAssert.ignoreFailingMessages = true;
            Assert.That(await ReasonOfFailedGetAsync(lobby.Id), Is.EqualTo(LobbyExceptionReason.LobbyNotFound));
        });

        [UnityTest]
        public IEnumerator Lobby_GuestJoinsByCodeThenLeaves() => Await(async () =>
        {
            var lobby = await CreateLobbyAsync("host");

            // The sample keeps one anonymous player per Authentication profile; the host's session
            // token survives the guest's sign-in under another profile.
            await SwitchToFreshPlayerAsync(k_GuestProfile);
            var guestId = AuthenticationService.Instance.PlayerId;

            var joined = await LobbyService.Instance.JoinLobbyByCodeAsync(lobby.LobbyCode,
                new JoinLobbyByCodeOptions { Player = new Player(data: PlayerData("guest", isReady: false)) });
            Assert.That(joined.Id, Is.EqualTo(lobby.Id));
            Assert.That(joined.Players, Has.Count.EqualTo(2));
            Assert.That(joined.Players[1].Id, Is.EqualTo(guestId));
            Assert.That(joined.Players[1].Data[k_PlayerNameKey].Value, Is.EqualTo("guest"));

            await LobbyService.Instance.RemovePlayerAsync(lobby.Id, guestId);

            await SwitchToSignedInPlayerAsync(k_HostProfile);
            var afterLeave = await LobbyService.Instance.GetLobbyAsync(lobby.Id);
            Assert.That(afterLeave.Players, Has.Count.EqualTo(1));

            await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
        });

        [UnityTest]
        public IEnumerator Relay_JoinCodeReachesTheHostAllocation() => Await(async () =>
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(k_MaxPlayers);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Assert.That(joinCode, Is.Not.Empty);
            Assert.That(allocation.RelayServer.IpV4, Is.Not.Empty);
            Assert.That(allocation.RelayServer.Port, Is.GreaterThan(0));

            var joined = await RelayService.Instance.JoinAllocationAsync(joinCode);
            Assert.That(joined.AllocationId, Is.Not.EqualTo(allocation.AllocationId));
            Assert.That(joined.RelayServer.IpV4, Is.EqualTo(allocation.RelayServer.IpV4));
            Assert.That(joined.RelayServer.Port, Is.EqualTo(allocation.RelayServer.Port));
            Assert.That(joined.HostConnectionData, Is.Not.Empty);
        });

        static Task<Lobby> CreateLobbyAsync(string hostName)
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { k_HostNameKey, new DataObject(DataObject.VisibilityOptions.Public, hostName) },
                    { k_RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, "RELAYCODE") },
                },
                Player = new Player(data: PlayerData(hostName, isReady: false)),
            };

            return LobbyService.Instance.CreateLobbyAsync("LobbyRelayIntegrationTests", k_MaxPlayers, options);
        }

        static Dictionary<string, PlayerDataObject> PlayerData(string playerName, bool isReady)
        {
            return new Dictionary<string, PlayerDataObject>
            {
                { k_PlayerNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
                { k_IsReadyKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, isReady.ToString()) },
            };
        }

        static async Task<LobbyExceptionReason?> ReasonOfFailedGetAsync(string lobbyId)
        {
            // Assert.ThrowsAsync would block the main thread the call completes on, so await by hand.
            try
            {
                await LobbyService.Instance.GetLobbyAsync(lobbyId);
                return null;
            }
            catch (LobbyServiceException e)
            {
                return e.Reason;
            }
        }

        static async Task SignInFreshPlayerAsync(string profile)
        {
            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn)
            {
                auth.SignOut(true);
            }

            auth.SwitchProfile(profile);
            auth.ClearSessionToken();
            await auth.SignInAnonymouslyAsync();
            Debug.Log($"[MultiplayerIntegration] project={Application.cloudProjectId} profile={profile} player={auth.PlayerId}");
        }

        static async Task SwitchToFreshPlayerAsync(string profile)
        {
            AuthenticationService.Instance.SignOut(clearCredentials: false);
            await SignInFreshPlayerAsync(profile);
        }

        static async Task SwitchToSignedInPlayerAsync(string profile)
        {
            var auth = AuthenticationService.Instance;
            auth.SignOut(clearCredentials: false);
            auth.SwitchProfile(profile);
            await auth.SignInAnonymouslyAsync();
        }

        static IEnumerator Await(Func<Task> body) => Await(body());

        static IEnumerator Await(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                ExceptionDispatchInfo.Capture(task.Exception.InnerException ?? task.Exception).Throw();
            }
        }
    }
}
