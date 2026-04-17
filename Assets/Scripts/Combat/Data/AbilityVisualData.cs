using UnityEngine;
using System;

namespace MilehighWorld.CombatSystems.Data
{
    public enum ParameterType
    {
        Float,
        Int,
        Bool,
        Color,
        Vector3
    }

    [Serializable]
    public class VFXParameterMapping
    {
        public string parameterName;
        public ParameterType type;

        public float floatValue;
        public int intValue;
        public bool boolValue;
        public Color colorValue;
        public Vector3 vectorValue;
    }

    [CreateAssetMenu(fileName = "AbilityVisualData", menuName = "MilehighWorld/Combat/Ability Visual Data")]
    public class AbilityVisualData : ScriptableObject
    {
        [Header("Animation")]
        public string animTrigger;

        [Header("VFX Settings")]
        public string vfxSlotName;
        public string[] startVFXEvents;
        public string[] impactVFXEvents;
        public VFXParameterMapping[] parameterMappings;

        [Header("Lighting Settings")]
        public string lightSlotName;
        public bool usePrimaryLightFlash = true;
        public float lightFlashIntensity = 150000f;
        public float lightFlashSpeed = 5f;

        [Header("Timing")]
        public float delayBeforeCompletion = 0.4f;
    }
}
