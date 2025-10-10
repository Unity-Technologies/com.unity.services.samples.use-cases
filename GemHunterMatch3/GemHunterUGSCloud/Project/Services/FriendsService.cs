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
using Unity.Services.Friends.Model;
namespace GemHunterUGSCloud.Services;

/// <summary>
/// FriendsService - Manages player friend relationships and social interactions.
/// 
/// Core Responsibilities:
/// - Friend list retrieval with complete player profile data
/// - Friend request creation and management
/// - Public player data integration for friend profiles
/// - Relationship validation and duplicate prevention
/// 
/// Key Cloud Code Functions:
/// - GetFriends: Retrieves complete friend list with player data
/// - AddFriend: Creates friend requests with validation
/// - RemoveFriend: Deletes friend relationships
/// - AcceptFriendRequest: Converts requests to friendships (commented for demo)
/// 
/// </summary>
public class FriendsService
{
    private readonly IGameApiClient m_GameApiClient;
    private readonly ILogger<FriendsService> m_Logger;
    
    public FriendsService(ILogger<FriendsService> logger, IGameApiClient gameApiClient)
    {
        m_Logger = logger;
        m_GameApiClient = gameApiClient;
    }
    
    [CloudCodeFunction("GetFriends")]
    public async Task<List<Player>> GetFriends(IExecutionContext context)
    {
        var relationships = await GetPlayerRelationships(context);
        
        var players = await Task.WhenAll(
            relationships.Select(relationship => PopulateFriendInfo(context, relationship))
        );

        return players.OfType<Player>().ToList();
    }
    
    private async Task<List<Relationship>> GetPlayerRelationships(IExecutionContext context)
    {
        var response = await m_GameApiClient.FriendsRelationshipsApi.GetRelationshipsAsync(
            context,
            context.AccessToken,
            type: new List<RelationshipType> { RelationshipType.FRIEND, RelationshipType.FRIENDREQUEST },
            cancellationToken: CancellationToken.None
        );

        return response.Data ?? new List<Relationship>();
    }
    
    private async Task<Player?> PopulateFriendInfo(IExecutionContext context, Relationship relationship)
    {
        var friendMember = relationship.Members.First(m => m.Id != context.PlayerId);
        var publicPlayerData = await GetPublicPlayerData(context, friendMember.Id);
        
        return MapToPlayer(context, friendMember.Id, publicPlayerData);
    }
    
    private async Task<ApiResponse<GetItemsResponse>> GetPublicPlayerData(IExecutionContext context, string playerId)
    {
        return await m_GameApiClient.CloudSaveData.GetPublicItemsAsync(
            context,
            context.AccessToken,
            context.ProjectId,
            playerId,
            new List<string>
            {
                PlayerDataService.k_PublicDisplayNameKey,
                PlayerDataService.k_PublicProfilePictureKey,
            }
        );
    }
    
    private Player? MapToPlayer(IExecutionContext context, string playerId, ApiResponse<GetItemsResponse> publicData)
    {
        var displayName = GetDisplayName(publicData.Data.Results);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            m_Logger.LogWarning("Player {PlayerId} has friend {FriendId} with missing display name", context.PlayerId, playerId);
            return null;
        }

