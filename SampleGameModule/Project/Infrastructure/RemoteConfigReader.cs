using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SampleGameModule.Models;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.RemoteConfig.Model;

namespace SampleGameModule.Infrastructure;

/// <summary>
/// Remote Config settings as the player would receive them, including Game Override
/// targeting. User attributes drive the overrides' JEXL rules: the season samples send
/// <c>timestampMinutes</c> so the right season override is applied server-side.
/// </summary>
public class RemoteConfigReader
{
    readonly IGameApiClient m_GameApiClient;

    public RemoteConfigReader(IGameApiClient gameApiClient)
    {
        m_GameApiClient = gameApiClient;
    }

    public async Task<ConfigValues> GetAsync(IExecutionContext context, IEnumerable<string> keys,
        Dictionary<string, string>? userAttributes = null)
    {
        var keyList = keys.ToList();
        Dictionary<string, object>? settings;
        if (userAttributes is null)
        {
            var response = await m_GameApiClient.RemoteConfigSettings.AssignSettingsGetAsync(context,
                context.AccessToken, context.ProjectId, context.EnvironmentId, key: keyList);
            settings = response.Data?.Configs?.Settings;
        }
        else
        {
            var request = new SettingsDeliveryRequest(context.ProjectId, context.PlayerId, null,
                context.EnvironmentId, null, keyList, null, false, null, null,
                // Remote Config rejects a null "unity" attributes object; an empty one is fine.
                new RequestAttributes(new UnityAttributes(null!, null!, null!, null!, null!, null!, null!, null!, null!, 0, null!, 0),
                    new Dictionary<string, string>(), userAttributes));
            var response = await m_GameApiClient.RemoteConfigSettings.AssignSettingsAsync(context,
                context.AccessToken, request);
            settings = response.Data?.Configs?.Settings;
        }

        return new ConfigValues(settings ?? new Dictionary<string, object>());
    }
}

/// <summary>Typed access to one Remote Config response.</summary>
public class ConfigValues
{
    readonly IReadOnlyDictionary<string, object> m_Settings;

    public ConfigValues(IReadOnlyDictionary<string, object> settings)
    {
        m_Settings = settings;
    }

    public IReadOnlyDictionary<string, object> Raw => m_Settings;

    public bool Has(string key) => m_Settings.TryGetValue(key, out var v) && v is not null;

    public string GetString(string key, string fallback = "")
        => m_Settings.TryGetValue(key, out var v) && v is not null ? v.ToString()! : fallback;

    public long GetLong(string key, long fallback = 0)
        => m_Settings.TryGetValue(key, out var v) && v is not null && long.TryParse(v.ToString(), out var l) ? l : fallback;

    /// <summary>A JSON-typed setting; Remote Config hands these back as JObject/JArray or as a string.</summary>
    public JToken? GetJson(string key)
    {
        if (!m_Settings.TryGetValue(key, out var v) || v is null)
            return null;
        return v as JToken ?? JToken.Parse(v.ToString()!);
    }

    public T GetJson<T>(string key, string missingMessage)
    {
        var token = GetJson(key)
            ?? throw new ModuleError(2, "RemoteConfigKeyMissingError", missingMessage);
        return token.ToObject<T>()
            ?? throw new ModuleError(2, "RemoteConfigKeyMissingError", missingMessage);
    }
}
