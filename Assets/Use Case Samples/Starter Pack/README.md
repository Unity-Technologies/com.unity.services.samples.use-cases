# Starter Pack
It's common in games to offer a one-time virtual purchase, such as a Starter Pack, to give players a boost
with getting started with first playing your game or when players delete their game save and start again.

This sample shows how to create a one-time deal Starter Pack in your game that a player can purchase with in-game currency.

### Implementation Overview
First, decide the price of a Starter Pack and ensure the player has enough in-game currency to purchase one.
This sample uses gems as the in-game currency.
To simplify a purchase for a player, this sample scene shows a button that grants a
player 10 gems when they click it, so they have enough gems for a Starter Pack.

When a player presses the button to buy the Starter Pack, a request is sent to the Cloud Code service to execute the PurchaseStarterPack script.

The Cloud Code script:
- Verifies the player has not yet claimed this one-time deal.
- Makes the purchase directly with the Economy service.
- Sends an update to Cloud Save directly so we know in the future that this player has already claimed this one-time deal.

After purchasing a Starter Pack, the player can't claim another one unless they reset their game save.
To illustrate this, this sample scene shows a button to reset the Starter Pack flag so the player can purchase it again.

### Packages Required
These packages were used to implement this Starter Pack sample.

- **Authentication:** Automatically signs in the user anonymously to keep track of their data on the server side.
- **Economy:** Keeps track of the player's currencies and inventory.
- **Cloud Code:** Keeps important validation logic on the server side.
- **Cloud Save:** We store small pieces of special information in Cloud Save, such as a flag that says this user did already claim their Starter Pack.

See the [Authentication](http://documentation.cloud.unity3d.com/en/articles/5385907-unity-authentication-anonymous-sign-in-guide),
[Economy](https://docs.unity.com/economy), [Cloud Code](https://docs.unity.com/cloud-code), and [Cloud Save](https://docs.unity.com/cloud-save)
docs to learn how to install and configure these SDKs in your project.

### Dashboard Setup
To use Economy, Cloud Code, and Cloud Save services in your game, activate each service for your organization and project in the Unity Dashboard.
You'll need a few currencies and inventory items in the Economy setup, as well as a couple of scripts in Cloud Code:

#### Economy Items
* Gem - `ID: "GEM"` - The currency cost of the Starter Pack
* Coin - `ID: "COIN"` - A currency reward from the Starter Pack
* Pearl - `ID: "PEARL"` - A currency reward from the Starter Pack
* Sword - `ID: "SWORD"` - An inventory item reward from the Starter Pack

#### Cloud Code Scripts
* PurchaseStarterPack:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Starter Pack/Cloud Code/PurchaseStarterPack.js`
* ResetStarterPackFlag:
  * Parameters: `none`
  * Script: `Assets/Use Case Samples/Starter Pack/Cloud Code/ResetStarterPackFlag.js`

_**Note**:
The Cloud Code scripts included in the `Cloud Code` folder are just local copies, since you can't see the sample's dashboard. Changes to these scripts will not affect the behavior of this sample since they will not be automatically uploaded to Cloud Code service._
