# Unity Gaming Services Use Cases

This Unity Gaming Services (UGS) Samples package contains a collection of samples designed to show you how you can use multiple UGS products to solve common game development challenges. These samples implement typical backend game use cases and game design elements, show how to resolve specific development tasks, and highlight the efficiency you can achieve in your game backend by integrating different UGS packages in your project.

## Table of Contents

- [Getting Started](#getting-started)
- [List of Samples](#list-of-samples)
- [Using your own Unity Services Account](#using-your-own-unity-services-account)
- [Feedback and Samples Requests](#feedback-and-sample-requests)

## Getting Started

- Download Unity Editor 2021.3 or later
  - [Unity Hub](unityhub://2021.3.32f1/3b9dae9532f5)
  - [Download (Win)](https://download.unity3d.com/download_unity/3b9dae9532f5/UnityDownloadAssistant-2021.3.32f1.exe)
  - [Download (Mac)](https://download.unity3d.com/download_unity/3b9dae9532f5/UnityDownloadAssistant-2021.3.32f1.dmg)
- Clone the repository: `git clone https://github.com/Unity-Technologies/com.unity.services.samples.use-cases.git`
- Open the project from the Unity Hub or Unity Editor

To view each sample in action, open the Start Here scene in the Assets directory and hit Play. To review an individual sample, find the use case directory in Assets/Use Case Samples and view the README file for implementation details.

_Note: This project is tied to a Unity Services Account that allows read-only testing in the Editor. The messages "Unable to link project to Unity Services" in Project Settings and "Unable to access Unity Services" in Build Settings are expected. Additionally, you will be unable to create a device build of this project._

_Note: This project uses the Unity Mediation package, which requires [CocoaPods](https://cocoapods.org/) to be installed if building for iOS. If your project uses the Unity Mediation package, see that package's [documentation](https://docs.unity.com/mediation) on [requirements](https://docs.unity.com/mediation/SDKIntegrationUnityRequirements.html) and [troubleshooting](https://docs.unity.com/mediation/TroubleshootingIntegrationsiOS.html), before building for iOS._

## List of Samples

- [A/B Test on Game Difficulty](Assets/Use%20Case%20Samples/AB%20Test%20Level%20Difficulty/README.md) - Segment players into multiple test groups in order to determine which variation of a specific variable is the most engaging to the players (in this case, the amount of XP required for leveling up).
- [Battle Pass](Assets/Use%20Case%20Samples/Battle%20Pass/README.md) - A seasonal reward tier system with a free track and a premium track.
- [Cloud AI Mini Game](Assets/Use%20Case%20Samples/Cloud%20AI%20Mini%20Game/README.md) - Server authoritative gameplay in a simple Tic-Tac-Toe game played against AI running on UGS with persistent state, Currency rewards, stats, and straightforward AI.
- [Command Batching](Assets/Use%20Case%20Samples/Command%20Batching/README.md) - Group game Commands into a queue and process on the server in a single batch to reduce the volume and frequency of server calls made during gameplay.
- [Daily Rewards](Assets/Use%20Case%20Samples/Daily%20Rewards/README.md) - A prevalent engagement feature that can boost retention by showing players an escalating series of rewards incentivizes them to keep logging in to claim better and better prizes.
- [Idle Clicker Game](Assets/Use%20Case%20Samples/Idle%20Clicker%20Game/README.md) - Update server authoritative game state in real time, similar to idle clicker and social games with a merging mechanic and a generic Unlock Manager.
- [In-Game Mailbox](Assets/Use%20Case%20Samples/In-Game%20Mailbox/README.md) - Demonstrates a way that developers can send in-game messages to their players, including with gifts of various game currencies and inventory items.
- [Loot Boxes](Assets/Use%20Case%20Samples/Loot%20Boxes/README.md) - Reward players with a random Economy currency using Cloud Code to perform the Economy grants.
- [Loot Boxes With Cooldown](Assets/Use%20Case%20Samples/Loot%20Boxes%20With%20Cooldown/README.md) - Grant players random collections of both Currencies and Inventory Items at timed intervals.
- [Seasonal Events](Assets/Use%20Case%20Samples/Seasonal%20Events/README.md) - Update game content remotely based on timed special events.
- [Serverless Multiplayer Game](Assets/Use%20Case%20Samples/Serverless%20Multiplayer%20Game/README.md) - Demonstrates how to utilize game lobbies and compete in a simple, serverless, real-time arena-style game where players collect coins for points.
- [Starter Packs](Assets/Use%20Case%20Samples/Starter%20Pack/README.md) - Allow players to purchase a Starter Pack using Cloud Code to implement the one-time-only purchase.
- [Virtual Shop](Assets/Use%20Case%20Samples/Virtual%20Shop/README.md) - Demonstrates a key feature in many games: allowing players to use in-game currency to purchase items and resources to facilitate a server-authoritative in-game economy with multiple store pages and server-managed badges.

_Tested with Unity 2021.3 for PC and Mac._

## Using your own Unity Services Account

These samples use UGS packages which have already been configured so you can easily explore the use case samples provided.

To use these implementations in your own project, you will need to setup and configure the services used in your own UGS Dashboard. Learn more about each configuration in the README.md file within each samples directory.

## Feedback and Sample Requests

Your opinion matters to us, and we would like to hear from you so we can provide you with the best learning material that’ll help you become a Unity Gaming Services expert in no time.

You can let us know how we're doing by filling out [this anonymous, short survey](https://unitysoftware.co1.qualtrics.com/jfe/form/SV_eE6DomzzTS5YO6a) - it should take less than a minute to fill out!

Alternatively, you can reach us for questions, or to offer feedback or ideas for new sample use cases in the [Unity Gaming Services Forum](https://forum.unity.com/forums/unity-gaming-services-general-discussion.561/) (we even have a helpful [tag](https://forum.unity.com/tags/unity-gaming-services-samples/) to filter by!).
Also, while in Play Mode in the samples project, you can submit anonymous feedback by using the feedback button in the bottom left corner of each scene.

## Cloud Diagnostics & User Reporting

Unity's Cloud Diagnostics service helps to ensure the quality of this project by automatically sending diagnostic data to the dashboard when you encounter errors.
Additionally, the User Reporting feature lets you submit a screenshot and detailed feedback about a sample by clicking the Feedback button in any of the Sample scenes during Play Mode.

To set up Cloud Diagnostics and User Reporting in your own project, follow the set-up instructions provided in the dashboard.
[Setting up Cloud Diagnostics](https://unitytech.github.io/clouddiagnostics/userreporting/UnityCloudDiagnosticsSettingUp.html)

_Note: If you don't want to automatically send exception data, disable Cloud Diagnostics under Project Settings > Services > Cloud Diagnostics._
