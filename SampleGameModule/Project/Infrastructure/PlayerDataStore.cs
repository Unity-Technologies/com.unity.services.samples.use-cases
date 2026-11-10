using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace SampleGameModule.Infrastructure;

/// <summary>A Cloud Save item as the sample services use it: its value plus the write lock to send back.</summary>
public record StoredItem(JToken? Value, string? WriteLock)
{
    public bool Exists => Value is not null;
    public T? As<T>() => Value is null ? default : Value.ToObject<T>();
}

/// <summary>
/// The player's Cloud Save keys. The keys the sample services own live in the protected access class:
/// the player can read them, and only a server holding the service token can write them, so a modified
/// client cannot set its own balance or progress. Key names are the ones the JavaScript scripts used.
/// Keys the player writes from the client, such as the mailbox inbox, stay in the default access class.
/// </summary>
public class PlayerDataStore
{
    readonly IGameApiClient m_GameApiClient;

    public PlayerDataStore(IGameApiClient gameApiClient)
    {
        m_GameApiClient = gameApiClient;
    }

    public async Task<StoredItem> GetAsync(IExecutionContext context, string key)
    {
        var items = await GetManyAsync(context, new[] { key });
        return items[key];
    }

    /// <summary>Reads several keys at once. Missing keys come back as empty items.</summary>
    public async Task<Dictionary<string, StoredItem>> GetManyAsync(IExecutionContext context, IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        var response = await m_GameApiClient.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
            context.ProjectId, context.PlayerId!, keyList);
        return ToStoredItems(keyList, response.Data?.Results);
    }

    /// <summary>Writes one key. Pass the write lock from the read to make the write conditional.</summary>
    public Task SetAsync(IExecutionContext context, string key, object value, string? writeLock = null)
    {
        return m_GameApiClient.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
            context.PlayerId!, new SetItemBody(key, value, writeLock!));
    }

    /// <summary>Writes several keys in one request, unconditionally.</summary>
    public Task SetManyAsync(IExecutionContext context, IDictionary<string, object> values)
    {
        var data = values.Select(kv => new SetItemBody(kv.Key, kv.Value, null!)).ToList();
        return m_GameApiClient.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
            context.ProjectId, context.PlayerId!, new SetItemBatchBody(data));
    }

    /// <summary>Deletes a key; a key that does not exist is not an error.</summary>
    public async Task DeleteAsync(IExecutionContext context, string key)
    {
        try
        {
            await m_GameApiClient.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, key,
                context.ProjectId, context.PlayerId!);
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.NotFound)
        {
        }
    }

    /// <summary>Reads a key the player writes from the client, in the default access class.</summary>
    public async Task<StoredItem> GetPlayerWrittenAsync(IExecutionContext context, string key)
    {
        var keyList = new List<string> { key };
        var response = await m_GameApiClient.CloudSaveData.GetItemsAsync(context, context.AccessToken,
            context.ProjectId, context.PlayerId!, keyList);
        return ToStoredItems(keyList, response.Data?.Results)[key];
    }

    /// <summary>Writes a key the player also writes from the client, in the default access class.</summary>
    public Task SetPlayerWrittenAsync(IExecutionContext context, string key, object value)
    {
        return m_GameApiClient.CloudSaveData.SetItemAsync(context, context.AccessToken, context.ProjectId,
            context.PlayerId!, new SetItemBody(key, value, null!));
    }

    public static bool IsWriteConflict(ApiException e) => e.Response?.StatusCode == HttpStatusCode.Conflict;

    static Dictionary<string, StoredItem> ToStoredItems(List<string> keys, List<Item>? results)
    {
        var items = keys.ToDictionary(k => k, _ => new StoredItem(null, null));
        foreach (var item in results ?? new List<Item>())
        {
            items[item.Key] = new StoredItem(item.Value is null ? null : JToken.FromObject(item.Value), item.WriteLock);
        }

        return items;
    }
}
