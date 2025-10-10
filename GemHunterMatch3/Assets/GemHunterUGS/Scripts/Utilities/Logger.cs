using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace GemHunterUGS.Scripts.Utilities
{
    internal static class Logger
    {

        const string k_VerboseLoggingDefine = "ENABLE_VERBOSE_LOGGING";
        const string k_DemoLoggingDefine = "ENABLE_DEMO_LOGGING";
    
        public static void Log(object message) => Debug.unityLogger.Log(message);
        public static void LogWarning(object message) => Debug.unityLogger.LogWarning(null, message);
        public static void LogError(object message) => Debug.unityLogger.LogError(null, message);
        public static void LogException(Exception exception) => Debug.unityLogger.Log(LogType.Exception, exception);
        [Conditional("UNITY_ASSERTIONS")]
        public static void LogAssertion(object message) => Debug.unityLogger.Log(LogType.Assert, message);
    
#if !ENABLE_UNITY_SERVICES_VERBOSE_LOGGING
        [Conditional(k_VerboseLoggingDefine)]
#endif
        public static void LogVerbose(object message) => Debug.unityLogger.Log(message);
        
        [Conditional(k_DemoLoggingDefine)]
        public static void LogDemo(object message) => Debug.unityLogger.Log(LogType.Log, null, message);
    }
}
