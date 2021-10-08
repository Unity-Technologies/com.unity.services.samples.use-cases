# Game Operations Samples

_Tested with Unity 2020.3 for Mac and Windows._

This project demonstrates several common ways in which [Unity Gaming Services](https://unity.com/solutions/gaming-services) are used with each other for game operations purposes. This includes things like daily rewards, seasonal events, and tuning the game balance and economy without releasing an update to the game.

> **Note**: This project is configured to use a shared read-only Unity Gaming Services account so that the samples will work for you right away. However, this means that you won't be able to log into the Unity Dashboard to see the server-side configuration for this project. Each use case has a README file which explains how the project is configured on the server.

> **Note**: Unity Gaming Services are currently in Open Beta. To join the Open Beta and use the new Services, contact your Unity representative.

### Demonstrated Use Cases:

* **Grant Random Currency**: The server grants the current player a bit of in-game currency.
* **Grant Timed Random Reward**: The server grants the current player a recurring reward for a set time period.
* **Seasonal Events**: Rewards, visual theme, and other content changes based on the current server time.
* **Starter Pack**: A special virtual purchase that can only be purchased once, but can be offered again if the player resets their game.
* **A/B Game Difficult/Balance Testing** - Try out multiple different gameplay balance adjustments across different random player groups to see which values work best for your game.

## Getting Started

Clone this repository and open the working copy as a project in Unity (version 2020.3 or newer).

## Running the Samples

Open the scene named "Start Here". From there, you can choose which use case you want to see demonstrated. After you're done with a use case, stop Play Mode and reload the Start Here scene to choose another use case.
