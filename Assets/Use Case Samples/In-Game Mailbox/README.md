# In-Game Mailbox

In-game mailboxes are common mechanisms for game developers to communicate with their players.
You can use them to tell players about new in-game events, gift players items and currency, or to help retain players in the game.

![In Game Mailbox scene](Documentation~/In_Game_Mailbox_scene.png)

## Overview

When a player loads the scene for the first time, they see an inbox with a list of messages waiting for them to read.
On subsequent loads, the inbox is either in the state they left it, or an updated state due to messages expiring between sessions.

Players can interact with the messages, claim their attachments, delete messages, or reset the inbox to a brand new state.

To see this use case in action:

1. In the Unity Editor **Project** window, select **Assets** > **Use Case Samples** > **In-Game Mailbox**, and then double-click `InGameMailboxSample.unity` to open the sample scene.
2. Enter **Play Mode** to interact with the use case.

### Initialization

When the scene loads, the `InGameMailboxSceneManager` script performs the following initialization steps:

1. Initializes Unity Gaming Services.
2. Signs in the player [anonymously](https://docs.unity.com/authentication/UsingAnonSignIn.html) using the Authentication service. If you’ve previously initialized any of the other sample scenes, Authentication will use your cached Player ID instead of creating a new one.
3. Refreshes the economy catalog configuration data from Remote Config (the `ECONOMY_CURRENCIES` and `ECONOMY_ITEMS` keys). If new catalog items were created since the last time the player opened the app, this will initialize those items in the player's configuration.
4. Retrieves and updates currency balances from the player's `player_economy` Cloud Save key for that authenticated user.
5. Uses the sprite addresses stored in each catalog entry's custom data to load all possible currency and inventory item sprites from Addressables.
6. Retrieves the updated message info for the player's inbox:
   1. Downloads the list of all possible messages from Remote Config.
   2. Retrieves the player's current inbox data from Cloud Save.
   3. Checks whether any of the messages already saved in the player's inbox are expired, and deletes them if so.
   4. Checks for any new messages that were downloaded from Remote Config and not yet added to the player's inbox.
   5. Saves the updated inbox state for the player in Cloud Save.
7. Displays the updated list of inbox messages in the scene.

### Functionality

The left panel in the scene displays the list of messages in the player's inbox.
This list updates over time as messages expire, or based on player interaction.

Below the list, a counter displays how many messages are in the inbox and the max number of messages that can be in the inbox at any given time.
When a player loads the scene for the first time, the inbox is full.

When all messages have been deleted from the inbox (either through player interaction or message expiration), a popup appears prompting the player to [reset the inbox](#reset-the-inbox-for-a-specific-audience).

**Note**: This popup is a usability feature of the sample, and would not be an expected interaction in a real-world implementation.

#### Open a message

When you select a message from the list, the following occurs:

1. The full details of the message appears on the right side of the scene.
2. The message is marked as read, and that status is saved to Cloud Save.

If the message has an attachment, there is also an indication of which currency and inventory items are attached, along with a button to claim them.

##### Claim an attachment

When you press the **Claim** button, the client code calls the `InGameMailbox_ClaimAttachment` function of the `SampleGameModule` Cloud Code module with the selected message's ID and the current audience as parameters. The following occurs on the backend:

1. The function reads the Cloud Save inbox data, and locates the message with the supplied ID.
2. The function looks the message up in Remote Config for the given audience. The message must be listed in `MESSAGES_ALL`, and its attachment is taken from Remote Config, never from the inbox: the player writes the inbox, so a modified client could put any attachment there.
3. The function checks the `MESSAGES_CLAIMED` Cloud Save key to make sure the attachment hasn't been claimed yet, and adds the message ID to it.
4. The function grants the attachment's Virtual Purchase. If the purchase fails, the message ID is removed from `MESSAGES_CLAIMED` again.
5. The `message.metadata.hasUnclaimedAttachment` field is set to `false` in the inbox, so the view shows the attachment as claimed.

#### Delete a message

When you press the delete button for a message:

1. The message is removed from the local list of inbox messages.
2. If the inbox was previously full, such that deleting the message created space for a new message, the client rechecks the list of all possible messages downloaded from Remote Config against the message ID of the last message saved in Cloud Save to see if there are any new messages to add to the inbox. If there are, it adds as many of those messages to the inbox as will fit.
3. The updated list of inbox messages is then saved in Cloud Save.
4. Finally, the view refreshes to show the updated list. If the deleted message was previously selected, this also updates the UI to not display the deleted message's detail view.

#### Claim all attachments

When you press the **Claim All** button, the client code makes a call to the `InGameMailbox_ClaimAllAttachments` function of the `SampleGameModule` Cloud Code module. The following occurs on the backend:

1. The function reads the inbox data from Cloud Save.
2. It keeps the inbox messages that Remote Config lists in `MESSAGES_ALL` for the current audience, have an attachment in Remote Config, and are not in `MESSAGES_CLAIMED` yet.
3. It adds those message IDs to `MESSAGES_CLAIMED`, then grants all of their Virtual Purchases in one economy write.
4. For each of those messages, `message.metadata.hasUnclaimedAttachment` is set to `false`, and `message.metadata.isRead` is set to `true`, and the updated message list is saved in Cloud Save.

**Note**: Saving changes in Cloud Save after each attachment is claimed would make the process more fault tolerant. However, it would also require more server calls and therefore be less efficient than the selected approach. It is up to the developer which advantage to prioritize.

#### Delete all read and claimed attachments

When you press the **Delete Read** button:

1. The client loops through the list of inbox messages, and removes each message that has both `message.metadata.isRead` set to `true` and `message.metadata.hasUnclaimedAttachment` set to `false`. This implementation does not delete messages with unclaimed attachments, to prevent players from accidentally deleting messages with available attachments.
2. If the inbox was previously full, such that deleting the message created space for a new message, the client rechecks the list of all possible messages downloaded from Remote Config against the message ID of the last message saved in Cloud Save to see if there are any new messages to add to the inbox. If there are, it adds as many of those messages to the inbox as will fit.
3. The updated list of inbox messages is then saved in Cloud Save.
4. Finally, the view refreshes to show the updated list. If the deleted message was previously selected, this also updates the UI to not display the deleted message's detail view.

#### Reset the inbox for a specific audience

At the bottom of the scene, you can reset the inbox while impersonating a particular audience:

- `Default`
- `All Spenders`
- `Unengaged Players`
- `French Speakers`
- `New Players`

Each of the non-default audiences adds a message to the message list that is specific to that particular audience. These messages are determined by Game Overrides.

When you reset the inbox for the given audience, the following occurs:

1. The scene resets, clearing the selected message field and deleting the Cloud Save inbox data. The client also calls `InGameMailbox_ResetForDebug`, which deletes `MESSAGES_CLAIMED` so the reset inbox can claim its gifts again. This function exists for testing only; remove it from a real game.
2. The client queries Remote Config with the specified audience, to retrieve the list of potential messages that includes any audience specific ones that were previously omitted.
3. The maximum number of messages are added to the inbox from the Remote Config data, and saved in Cloud Save.
4. The view refreshes to show the new list of messages.

#### Open the inventory

When you press the inventory bag icon, the following occurs:

1. The client calls `PlayerEconomy.Instance.GetInventoryAsync()` to refresh the player's list of owned inventory items from the `player_economy` Cloud Save key.
2. A pop-up window displays the resulting list of inventory items.

## Setup

### Requirements

To replicate this use case, you need the following [Unity packages](https://docs.unity3d.com/Manual/Packages.html) in your project:

| **Package**                                                                                                            | **Role**                                                                                                                                                                                                                                                                                                                                                               |
|------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)                                        | Allows developers to retrieve an asset by using its address. In this sample, the service looks up currency and inventory item sprites based on the information stored in each catalog entry's custom data.                                                                                                                                                             |
| [Authentication](https://docs.unity.com/authentication/IntroUnityAuthentication.html)                                  | Automatically signs in the user anonymously to keep track of their data server-side.                                                                                                                                                                                                                                                                                   |
| [Cloud Code](https://docs.unity.com/cloud-code/implementation.html)                                                    | Stores important validation logic server-side. In this use case it is used to validate that message attachments haven't already been claimed, and to process the virtual purchase that grants the attachment rewards through the `SampleGameModule` module.                                                                                                      |
| [Cloud Save](https://docs.unity.com/cloud-save/index.html#Implementation)                                              | Stores the player's inbox state, including the message info downloaded from Remote Config, and the player-specific message metadata like whether the message has been read yet. Also stores the player's wallet and inventory in the `player_economy` key, which the client reads at runtime. The inbox (`MESSAGES_INBOX_STATE`, `MESSAGES_LAST_MESSAGE_DOWNLOADED_ID`) is in the default access class because the client writes it. The record of claimed messages (`MESSAGES_CLAIMED`) and `player_economy` are in the protected [access class](https://docs.unity.com/ugs/en-us/manual/cloud-save/manual/concepts/player-data#Access_Classes): the client can read them and only `SampleGameModule` can write them. |
| [Deployment](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.2)                                      | The Deployment package provides a cohesive interface to deploy assets for Cloud Services.                                                                                                                                                                                                                                                                              |
| [Game Overrides](https://docs.unity3d.com/Packages/com.unity.remote-config@3.2/manual/GameOverridesAndSettings.html)\* | Defines the audience grouping and message data for those messages that we want to only send to a certain audience.                                                                                                                                                                                                                                                     |
| [Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@latest)                                      | Provides key-value pairs where the value that is mapped to a given key can change on the server side, either manually or based on specific Game Overrides. In this sample, we store the message info in Remote Config, and for messages that should only be sent to a particular audience, we store them as blank messages (they'll get filled in by Game Overrides). Remote Config also stores the economy catalog (`ECONOMY_CURRENCIES`, `ECONOMY_ITEMS`, `ECONOMY_VIRTUAL_PURCHASES`), including the virtual purchase info associated with a given message's attachment.  |

\* Note that though it is listed as a package and requires separate dashboard configuration, Game Overrides doesn't actually have an SDK to install from Package Manager. It is a server side offering that affects values returned from other services, like Remote Config.

To use these services in your game, activate each service for your Organization and project in the [Unity Cloud Dashboard](https://dashboard.unity3d.com/).

### Unity Cloud services configuration

To replicate this sample scene's setup in your own Unity project, configure the following items:

- The `SampleGameModule` Cloud Code module
- Remote Config economy catalog values
- Remote Config values
- Remote Config Game Overrides

To configure these items you can [use the Deployment package](#using-the-deployment-package), or [manually enter them using the Unity Cloud Dashboard](#using-the-unity-cloud-dashboard).
The recommended best practice is to use the Deployment package as it greatly accelerates this process.

#### Using the Deployment package

To deploy configurations using the Deployment package:

1. Open the [Deployment window](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.2/manual/deployment_window.html).
1. Check in `Common` and `In-Game Mailbox`.
1. Click `Deploy Selection`.

This deploys the following items:

- The `SampleGameModule` Cloud Code module
- Remote Config economy catalog values
- Remote Config values
- Remote Config Game Overrides

#### Using the Unity Cloud Dashboard

You can use the [Unity Cloud Dashboard](dashboard.unity3d.com) to manually configure your services by project and environment.
Refer to the following sections to configure this sample.

##### Game Overrides

Configure the following [Overrides](https://docs.unity.com/gameoverrides/CreateAnOverride.html) in the Unity Cloud Dashboard:

| **Details**    | Name the Override “Messages All Spenders Overrides”.                                                                                                                                                                                                                                                                                                                                                                                             |
|----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Targeting**  | Select **JEXL** with the following JEXL code:<br/>user.audience == "AllSpenders" (See **Note**, below)                                                                                                                                                                                                                                                                                                                                           |
| **Content**    | Select **Choose content type** > **Config Overrides**, then enter override values for the following keys:<br/><ul><li>`MESSAGE_001`: </li><ul>{<ul>`"title"`: "Thank you for supporting us!",<br/>`"content"`: "This message specifically targets Spender players, thanks to Game Overrides that enables other Unity services to target pre-defined or custom audiences.",<br/>`"attachment"`: "",<br/>`"expirationPeriod"`: "0.00:03:00.00"<br></ul>} |
| **Scheduling** | Set the following start and end dates:<ul><li>Set **Start Date** to **Update content immediately**.</li><li>Set **End Date** to **Run indefinitely**.</li></ul>                                                                                                                                                                                                                                                                                  |
| **Status**     | After finishing creating the Game Override, click `Enable`                                                                                                                                                                                                                                                                                                                                                                                       |

| **Details**    | Name the Override “Messages French Speaker Overrides”.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
|----------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Targeting**  | Select **JEXL** with the following JEXL code:<br/>user.audience == "FrenchSpeakers" (See **Note**, below)                                                                                                                                                                                                                                                                                                                                                                                                                                    |
| **Content**    | Select **Choose content type** > **Config Overrides**, then enter override values for the following keys:<br/><ul><li>`MESSAGE_002`: </li><ul>{<ul>`"title"`: "Oh oui, le message!",<br/>`"content"`: "Et oui, ce message est en français car il cible les joueurs dont la langue du jeu est paramétrée en français. Ceci est rendu possible grâce à Game Overrides qui permet à certains services de Unity  de cibler des audiences pré-définies ou personnalisées.",<br/>`"attachment"`: "",<br/>`"expirationPeriod"`: "0.00:03:00.00"<br></ul>} |
| **Scheduling** | Set the following start and end dates:<ul><li>Set **Start Date** to **Update content immediately**.</li><li>Set **End Date** to **Run indefinitely**.</li></ul>                                                                                                                                                                                                                                                                                                                                                                              |
| **Status**     | After finishing creating the Game Override, click `Enable`                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |

| **Details**    | Name the Override “Messages New Players Overrides”.                                                                                                                                                                                                                                                                                                                                                                                                              |
|----------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Targeting**  | Select **JEXL** with the following JEXL code:<br/>user.audience == "NewPlayers" (See **Note**, below)                                                                                                                                                                                                                                                                                                                                                            |
| **Content**    | Select **Choose content type** > **Config Overrides**, then enter override values for the following keys:<br/><ul><li>`MESSAGE_003`: </li><ul>{<ul>`"title"`: "Welcome to the game!",<br/>`"content"`: "This message specifically targets New Players, thanks to Game Overrides that enables other Unity services to target pre-defined or custom audiences.",<br/>`"attachment"`: "MESSAGE_003_GIFT_NEW_PLAYERS",<br/>`"expirationPeriod"`: "0.00:10:00.00"<br></ul>} |
| **Scheduling** | Set the following start and end dates:<ul><li>Set **Start Date** to **Update content immediately**.</li><li>Set **End Date** to **Run indefinitely**.</li></ul>                                                                                                                                                                                                                                                                                                  |
| **Status**     | After finishing creating the Game Override, click `Enable`                                                                                                                                                                                                                                                                                                                                                                                                       |

| **Details**    | Name the Override “Messages Unengaged Players Overrides”.                                                                                                                                                                                                                                                                                                                                                                                                                |
|----------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Targeting**  | Select **JEXL** with the following JEXL code:<br/>user.audience == "UnengagedPlayers" (See **Note**, below)                                                                                                                                                                                                                                                                                                                                                              |
| **Content**    | Select **Choose content type** > **Config Overrides**, then enter override values for the following keys:<br/><ul><li>`MESSAGE_004`: </li><ul>{<ul>`"title"`: "Welcome back to the game!",<br/>`"content"`: "This message specifically targets Unengaged players, thanks to Game Overrides that enables other Unity services to target pre-defined or custom audiences.",<br/>`"attachment"`: "MESSAGE_004_GIFT_UNENGAGED",<br/>`"expirationPeriod"`: "0.00:10:00.00"<br></ul>} |
| **Scheduling** | Set the following start and end dates:<ul><li>Set **Start Date** to **Update content immediately**.</li><li>Set **End Date** to **Run indefinitely**.</li></ul>                                                                                                                                                                                                                                                                                                          |
| **Status**     | After finishing creating the Game Override, click `Enable`                                                                                                                                                                                                                                                                                                                                                                                                               |

**Note**: This sample determines which Game Override data should be returned based on a JEXL match with the audience value specified in the client. This is so that the use case can fake a player being in different audiences on demand. In a real app, the Game Overrides would likely be set up to use built-in or custom-defined Analytics audiences for targeting (i.e. during Game Override's targeting step, choose `Stateful (Audiences)` and check the appropriate analytics audience from the list or click `Build a new Audience`).  

##### Cloud Code

The `SampleGameModule` C# Cloud Code module provides the following [module functions](https://docs.unity.com/ugs/en-us/manual/cloud-code/manual/modules):

| **Module function**                 | **Parameters**                                                                                               | **Description**                                                                                                                                                                                                                     | **Location in project**                                                                       |
|-------------------------------------|--------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| `InGameMailbox_ClaimAttachment`     | `messageId`<br><br>`STRING`<br><br>The id of the message that owns the attachment the player wants to claim.<br><br>`audience`<br><br>`STRING`<br><br>The audience the inbox was downloaded for. | Fetches the message's attachment from Remote Config for the audience, validates that it hasn't already been claimed, records it in `MESSAGES_CLAIMED`, makes that Virtual Purchase, and marks the attachment as claimed.        | `SampleGameModule/Project/Services/InGameMailboxService.cs`                                   |
| `InGameMailbox_ResetForDebug`       | none                                                                                                         | Deletes `MESSAGES_CLAIMED`, so a reset inbox can claim its gifts again. For testing only.                                                                                                     | `SampleGameModule/Project/Services/InGameMailboxService.cs`                                   |
| `InGameMailbox_ClaimAllAttachments` | `audience`<br><br>`STRING`<br><br>The audience the inbox was downloaded for.                                 | Gets the list of messages in a player's inbox, finds all messages that have an unclaimed attachment, and makes each Virtual Purchase, marking each message as read and attachment as claimed. | `SampleGameModule/Project/Services/InGameMailboxService.cs`                                   |

To publish them, deploy the `SampleGameModule` module (in the repo root `SampleGameModule/` folder) through `Assets/Common/Config as Code/SampleGameModule.ccmr` in the Deployment window, or upload it from the Unity Cloud Dashboard.

##### Economy catalog (Remote Config)

The economy catalog is stored in Remote Config and deployed from `Assets/Common/Config as Code/economy_config.rc`. The following resources are defined under the `ECONOMY_CURRENCIES` and `ECONOMY_ITEMS` keys:

| **Resource type** | **Resource item** | **ID**    | **Custom Data**                                   | **Description**                             |
|-------------------|-------------------|-----------|---------------------------------------------------|---------------------------------------------|
| Currency          | Gem               | `GEM`     | `{ "spriteAddress": "Sprites/Currency/Gem" }`     | A premium currency gifted by some messages. |
| Currency          | Coin              | `COIN`    | `{ "spriteAddress": "Sprites/Currency/Coin" }`    | A soft currency gifted by some messages.    |
| Inventory Item    | Sword             | `SWORD`   | `{ "spriteAddress": "Sprites/Inventory/Sword" }`  | An inventory item gifted by some messages.  |
| Inventory Item    | Shield            | `SHIELD`  | `{ "spriteAddress": "Sprites/Inventory/Shield" }` | An inventory item gifted by some messages.  |

In addition, the following virtual purchases are defined as entries in the `ECONOMY_VIRTUAL_PURCHASES` key:

| **Item name**                          | **ID**                         | **This purchase rewards**         | **This purchase costs** |
|----------------------------------------|--------------------------------|-----------------------------------|-------------------------|
| Message 003 Gift for New Players       | `MESSAGE_003_GIFT_NEW_PLAYERS` | Sword (1), Shield (1), Coin (100) | none                    |
| Message 004 Gift for Unengaged Players | `MESSAGE_004_GIFT_UNENGAGED`   | Gem   (50)                        | none                    |
| Message 005 Gift                       | `MESSAGE_005_GIFT`             | Coin  (50)                        | none                    |
| Message 008 Gift                       | `MESSAGE_008_GIFT`             | Coin  (100), Gem (50)             | none                    |
| Message 010 Gift                       | `MESSAGE_010_GIFT`             | Gem   (50)                        | none                    |
| Message 011 Gift                       | `MESSAGE_011_GIFT`             | Sword (1)                         | none                    |

\* There is no cost associated with these Virtual Purchases because they are all gifts to the player reading the message.

##### Remote Config values

Set up the following [config values](https://docs.unity.com/remote-config/HowDoesRemoteConfigWork.html) in the Unity Cloud Dashboard:

| **Key**        | **Type** | **Description**                                                                                                                        | **Value**                                                                                                                                                                                                                                                                                                                                                                                          |
|----------------|----------|----------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `MESSAGES_ALL` | JSON     | The JSON list of all possible message ids that exist in the Remote Config dashboard, listed in the order they should be downloaded in. | {<ul> `"messageList"`: [<ul>"MESSAGE_001",</br>"MESSAGE_002",</br>"MESSAGE_003",</br>"MESSAGE_004",</br>"MESSAGE_005",</br>"MESSAGE_006",</br>"MESSAGE_007",</br>"MESSAGE_008",</br>"MESSAGE_009",</br>"MESSAGE_010",</br>"MESSAGE_011"</ul>]</ul>}                                                                                                                                                |
| `MESSAGE_001`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "",</br>`"content"`: "",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                                                                                                   |
| `MESSAGE_002`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "",</br>`"content"`: "",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                                                                                                   |
| `MESSAGE_003`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "",</br>`"content"`: "",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                                                                                                   |
| `MESSAGE_004`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "",</br>`"content"`: "",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                                                                                                   |
| `MESSAGE_005`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "Got new Use Case sample ideas?",</br>`"content"`: "We’d love to hear your suggestions about what kind of new samples you would like us to deliver. Let us know at headstart@unity3d.com",</br>`"attachment"`: "MESSAGE_005_GIFT",</br>`"expirationPeriod"`: "0.00:10:00.00"</br></ul>}                                                                                                 |
| `MESSAGE_006`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "New update coming soon",</br>`"content"`: "Our next update will be released soon and will require a client update from the App Store or Google Play Store. No, that’s a joke. Our samples are only available on Github for now ;)",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                       |
| `MESSAGE_007`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "There's cake at the end",</br>`"content"`: "The cake is a lie.",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                                                          |
| `MESSAGE_008`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "The new update is here!!",</br>`"content"`: "We hope you will enjoy the new content the team has prepared for you. On the menu: this brand new In-Game messaging sample and an updated version of the Idle Clicker Game sample that now showcases merging and evolving inventory items.",</br>`"attachment"`: "MESSAGE_008_GIFT",</br>`"expirationPeriod"`: "0.00:10:00.00"</br></ul>} |
| `MESSAGE_009`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "Where is my mind?",</br>`"content"`: "This is a very uninspired and uninspiring message. Our apologies.",</br>`"attachment"`: "",</br>`"expirationPeriod"`: "0.00:03:00.00"</br></ul>}                                                                                                                                                                                                 |
| `MESSAGE_010`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "Server Downtime",</br>`"content"`: "Our servers were offline for 2 hours yesterday for unexpected reasons. Please accept our apologies and this gift as a compensation. Of course, this is only for illustration purposes; UGS Use Case samples are never offline!",</br>`"attachment"`: "MESSAGE_010_GIFT",</br>`"expirationPeriod"`: "0.00:10:00.00"</br></ul>}                      |
| `MESSAGE_011`  | JSON     | One of the messages that a player could receive.                                                                                       | {<ul> `"title"`: "It's dangerous to go alone",</br>`"content"`: "Take this!",</br>`"attachment"`: "MESSAGE_011_GIFT",</br>`"expirationPeriod"`: "0.00:10:00.00"</br></ul>}                                                                                                                                                                                                                               |
