# Loot Boxes

Loot boxes are virtual items that players can win, earn, or purchase, and then open to receive a randomized selection of items. Rewards can vary greatly depending on the game's genre, theme, and virtual economy. They can positively impact retention, supplement live events, and pique the curiosity of old and new players alike.

This sample demonstrates how to set up a basic loot box that grants random currency to players.

![Loot Boxes scene](Documentation~/Loot_Boxes_Scene.png)

## Overview

To see this use case in action:

1. In the Unity Editor **Project** window, select **Assets** > **Use Case Samples** > **Loot Boxes**, and then double-click `LootBoxesSample.unity` to open the sample scene.
2. Enter Play Mode to interact with the use case.

### Initialization

The `LootBoxesSceneManager.cs` script performs the following initialization tasks:

1. Initializes Unity Gaming Services.
2. Signs in the player [anonymously](https://docs.unity.com/authentication/UsingAnonSignIn.html) using the Authentication service. If you’ve previously initialized any of the other sample scenes, Authentication will use your cached Player ID instead of creating a new one.
3. Retrieves and updates currency balances from the Economy service for that authenticated user.

### Functionality

When you click the **Open Loot Box** button, you receive a random amount of rewards from the available pool (indicated in the currency HUD). The following occurs on the backend:

1. The button's `OnClick` method calls the `GetRandomCurrency` script from the Cloud Code service, which picks a random quantity of a random currency from an internal list to reward the user.
2. Cloud Code calls the Economy service directly to grant the awarded currency and update the player's balance.
3. Cloud Code returns the results to the client and updates the UI.

## Setup

### Requirements

To replicate this use case, you need the following [Unity packages](https://docs.unity3d.com/Manual/Packages.html) in your project:

| **Package**                                                                           | **Role**                                                                                             |
|---------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| [Authentication](https://docs.unity.com/authentication/IntroUnityAuthentication.html) | Automatically signs in the player as an anonymous user to keep track of their data server-side.      |
| [Cloud Code](https://docs.unity.com/cloud-code/implementation.html)                   | Picks and grants random currency for the loot box through the Economy server and returns the result. |
| [Economy](https://docs.unity.com/economy/implementation.html)                         | Retrieves the starting and updated currency balances at runtime.                                     |
| [Deployment](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.2)     | The Deployment package provides a cohesive interface to deploy assets for Cloud Services.            |

To use these services in your game, activate each service for your Organization and project in the [Unity Dashboard](https://dashboard.unity3d.com/).

### Unity Cloud services configuration

To replicate this sample scene's setup in your own Unity project, we need to configure the following items:

- Cloud Code scripts
- Economy items

There are two main ways of doing this, either by [using the Deployment package](#using-the-deployment-package), or by [manually entering them using the Dashboard](#using-the-dashboard).
We recommend the usage of the Deployment package since it will greatly accelerate this process.

#### Using the Deployment package

Here are the steps to deploy configuration using the Deployment package:

1. Open the [Deployment window](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.2/manual/deployment_window.html)
1. Check in `Common` and `Loot Boxes`
1. Click `Deploy Selection`

This will deploy all the necessary items.

#### Using the Dashboard

The [Dashboard](dashboard.unity3d.com) enables you to edit manually all your services configuration by project and environment.
Here are the details necessary for the configuration of the current sample.

##### Cloud Code

[Publish the following script](https://docs.unity.com/cloud-code/implementation.html#Writing_your_first_script) in the **LiveOps** dashboard:

| **Script**                      | **Parameters** | **Description**                                                                                                                                  | **Location in project**                                                              |
|---------------------------------|----------------|--------------------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------|
| `LootBoxes_GrantRandomCurrency` | None           | Picks a random quantity of a random currency from an internal list to reward the user, grants it on the Economy service, and returns the result. | `Assets/Use Case Samples/Loot Boxes/Config as Code/LootBoxes_GrantRandomCurrency.js` |

##### Economy

[Configure the following resources](https://docs.unity.com/economy/) in the **LiveOps** dashboard:

| **Resource type** | **Resource name** | **ID**  | **Description**              |
|-------------------|-------------------|---------|------------------------------|
| Currency          | Coin              | `COIN`  | A potential loot box reward. |
| Currency          | Gem               | `GEM`   | A potential loot box reward. |
| Currency          | Pearl             | `PEARL` | A potential loot box reward. |
| Currency          | Star              | `STAR`  | A potential loot box reward. |
