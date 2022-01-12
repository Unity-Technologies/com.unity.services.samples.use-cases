# Welcome to Gaming Operations Samples
_Tested with Unity 2020.3 for PC and Mac._

We're proud to present this collection of samples to demonstrate how to use various Unity Gaming Services together to solve common game development challenges.

### Purpose
This collection of samples is designed to guide developers to use multiple Unity Gaming Services in a single project. These samples demonstrate how various services can be used together to address and implement typical backend game use cases and game design elements, how to resolve specific development tasks, and show the efficiency you can achieve in your game backend by leveraging the Unity Gaming Services packages in your project.

To tackle common challenges game developers may face as they build their game, each of the following samples integrate several Unity Gaming Services to illustrate interactive solutions and detailed tutorials that explain how these services were used.

### Samples Overview
In these 6 sample scenes and their associated script files, you will learn how to implement several common development challenges, including:
* Loot Boxes - Reward players with a random Economy Currency using Cloud Code to perform the Economy grants.
* Daily Rewards - Grant players random collections of both Currencies and Inventory Items at timed intervals.
* Starter Packs - Allow players to purchase a Starter Pack using Cloud Code to implement the one-time-only purchase.
* Seasonal Events - Update game content remotely based on timed special events.
* A/B Testing Level Difficulty - Segment players into multiple test groups in order to determine which variation of a specific variable is the most engaging to the players (in this case, the amount of XP required for leveling up).
* Idle Clicker Mini Game - Update server authoritative game state in real time, similar to idle clicker and social games.

### Getting Started
To test these samples, download this public Gaming Operations Samples repo and open the project in Unity 2020.3 or higher.

Preview all the sample use cases by opening the Start Here scene located in the Assets folder.
Press the Play button and select any of the available use case samples to see how they initialize and demonstrate a solution to each problem.
Pay special attention to the Inspector window when you select each use case sample for detailed information.
To test another sample, simply press the back button in the scene to be returned to the Start Here scene.

Note: The UGS samples in this project were verified to build on Android, iOS, MacOS, and Windows platforms.
However, since the UGS Samples project serves as a demonstration project only, the Unity Services account used in the project only allows read-only testing in the Editor, and therefore does not allow device builds.

Consequently, if you go to Project Settings > Services when exploring the UGS Samples project, you will see a notice saying, "Unable to link project to Unity Services".
Similarly, in the Build Settings window, you will see the message "Unable to access Unity Services".
These notices are expected and triggered because the Samples project is linked to a read-only Unity Services account to provide an out-of-the-box working experience using preset Unity Dashboard settings.

To see an end-to-end, fully functional implementation, from setting up a backend to building similar use cases in your game to a native device, you will need to create your own implementation in your project and backend infrastructure in your own Unity Services account.
See the next section for more guidance on how to create a backend infrastructure in your own Unity Services account.

### Packages Required & Dashboard Setup
These samples use Unity Gaming Services Packages which have already been configured so you can easily explore the use case samples provided.

To use these implementations in your own project, you will need to set up the requisite Packages on the Unity Gaming Services Dashboard for your organization. To learn which packages are required to implement a specific use case sample, navigate to a specific use case, then the "Packages Required" section of that use case README.md file to see which exact packages are required.

Each README also contains a "Dashboard Setup" section, which describes what data has been created on the dashboard for each service used by that Use Case.
