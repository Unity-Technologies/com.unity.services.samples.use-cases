using Unity.Netcode;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class Coin : NetworkBehaviour
    {
        [field: SerializeField]
        public NetworkObject networkObject { get; private set; }

        float m_DespawnTime = float.MaxValue;

        bool m_WasCollected = false;

        bool m_WasDespawned = false;

        public bool isCollectable => !(m_WasCollected || m_WasDespawned);

        public void Initialize(float lifespan)
        {
            if (IsOwner)
            {
                m_DespawnTime = Time.time + lifespan;
            }
        }

        void Update()
        {
            if (IsOwner)
            {
                if (!isCollectable) return;

                if (Time.time >= m_DespawnTime)
                {
                    m_WasDespawned = true;
                    networkObject.Despawn(true);
                }
            }
        }

        public void OnCollect()
        {
            if (!isCollectable) return;

            m_WasCollected = true;
            networkObject.Despawn(true);
        }
    }
}
