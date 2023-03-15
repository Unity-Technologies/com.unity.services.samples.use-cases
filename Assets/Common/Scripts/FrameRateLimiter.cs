using System;
using UnityEngine;

namespace Unity.Services.Samples
{
    // We don't need a ton of frames to run this project.
    // Lower frame rates use less energy.

    public static class FrameRateLimiter
    {
        [RuntimeInitializeOnLoadMethod]
        public static void LimitFrameRate()
        {
            Application.targetFrameRate = 30;
        }
    }
}
