using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    [DisallowMultipleComponent]
    public class NetworkServiceManager : MonoBehaviour
    {
        public static NetworkServiceManager instance { get; private set; }

        [SerializeField]
        UnityTransport m_UnityTransport;

        bool m_NetworkManagerInitialized = false;

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

        public async Task<string> InitializeHost(int maxPlayerCount)
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayerCount);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkEndPoint endPoint = NetworkEndPoint.Parse(allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port);

             var ipAddress = endPoint.Address.Split(':')[0];

            m_UnityTransport.SetHostRelayData(ipAddress, endPoint.Port,
                allocation.AllocationIdBytes, allocation.Key,
                allocation.ConnectionData, false);

            Debug.Log($"Initialized Relay Host and received join code: {joinCode}");

            NetworkManager.Singleton.StartHost();

            m_NetworkManagerInitialized = true;

            return joinCode;
        }

        public async Task InitializeClient(string relayJoinCode)
        {
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            var endPoint = NetworkEndPoint.Parse(joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port);

            var ipAddress = endPoint.Address.Split(':')[0];

            m_UnityTransport.SetClientRelayData(ipAddress, endPoint.Port,
                joinAllocation.AllocationIdBytes, joinAllocation.Key,
                joinAllocation.ConnectionData, joinAllocation.HostConnectionData, false);

            NetworkManager.Singleton.StartClient();

            m_NetworkManagerInitialized = true;
        }

        public void Uninitialize()
        {
            if (m_NetworkManagerInitialized)
            {
                m_NetworkManagerInitialized = false;
                NetworkManager.Singleton.Shutdown();
            }
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
