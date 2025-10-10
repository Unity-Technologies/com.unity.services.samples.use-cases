using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Match3
{
    [ExecuteInEditMode]
    public class LightManager : MonoBehaviour
    {
        public Material globalLightMaterial;

        void Update()
        {
            var matAngle = transform.rotation * Vector3.forward;
            globalLightMaterial.SetVector("_LightDirection", matAngle);
        }

        public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
        {
            return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
        }
    }
}