        return new Player
        {
            PlayerId = playerId,
            DisplayName = displayName,
            PlayerPortrait = GetProfilePicture(publicData.Data.Results) ?? new ProfilePicture()
        };
    }
    
    private string? GetDisplayName(List<Item> items)
    {
        try
        {
            var displayNameItem = items.SingleOrDefault(item => 
                item.Key == PlayerDataService.k_PublicDisplayNameKey);
        
            var displayName = displayNameItem?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }
            return displayName;
        }
        catch (InvalidOperationException)
        {
            m_Logger.LogError("Multiple display name entries found for friend - data integrity issue");
            return null;
        }
    }

    private ProfilePicture GetProfilePicture(List<Item> items)
    {
        try
        {
            var pictureItem = items.SingleOrDefault(item => 
                item.Key == PlayerDataService.k_PublicProfilePictureKey);

            if (pictureItem?.Value?.ToString() is { } profilePictureJson)
            {
                try
                {
                    var picture = JsonConvert.DeserializeObject<ProfilePicture>(profilePictureJson);
                    return picture ?? new ProfilePicture();
                }
                catch (JsonException ex)
                {
                    m_Logger.LogWarning($"Failed to parse friend's profile picture: {ex.Message}");
                    return new ProfilePicture();
                }
            }
        
            return new ProfilePicture();
        }
        catch (InvalidOperationException)
        {
            m_Logger.LogError("Multiple profile picture entries found for friend - data integrity issue");
            return new ProfilePicture();
        }
    }
    
    [CloudCodeFunction("AddFriend")]
    public async Task<List<Player>> AddFriend(IExecutionContext context, string friendPlayerId)
    {
        await ValidateFriendRequest(context, friendPlayerId);
        await CreateFriendRequest(context, friendPlayerId);
        return await GetFriends(context);
    }
    
    private async Task ValidateFriendRequest(IExecutionContext context, string friendPlayerId)
    {
        if (context.PlayerId == friendPlayerId)
        {
            throw new InvalidOperationException("Cannot create friend request with self");
        }

        var existingRelationships = await GetPlayerRelationships(context);
        if (existingRelationships.Any(r => r.Members.Any(m => m.Id == friendPlayerId)))
        {
            throw new InvalidOperationException($"Friendship or request already exists with {friendPlayerId}");
        }
    }

    private async Task CreateFriendRequest(IExecutionContext context, string friendPlayerId)
    {
        var newRelationshipRequest = new AddRelationshipRequest(
            RelationshipType.FRIEND,
            new List<MemberIdentity>
            {
                new() { Id = friendPlayerId, Role = Role.NONE }
            }
        );

        await m_GameApiClient.FriendsRelationshipsApi.CreateRelationshipAsync(
            context,
            context.AccessToken,
            addRelationshipRequest: newRelationshipRequest,
            cancellationToken: default
        );
    }
    
    [CloudCodeFunction("RemoveFriend")]
    public async Task<List<Player>> RemoveFriend(IExecutionContext context, string friendPlayerId)
    {
        var relationship = await FindRelationship(context, friendPlayerId);
        
        await m_GameApiClient.FriendsRelationshipsApi.DeleteRelationshipAsync(
            context,
            context.AccessToken,
            relationship.Id,
            cancellationToken: default
        );
        
        return await GetFriends(context);
    }
    
    private async Task<Relationship> FindRelationship(IExecutionContext context, string friendPlayerId)
    {
        var relationships = await GetPlayerRelationships(context);
        
        var relationship = relationships.FirstOrDefault(r => 
            r.Members.Any(m => m.Id == friendPlayerId));

        if (relationship == null)
        {
            throw new InvalidOperationException($"No relationship found with player {friendPlayerId}");
        }

        return relationship;
    }
    
    // Not using, it's helpful to clean prior requests when testing
    private async Task CleanupExistingRequests(IExecutionContext context)
    {
        var existingRequests = await m_GameApiClient.FriendsRelationshipsApi.GetRelationshipsAsync(
            context,
            context.AccessToken,
            type: new List<RelationshipType> { RelationshipType.FRIENDREQUEST }
        );

        if (existingRequests.Data?.Any() != true) return;

        foreach (var request in existingRequests.Data)
        {
            await m_GameApiClient.FriendsRelationshipsApi.DeleteRelationshipAsync(
                context,
                context.AccessToken,
                request.Id,
                cancellationToken: default
            );
        }
    }


    // Not using, but can be helpful in development
    private void LogRelationships(ApiResponse<List<Relationship>> relationships)
    {
        foreach (var relationship in relationships.Data)
        {
            m_Logger.LogInformation($"Relationship {relationship.Id}:");
            foreach (var member in relationship.Members)
            {
                m_Logger.LogInformation($"-- Member: Id={member.Id}, Role={member.Role}");
            }
        }
    }
    
    // Not using but including for demo
    #region Accept Friend Request
    // [CloudCodeFunction("AcceptFriendRequest")]
    public async Task<List<Player>> AcceptFriendRequest(
        IExecutionContext context,
        string requesterPlayerId)
    {
        var pendingRequests = await GetPlayerRelationships(context);

        var friendRequest = pendingRequests.FirstOrDefault(r =>
            r.Type == RelationshipType.FRIENDREQUEST &&
            r.Members.Any(m => m.Id == requesterPlayerId));

        if (friendRequest == null)
        {
            throw new InvalidOperationException($"No pending friend request found from player {requesterPlayerId}");
        }

        // Delete the friend request
        await m_GameApiClient.FriendsRelationshipsApi.DeleteRelationshipAsync(
            context,
            context.AccessToken,
            friendRequest.Id,
            cancellationToken: CancellationToken.None
        );

        // Create the friendship
        await CreateFriendRequest(context, requesterPlayerId);

        return await GetFriends(context);
    }
    #endregion
}
