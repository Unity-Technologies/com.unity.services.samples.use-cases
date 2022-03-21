using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Economy;

namespace UnityGamingServicesUseCases
{
    public static class Utils
    {
        public static async Task<T> ProcessEconomyTaskWithRetry<T>(Task<T> task)
        {
            var delayMs = 100;
            while (true)
            {
                try
                {
                    var ret = await task;

                    return ret;
                }
                catch (EconomyException e)
                when (e.Reason == EconomyExceptionReason.RateLimited)
                {
                    // If the rate-limited exception occurs, use exponential back-off when retrying
                    await Task.Delay(delayMs);

                    Debug.Log($"Retrying Economy call due to rate-limit exception after {delayMs}ms delay.");

                    delayMs *= 2;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    return default;
                }
            }
        }

        public static string GetElapsedTimeRange(DateTime startTime)
        {
            var elapsedTime = DateTime.Now - startTime;
            var elapsedSeconds = elapsedTime.TotalSeconds;

            if (elapsedSeconds < 0)
            {
                return "N/A";
            }

            // BottomRange is the nearest divisible-by-10 number less than elapsedSeconds.
            // For instance, 47.85 seconds has a bottom range of 40.
            var bottomRange = (int) Math.Floor(elapsedSeconds / 10) * 10;

            // TopRange is the nearest divisible-by-10 number greater than elapsedSeconds.
            // For instance, 47.85 seconds has a top range of 50.
            var topRange = bottomRange + 10;

            // In the string being returned `[` represents inclusive and `)` represents exclusive. So a range of
            // [40, 50) includes numbers from 40.0 to 49.99999 etc.
            return $"[{bottomRange}, {topRange}) seconds";
        }
    }
}
