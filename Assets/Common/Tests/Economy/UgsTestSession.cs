using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.Samples.Tests.Economy
{
    /// <summary>
    /// Live UGS session for the integration tests. Initializes services with the environment
    /// selected in Project Settings > Services (the same one the samples use), and signs in a
    /// fresh anonymous player on demand so each fixture starts from empty player state.
    /// </summary>
    public static class UgsTestSession
    {
        public static async Task SignInFreshPlayerAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            var auth = AuthenticationService.Instance;
            if (auth.IsSignedIn)
            {
                auth.SignOut(true);
            }

            auth.ClearSessionToken();
            await auth.SignInAnonymouslyAsync();

            Debug.Log($"[EconomyIntegration] world={PlayerEconomyState.WorldName} " +
                $"project={Application.cloudProjectId} player={auth.PlayerId}");
        }

        /// <summary>Calls a sample JS script exactly as its CloudCodeManager does.</summary>
        public static Task<JObject> CallScriptAsync(string scriptName, Dictionary<string, object> args = null)
        {
            return CallScriptAsync<JObject>(scriptName, args);
        }

        /// <summary>
        /// Same function names, same contracts: the migrated project serves them from the
        /// SampleGameModule C# module, the legacy checkout from the JavaScript scripts, so the
        /// comparison stays like for like.
        /// </summary>
        public static Task<T> CallScriptAsync<T>(string functionName, Dictionary<string, object> args = null)
        {
            args ??= new Dictionary<string, object>();
#if UGS_ECONOMY_SDK
            return CloudCodeService.Instance.CallEndpointAsync<T>(functionName, args);
#else
            return CloudCodeService.Instance.CallModuleEndpointAsync<T>(k_Module, functionName, args);
#endif
        }

        /// <summary>Calls a function that returns nothing; the SDK would fail to deserialize an empty body.</summary>
        public static Task CallScriptVoidAsync(string functionName, Dictionary<string, object> args = null)
        {
            args ??= new Dictionary<string, object>();
#if UGS_ECONOMY_SDK
            return CloudCodeService.Instance.CallEndpointAsync(functionName, args);
#else
            return CloudCodeService.Instance.CallModuleEndpointAsync(k_Module, functionName, args);
#endif
        }

        const string k_Module = "SampleGameModule";

        public static Dictionary<string, object> Args(params (string key, object value)[] pairs)
        {
            var args = new Dictionary<string, object>();
            foreach (var (key, value) in pairs)
            {
                args[key] = value;
            }

            return args;
        }
    }
}
