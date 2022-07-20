# Unity Gaming Services Use Cases

This Unity Gaming Services (UGS) Samples package contains a collection of samples designed to show you how you can use multiple UGS products to solve common game development challenges. These samples implement typical backend game use cases and game design elements, show how to resolve specific development tasks, and highlight the efficiency you can achieve in your game backend by integrating different UGS packages in your project.

## Table of Contents

- [Getting Started](#getting-started)
- [List of Samples](#list-of-samples)
- [Using your own Unity Services Account](#using-your-own-unity-services-account)
- [Feedback and Samples Requests](#feedback-and-sample-requests)

## Getting Started

- Download Unity Editor 2020.3 or later
  - [Unity Hub](unityhub://2020.3.30f1/1fb1bf06830e)
  - [Download (Win)](https://download.unity3d.com/download_unity/1fb1bf06830e/UnityDownloadAssistant-2020.3.30f1.exe)
  - [Download (Mac)](https://download.unity3d.com/download_unity/1fb1bf06830e/UnityDownloadAssistant-2020.3.30f1.dmg)
- Clone the repository: `git clone https://github.com/Unity-Technologies/com.unity.services.samples.use-cases.git`
- Open the project from the Unity Hub or Unity Editor

To view each sample in action, open the Start Here scene in the Assets directory and hit Play. To review an individual sample, find the use case directory in Assets/Use Case Samples and view the README file for implementation details.

_Note: This project is tied to a Unity Services Account that allows read-only testing in the Editor. The messages "Unable to link project to Unity Services" in Project Settings and "Unable to access Unity Services" in Build Settings are expected. Additionally, you will be unable to create a device build of this project._

## List of Samples

- [A/B Testing Level Difficulty](Assets/Use%20Case%20Samples/AB%20Test%20Level%20Difficulty/README.md) - Segment players into multiple test groups in order to determine which variation of a specific variable is the most engaging to the players (in this case, the amount of XP required for leveling up).
- [Battle Pass](Assets/Use%20Case%20Samples/Battle%20Pass/README.md) - A seasonal reward tier system with a free track and a premium track.
- [Cloud AI Mini Game](Assets/Use%20Case%20Samples/Cloud%20AI%20Mini%20Game/README.md) - Server authoritative gameplay in a simple Tic-Tac-Toe game played against AI running on UGS with persistent state, Currency rewards, stats, and straightforward AI.
- [Command Batching](Assets/Use%20Case%20Samples/Command%20Batching/README.md) - Group game Commands into a queue and process on the server in a single batch to reduce the volume and frequency of server calls made during gameplay.
- [Daily Rewards](Assets/Use%20Case%20Samples/Daily%20Rewards/README.md) - A prevalent engagement feature that can boost retention by showing players an escalating series of rewards incentivizes them to keep logging in to claim better and better prizes.
- [Idle Clicker Mini Game](Assets/Use%20Case%20Samples/Idle%20Clicker%20Game/README.md) - Update server authoritative game state in real time, similar to idle clicker and social games.
- [Loot Boxes](Assets/Use%20Case%20Samples/Loot%20Boxes/README.md) - Reward players with a random Economy currency using Cloud Code to perform the Economy grants.
- [Loot Boxes With Cooldown](Assets/Use%20Case%20Samples/Loot%20Boxes%20With%20Cooldown/README.md) - Grant players random collections of both Currencies and Inventory Items at timed intervals.
- [Over-The-Air Content Delivery](Assets/Use%20Case%20Samples/Over-The-Air%20Content/README.md) - Add new downloaded content to a game while the game is running.
- [Rewarded Ads With Unity Mediation](Assets/Use%20Case%20Samples/Rewarded%20Ads%20With%20Unity%20Mediation/README.md) - Offer players opportunity to boost level end rewards by interacting with a reward booster meter and watching a rewarded ad.
- [Starter Packs](Assets/Use%20Case%20Samples/Starter%20Pack/README.md) - Allow players to purchase a Starter Pack using Cloud Code to implement the one-time-only purchase.
- [Seasonal Events](Assets/Use%20Case%20Samples/Seasonal%20Events/README.md) - Update game content remotely based on timed special events.
- [Virtual Shop](Assets/Use%20Case%20Samples/Virtual%20Shop/README.md) - Demonstrates a key feature in many games: allowing players to use in-game currency to purchase items and resources to facilitate a server-authoritative in-game economy with multiple store pages and server-managed badges.

_Tested with Unity 2020.3 for PC and Mac._

## Using your own Unity Services Account

These samples use UGS packages which have already been configured so you can easily explore the use case samples provided.

To use these implementations in your own project, you will need to setup and configure the services used in your own UGS Dashboard. Learn more about each configuration in the README.md file within each samples directory.

## Feedback and Sample Requests

If you have feedback or would like us to demonstrate a new use-case with a sample, please let us know in the [Unity Gaming Services Forum](https://forum.unity.com/forums/unity-gaming-services-general-discussion.561/). We even have a helpful [tag](https://forum.unity.com/tags/unity-gaming-services-samples/) to filter by!

Additionally, you can submit feedback directly from the samples project by using the feedback button in the bottom left corner of each scene during Play Mode.

## Cloud Diagnostics & User Reporting

Unity's Cloud Diagnostics service helps to ensure the quality of this project by automatically sending diagnostic data to the dashboard when you encounter errors.
Additionally, the User Reporting feature lets you submit a screenshot and detailed feedback about a sample by clicking the Feedback button in any of the Sample scenes during Play Mode.

To set up Cloud Diagnostics and User Reporting in your own project, follow the set-up instructions provided in the dashboard.
[Setting up Cloud Diagnostics](https://unitytech.github.io/clouddiagnostics/userreporting/UnityCloudDiagnosticsSettingUp.html)

_Note: If you don't want to automatically send exception data, disable Cloud Diagnostics under Project Settings > Services > Cloud Diagnostics._

