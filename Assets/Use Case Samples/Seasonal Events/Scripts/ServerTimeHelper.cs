using System;
using System.Diagnostics;

namespace Unity.Services.Samples.SeasonalEvents
{
    public static class ServerTimeHelper
    {
        static readonly DateTime k_StartEpochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const double k_MillisecondsPerSecond = 1000;

        // Track server time by storing utc when server time is requested and using a timer to
        // track time since server time was updated.
        // To calculate approximate server time, simply add stopwatch elapsed time to recorded server time.
        static DateTime m_ServerStartUtc = DateTime.UtcNow;

        // This tracks the time since the server time was updated.
        // Note that this method prevents players from altering server time by changing the clock since the
        // timer is uneffected by system clock.
        static Stopwatch m_ServerTimeStopwatch = new Stopwatch();

        public static DateTime UtcNow => m_ServerStartUtc + m_ServerTimeStopwatch.Elapsed;

        public static void SetServerEpochTime(double serverEpochTime)
        {
            // Determine current UTC time by adding server epoch time to the starting epoch time (1/1/1970)
            m_ServerStartUtc = k_StartEpochTime.AddSeconds(serverEpochTime / k_MillisecondsPerSecond);

            // Start the timer so we can always calculate the server time by adding elapsed to start time.
            m_ServerTimeStopwatch.Restart();
        }
    }
}
