using System;
using UnityEngine;

namespace MilehighWorld.CombatSystems.Data
{
    /// <summary>
    /// Represents the consensus handshake between Alpha (Generative) and Omega (Anchor) cores.
    /// </summary>
    [Serializable]
    public class HandshakeAuthorization
    {
        public string RequestId;
        public string AlphaSignature;
        public string OmegaSignature;
        public bool ConsensusReached;
        public long Timestamp;
    }

    /// <summary>
    /// Telemetry payload for quantifying narrative drift and visual performance.
    /// </summary>
    [Serializable]
    public class VisualTelemetryData
    {
        public string AbilityName;
        public float ExecutionTimeMs;
        public float NarrativeDriftDelta;
        public int PhysicsTickIndex;
        public bool IsStabilized;
    }

    /// <summary>
    /// Interface for entities requiring consensus before execution (The "High-Five" Protocol).
    /// </summary>
    public interface IBicameralAuthorized
    {
        bool PerformHandshake(HandshakeAuthorization handshake);
    }

    /// <summary>
    /// Interface for maintaining stability via 9-bit parity cycles (Nonary Physics).
    /// </summary>
    public interface INonaryStabilizable
    {
        bool CheckParity(int tickCount);
        void ApplyStabilization(float vernierConstant);
    }

    /// <summary>
    /// Interface for nodes that contribute to the Narrative Health coefficient via telemetry.
    /// </summary>
    public interface ITelemetryNode
    {
        void RecordTelemetry(VisualTelemetryData data);
        VisualTelemetryData GetLatestTelemetry();
    }
}
