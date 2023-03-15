using Unity.Netcode;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class PlayerAvatar : NetworkBehaviour
    {
        // Amount of velocity change to apply to player to accelerate in desired direction when keys are pressed.
        // Note that we use a rigid body and velocity change rather than just changing x/z values so players
        // accelerate and decelerate a bit and collide with other players rather than passing through them.
        [SerializeField]
        float m_Acceleration = 250;

        [field: SerializeField]
        public NetworkObject networkObject { get; private set; }

        [SerializeField]
        Rigidbody m_RigidBody;

        public int playerIndex { get; private set; }

        public string playerId { get; private set; }

        public string playerName { get; private set; }

        public ulong playerRelayId { get; private set; }

        public int score { get; private set; }

        // During countdown, the player is not allowed to move so this is checked and inputs are ignored until the game begins.
        bool m_IsMovementAllowed = false;

        void Update()
        {
            if (!IsOwner) return;

            if (m_IsMovementAllowed)
            {
                var acceleration = new Vector3(Input.GetAxis("Horizontal"),
                    0, Input.GetAxis("Vertical"));

                acceleration *= Time.deltaTime * m_Acceleration;

                m_RigidBody.AddForce(acceleration, ForceMode.VelocityChange);
            }
        }

        [ClientRpc]
        public void SetPlayerAvatarClientRpc(int playerIndex, string playerId, string playerName, ulong relayClientId)
        {
            this.playerIndex = playerIndex;
            this.playerId = playerId;
            this.playerName = playerName;
            this.playerRelayId = relayClientId;

            // Sanitize the player name to ensure it's not profane.
            this.playerName = ProfanityManager.SanitizePlayerName(this.playerName);

            GameNetworkManager.instance?.AddPlayerAvatar(this, IsOwner);

            Debug.Log($"Set player avatar for player #{playerIndex}: id:'{playerId}' name:'{playerName}' relay:{relayClientId}");
        }

        public void AllowMovement()
        {
            m_IsMovementAllowed = true;
        }

        void OnTriggerEnter(Collider other)
        {
            // See if collision was with a coin, and, if so, collect it now.
            var coin = other.GetComponent<Coin>();
            if (coin != null)
            {
                HandleCoinCollection(coin);
            }
        }

        void HandleCoinCollection(Coin coin)
        {
            // The host can actually collect the coin and update the player's score.
            if (IsHost)
            {
                GameCoinManager.instance?.CollectCoin(this, coin);
            }

            // Clients hide the game object immediately so coin appears collected. Host will be doing the same (see above)
            // and will be resposible for actually despawning the coin and updating the player's score.
            else
            {
                coin.gameObject.SetActive(false);
            }
        }

        [ClientRpc]
        public void ScorePointClientRpc()
        {
            score++;

            GameSceneManager.instance?.UpdateScores();
        }

        public override string ToString()
        {
            return $"Player avatar #{playerIndex} '{playerName}' score:{score}.";
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            GameNetworkManager.instance?.OnPlayerAvatarDestroyedServerRpc(playerRelayId);
        }
    }
}
