# Loot Boxes
Games of various genres - from team-based multiplayers to first person shooters - make use of loot boxes and grand random currency, items, and equipment to the player to reward them for leveling up or completing long stretches of gameplay without quitting. Loot boxes are a great way for players to feel engaged and motivated to keep playing and get rewarded for it.

This sample shows how to set up a basic loot box in your game, or in other words, how to grant random currency to players.

### Implementation Overview
This sample demonstrates how to initialize Unity Services, log in, retrieve & update Currency balances from the Economy service and call Cloud Code to pick a currency at random from an internal list, choose a random quantity, then call the Currency service directly to grant the Currency, and return the results to the calling Unity C# script.

### Packages Required
- **Economy:** Retrieves the starting and updated currency balances at runtime.
- **Cloud Code:** Picks and grants random currency for the loot box through the Economy server and returns the result.

See [Economy](https://docs.unity.com/Economy) and [Cloud Code](https://docs.unity.com/Cloud-Code) docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, and Cloud Code services in your game, activate each service for your organization and project in the Unity Dashboard.

#### Economy Items
* Coin - `ID: "COIN"` - a loot box reward item
* Gem - `ID: "GEM"` - a loot box reward item
* Pearl - `ID: "PEARL"` - a loot box reward item
* Star - `ID:"STAR"` - a loot box reward item

#### Cloud Code Scripts
* ScriptName:
  * Parameters: `none`
  * Script: `StreamingAssets/Grant Random Currency/GrantRandomCurrency.js`

_**Note**:
The Cloud Code scripts included in StreamingAssets are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
