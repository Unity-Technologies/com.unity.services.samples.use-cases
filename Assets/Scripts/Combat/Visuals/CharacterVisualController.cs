using UnityEngine;
using UnityEngine.VFX;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System;
using MilehighWorld.CombatSystems.Data;

namespace MilehighWorld.CombatSystems.Visuals
{
    [RequireComponent(typeof(Animator))]
    public class CharacterVisualController : MonoBehaviour, IBicameralAuthorized, INonaryStabilizable, ITelemetryNode
    {
        [Serializable]
        public struct VisualEffectSlot
        {
            public string name;
            public VisualEffect vfxComponent;
        }

        [Serializable]
        public struct LightSlot
        {
            public string name;
            public Light lightComponent;
        }

        [Header("Core References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Light primaryFlashLight;

        [Header("Slotted Components")]
        [SerializeField] private List<VisualEffectSlot> vfxSlots = new List<VisualEffectSlot>();
        [SerializeField] private List<LightSlot> lightSlots = new List<LightSlot>();

        [Header("Ability Data")]
        [SerializeField] private List<AbilityVisualData> characterAbilities = new List<AbilityVisualData>();

        private Dictionary<string, AbilityVisualData> _abilityLookup = new Dictionary<string, AbilityVisualData>();
        private Dictionary<string, VisualEffect> _vfxLookup = new Dictionary<string, VisualEffect>();
        private Dictionary<string, Light> _lightLookup = new Dictionary<string, Light>();

        private TaskCompletionSource<bool> _currentVisualTask;
        private AbilityVisualData _currentActiveAbility;
        private VisualTelemetryData _latestTelemetry;

        // Architectural Constants
        private const int NONARY_TICK_CYCLE = 512;
        private const float VERNIER_CONSTANT = 1.125f;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();

            InitializeLookups();
            ResetVisualState();
        }

        private void InitializeLookups()
        {
            foreach (var ability in characterAbilities)
            {
                if (ability != null && !string.IsNullOrEmpty(ability.name))
                {
                    // Note: In a production environment, we might use a specific ID instead of the asset name
                    _abilityLookup[ability.name] = ability;
                }
            }

            foreach (var slot in vfxSlots)
            {
                if (!string.IsNullOrEmpty(slot.name))
                    _vfxLookup[slot.name] = slot.vfxComponent;
            }

            foreach (var slot in lightSlots)
            {
                if (!string.IsNullOrEmpty(slot.name))
                    _lightLookup[slot.name] = slot.lightComponent;
            }
        }

        private void ResetVisualState()
        {
            if (primaryFlashLight != null) primaryFlashLight.intensity = 0f;
            foreach (var light in _lightLookup.Values)
            {
                if (light != null) light.intensity = 0f;
            }
        }

        /// <summary>
        /// Entry point for playing ability visuals, now requiring architectural consensus.
        /// </summary>
        public async Task PlayAbilityVisualsAsync(string abilityName, HandshakeAuthorization handshake = null)
        {
            if (!_abilityLookup.TryGetValue(abilityName, out AbilityVisualData data))
            {
                Debug.LogWarning($"[VisualController] No AbilityVisualData mapped for '{abilityName}'.");
                _currentVisualTask?.TrySetResult(true);
                return;
            }

            // 1. Perform Bicameral Handshake (The "High-Five" Protocol)
            if (data.requiresHighFiveAuth && !PerformHandshake(handshake))
            {
                Debug.LogError($"[Architect] Bicameral Handshake Failed for '{abilityName}'. Execution Aborted.");
                return;
            }

            // Ensure any previous task is cleared to avoid race conditions
            _currentVisualTask?.TrySetResult(false);
            _currentVisualTask = new TaskCompletionSource<bool>();

            _currentActiveAbility = data;

            // Start timing for Telemetry
            float startTime = Time.realtimeSinceStartup;

            await ExecuteAbilitySequence(data);

            // Record Telemetry
            if (data.recordNarrativeTelemetry)
            {
                RecordTelemetry(new VisualTelemetryData
                {
                    AbilityName = abilityName,
                    ExecutionTimeMs = (Time.realtimeSinceStartup - startTime) * 1000f,
                    IsStabilized = CheckParity(Time.frameCount % NONARY_TICK_CYCLE),
                    PhysicsTickIndex = Time.frameCount % NONARY_TICK_CYCLE
                });
            }

            await _currentVisualTask.Task;
        }

        private async Task ExecuteAbilitySequence(AbilityVisualData data)
        {
            // Apply Nonary Stabilization before execution
            if (data.applyNonaryStabilization)
            {
                ApplyStabilization(VERNIER_CONSTANT);
            }

            // 1. Set Animator Trigger
            if (!string.IsNullOrEmpty(data.animTrigger))
            {
                animator.SetTrigger(data.animTrigger);
            }

            // 2. Setup and Trigger VFX
            if (_vfxLookup.TryGetValue(data.vfxSlotName, out VisualEffect vfx))
            {
                ApplyVFXParameters(vfx, data.parameterMappings);

                foreach (var startEvent in data.startVFXEvents)
                {
                    vfx.SendEvent(startEvent);
                }
            }

            // 3. Handle Completion Timing
            // If the ability relies on an animation event, we wait.
            // If it has a fixed delay, we use that.
            if (data.delayBeforeCompletion > 0)
            {
                await Task.Delay((int)(data.delayBeforeCompletion * 1000));
                _currentVisualTask?.TrySetResult(true);
            }

            // Note: If delayBeforeCompletion is <= 0, we expect AnimEvent_OnAbilityImpact to signal completion.
        }

        private void ApplyVFXParameters(VisualEffect vfx, VFXParameterMapping[] mappings)
        {
            if (vfx == null || mappings == null) return;

            foreach (var mapping in mappings)
            {
                switch (mapping.type)
                {
                    case ParameterType.Float:
                        vfx.SetFloat(mapping.parameterName, mapping.floatValue);
                        break;
                    case ParameterType.Int:
                        vfx.SetInt(mapping.parameterName, mapping.intValue);
                        break;
                    case ParameterType.Bool:
                        vfx.SetBool(mapping.parameterName, mapping.boolValue);
                        break;
                    case ParameterType.Color:
                        vfx.SetVector4(mapping.parameterName, mapping.colorValue);
                        break;
                    case ParameterType.Vector3:
                        vfx.SetVector3(mapping.parameterName, mapping.vectorValue);
                        break;
                }
            }
        }

        // ==========================================
        // Animation Event Receivers
        // ==========================================

        public void AnimEvent_OnAbilityImpact()
        {
            if (_currentActiveAbility == null) return;

            // Trigger Impact VFX
            if (_vfxLookup.TryGetValue(_currentActiveAbility.vfxSlotName, out VisualEffect vfx))
            {
                foreach (var impactEvent in _currentActiveAbility.impactVFXEvents)
                {
                    vfx.SendEvent(impactEvent);
                }
            }

            // Trigger Light Flash
            StartCoroutine(HandleImpactLighting(_currentActiveAbility));

            // Signal completion if not already signaled by delay
            _currentVisualTask?.TrySetResult(true);
        }

        [Obsolete("Use AnimEvent_OnAbilityImpact for new animations.")]
        public void AnimEvent_OnJusticesEdgeImpact()
        {
            AnimEvent_OnAbilityImpact();
        }

        // ==========================================
        // Architectural Interface Implementations
        // ==========================================

        public bool PerformHandshake(HandshakeAuthorization handshake)
        {
            // In a production environment, this would validate signatures via Cloud Code
            if (handshake == null)
            {
                Debug.LogWarning("[Architect] No handshake provided. Proceeding with Local Override (Dev Mode).");
                return true;
            }

            return handshake.ConsensusReached;
        }

        public bool CheckParity(int tickCount)
        {
            // Nonary Parity Check: Ensure digital root reconciles to 9 at the end of the cycle
            return tickCount % 9 == 0;
        }

        public void ApplyStabilization(float vernierConstant)
        {
            // IX-Node Stabilization logic: Adjust transforms or timings to match Nonary grid
            // For now, we log the stabilization event to the telemetry stream
            // Debug.Log($"[Nonary] Stabilization Applied with constant {vernierConstant}");
        }

        public void RecordTelemetry(VisualTelemetryData data)
        {
            _latestTelemetry = data;
            // Note: In production, this would be serialized and sent to the Cloud Code NarrativeObservabilityHub
            // Debug.Log($"[Telemetry] Ability: {data.AbilityName}, Latency: {data.ExecutionTimeMs}ms, Stabilized: {data.IsStabilized}");
        }

        public VisualTelemetryData GetLatestTelemetry() => _latestTelemetry;

        private IEnumerator HandleImpactLighting(AbilityVisualData data)
        {
            List<Light> lightsToFlash = new List<Light>();

            if (data.usePrimaryLightFlash && primaryFlashLight != null)
                lightsToFlash.Add(primaryFlashLight);

            if (!string.IsNullOrEmpty(data.lightSlotName) && _lightLookup.TryGetValue(data.lightSlotName, out Light slotLight))
                lightsToFlash.Add(slotLight);

            if (lightsToFlash.Count == 0) yield break;

            float intensity = data.lightFlashIntensity;
            foreach (var l in lightsToFlash) l.intensity = intensity;

            float t = 0;
            while (t < 1f)
            {
                t += Time.deltaTime * data.lightFlashSpeed;
                float currentIntensity = Mathf.Lerp(intensity, 0f, t);
                foreach (var l in lightsToFlash) l.intensity = currentIntensity;
                yield return null;
            }

            foreach (var l in lightsToFlash) l.intensity = 0f;
        }
    }
}
