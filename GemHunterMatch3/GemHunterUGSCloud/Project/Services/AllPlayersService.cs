using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace GemHunterUGSCloud.Services;

/// <summary>
/// AllPlayersService - Provides player discovery functionality by querying public player profiles.
/// 
/// Core Responsibilities:
/// - Query public player data from Cloud Save using indexed fields
/// - Filter and validate player profiles for completeness
/// - Convert Cloud Save query results to game Player objects
/// - Handle data integrity issues gracefully (duplicate entries, malformed data)
/// 
/// Key Cloud Code Functions:
/// - GetPlayerList: Retrieves a filtered list of players with valid profiles
/// 
/// Data Quality Features:
/// - Filters out players with missing display names or profile pictures
/// - Returns only players with complete, valid profiles
/// 
/// Query Strategy:
/// - Uses Cloud Save public data indexing for efficient queries
/// - Limits results (default: 25 players)
/// 
/// Documentation:
/// - Cloud Save Queries: 
/// <see href="<https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.CloudSave.CloudSaveDataApi.html#Unity_Services_Apis_CloudSave_CloudSaveDataApi_QueryPublicPlayerData_System_String_Unity_Services_Apis_CloudSave_QueryIndexBody_System_Threading_CancellationToken_>"/>
/// 
/// - QueryIndexBody:
/// https://docs.unity3d.com/Packages/com.unity.services.apis@1.1/api/Unity.Services.Apis.CloudSave.QueryIndexBody.html
/// </summary>
public class AllPlayersService
{
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<AllPlayersService> m_Logger;
    private const int k_DefaultPlayerQueryLimit = 25;

    public AllPlayersService(ILogger<AllPlayersService> logger, IGameApiClient gameApiClient)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
    }

    // TODO - Could add pagination parameters
    [CloudCodeFunction("GetPlayerList")]
    public async Task<List<Player>> GetPlayerListAsync(IExecutionContext context)
    {
        try
        {
            var queryResponse = await QueryPlayersPublicData(context);
            m_Logger.LogInformation($"Found {queryResponse.Data.Results.Count} players in Cloud Save");

            return FilterAndConvertToPlayerList(queryResponse.Data.Results);
        }
        catch (Exception ex)
        {
            m_Logger.LogError($"Failed to retrieve player list: {ex.Message}");
            // Consider whether to return an empty list or rethrow based on your requirements
            return new List<Player>();
            // Or: throw new InvalidOperationException("Could not retrieve player list", ex);
        }
    }
    
    private async Task<ApiResponse<QueryIndexResponse>> QueryPlayersPublicData(IExecutionContext context)
    {
        var queryBody = new QueryIndexBody(
            fields: new List<FieldFilter>
            {
                new(
                    key: PlayerDataService.k_PublicDisplayNameKey,
                    value: "",
                    op: FieldFilter.OpEnum.NE,
                    asc: true
                )
            },
            returnKeys: new List<string>
            {
                PlayerDataService.k_PublicDisplayNameKey,
                PlayerDataService.k_PublicProfilePictureKey
            },
            limit: k_DefaultPlayerQueryLimit
        );
        
        return await m_GameApiClient.CloudSaveData.QueryPublicPlayerDataAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            queryBody,
            CancellationToken.None
        );
    }

    private List<Player> FilterAndConvertToPlayerList(List<QueryIndexResponseResultsInner>? results)
    {
        if (results == null)
        {
            m_Logger.LogWarning("Received null results from QueryPlayersPublicData");
            return new List<Player>();
        }

        var mappedResults = results.Select(MapToPlayer).ToList();

        // Filter out players with incomplete profiles:
        // - Missing or empty display names
        // - Missing or invalid profile pictures (JSON parsing failures, data integrity issues)
        var validPlayers = mappedResults.Where(IsValidPlayer).ToList();

        // Log how many players were filtered out due to data issues
        var filteredCount = mappedResults.Count - validPlayers.Count;
        if (filteredCount > 0)
        {
            m_Logger.LogWarning("Filtered out {FilteredCount} of {TotalCount} players due to invalid data",
                filteredCount, mappedResults.Count);
        }

        return validPlayers;
    }

    private Player? MapToPlayer(QueryIndexResponseResultsInner result)
    {
        return new Player
        {
            PlayerId = result.Id,
            DisplayName = GetDisplayName(result.Data),
            PlayerPortrait = GetProfilePicture(result.Data)
        };
    }

    private bool IsValidPlayer(Player player)
    {
        return !string.IsNullOrWhiteSpace(player.DisplayName) &&
       player.PlayerPortrait != null;
    }

    private string? GetDisplayName(List<Item> items)
    {
        try
        {
            var displayNameItem = items.SingleOrDefault(item => 
                item.Key == PlayerDataService.k_PublicDisplayNameKey);
    
            return displayNameItem?.Value?.ToString();
        }
        catch (InvalidOperationException)
        {
            m_Logger.LogError("Multiple display name entries found for player - data integrity issue");
            return null;
        }
    }

    private ProfilePicture? GetProfilePicture(List<Item> items)
    {
        try
        {
            var pictureItem = items.SingleOrDefault(item =>
                item.Key == PlayerDataService.k_PublicProfilePictureKey);

            if (pictureItem?.Value?.ToString() is { } profilePictureJson)
            {
                try
                {
                    return JsonConvert.DeserializeObject<ProfilePicture>(profilePictureJson);
                }
                catch (JsonException ex)
                {
                    m_Logger.LogWarning($"Failed to parse profile picture: {ex.Message}");
                    return null;
                }
            }
        
            return null;
        }
        catch (InvalidOperationException)
        {
            m_Logger.LogError("Multiple profile picture entries found for player - data integrity issue");
            return null;
        }
    }
}