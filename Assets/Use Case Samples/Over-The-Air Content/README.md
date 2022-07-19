# Over-The-Air Content Delivery

When you want to add new content to your game, releasing a new version of your application that users must then install before they can play can result in a poor user experience. As an alternative, you can use an Over the Air Content approach, so that the player can open the application and begin interacting with the game while new content downloads in the background or during idle times.

This sample uses a menu screen to download and install new content without leaving the game.

![Over-The-Air Content Delivery scene](Documentation~/Over-The-Air_Content_Delivery_scene.png)

## Overview

This sample demonstrates how to download new content from the cloud while the game is running. The client pings the Remote Config service, which informs the client that there is new content, and where to get it.

**Note**: This sample primarily uses Addressables with Cloud Content Delivery to add new content to a game while it is running. The Remote Config, Economy, and Cloud Code service functions used are not strictly required for the content delivery part to work.

### Initialization

When the scene loads, the `OverTheAirContentSceneManager.cs` script performs the following initialization tasks:

1. Initializes Unity Gaming Services.
2. Signs in the player [anonymously](https://docs.unity.com/authentication/UsingAnonSignIn.html) using the Authentication service.
If you’ve previously initialized any of the other sample scenes,
Authentication will use your cached Player ID instead of creating a new one.
3. Downloads all configuration data from Remote Config, some of which is necessary to detect and download new content.

### Usage

When the scene loads, you'll see a **Begin** button. When you press it, an interstitial screen appears, and the new content automatically begins downloading. When the download completes, you can click the **Play** button to continue to view and interact with the new content.

You can click one of the two **Restart Sample** buttons to return to the beginning. If you choose to clear the cache, the sample will download the content again from Cloud Content Delivery. 

## Setup


### Requirements

To replicate this use case, you need the following [Unity packages](https://docs.unity3d.com/Manual/Packages.html) in your project:

| **Package**                                                                                                        | **Role**                                                                                                                                                                                                                                                         |
| ------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest)                                    | Allows developers to retrieve an asset by using its address. In this sample, the service looks up event-specific images and prefabs based on the information received from Remote Config.                                                                        |
| [Authentication](https://docs.unity.com/authentication/Content/InstallAndConfigureSDK.htm)                         | Automatically signs in the user anonymously to keep track of their data server-side.                                                                                                                                                                             |
| [Remote Config](https://docs.unity3d.com/Packages/com.unity.remote-config@latest/ConfiguringYourProject.html)      | Provides key-value pairs where the value that is mapped to a given key can change on the server side, either manually or based on specific Game Overrides. In this sample, we use it to inform the client of new content updates.                                |

To use these services in your game, activate each service for your Organization and project in the [Unity Dashboard](https://dashboard.unity3d.com/).

**Note**: This sample uses [Cloud Content Delivery (CCD)](https://docs.unity.com/ccd/UnityCCD.html) to host the downloaded new content. There's no need to install the CCD SDK in order to recreate this sample, as we're only using it for hosting the content asset bundle, which gets downloaded using the Addressables SDK.


### Dashboard setup

To replicate this sample scene's setup on your own dashboard, you need to:
- Upload an Addressables build (catalog and asset bundle) to a Cloud Content Delivery bucket.
- Configure values for the Remote Config service.


#### Cloud Content Delivery

You can use Addressables to build asset bundles (and optionally content catalogs), which can then be uploaded to CCD and downloaded by your game. This process isn't detailed in this document, but you can learn about it in [the CCD + Addressables walkthrough tutorial](https://docs.unity.com/ccd/UnityCCDWalkthrough.html).

It's very common to use Addressables and CCD from a single project. However, this example demonstrates a multi-project setup. Developers with medium or large teams might use the latter approach for a number of reasons:

* Reduce iteration time by making the main project smaller.
* Segment different types of work across multiple teams using smaller projects.
* Enable user-generated content.
 
This example uses a multi-project setup. One Unity project is the main project that downloads new content at runtime, while a separate Unity project builds new content into asset bundles for the main project to download. The main project only knows about new content built with the second project when the Remote Config service informs it.

**Note**: To see how this sample's second project is configured, look at the `ota-content` branch of the Use Cases github repository.

To make the multiple-project setup work, you'll need to upload a remote catalog along with your asset bundles.

In your content build project:

* Select the `AddressableAssetSettings` asset to view it in the Inspector window.
* Make sure the `Build Remote Catalog` option is enabled.
* Perform your Addressables build.

In the resulting build folder, there will be a catalog JSON file and a catalog hash file. Upload both files to your CCD bucket along with the related asset bundles.

To learn more about using Addressables with multiple projects,
visit the [Loading from Multiple Projects](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/MultiProject.html) page in the Addressables documentation.

After you've uploaded an Addressables build to a CCD bucket, make notes of a few things that you'll use in the Remote Config settings:

* The "Addressable Remote Path URL" for the bucket (found in the bucket page of the dashboard).
* The name of the content catalog JSON file.
* The addresses of any assets you want to download.


#### Remote Config

[Set up the following config values](https://docs.unity.com/remote-config/HowDoesRemoteConfigWork.html) in the **LiveOps** dashboard:

| **Value**             | **Type** | **Description**                                                                                      | **Default value**                                                                                                                                                                                                                                                                                                                                            |
|---------------------- | -------- |----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `OTA_CATALOG_URL`     | string   | The URL for the Addressables catalog file in the CCD bucket.                                         | The bucket URL provided to you by the CCD dashboard, concatenated with the catalog json filename at the end. Example: `Something like: https://[YOUR_PROJECT_ID].client-api.unity3dusercontent.com/client_api/v1/environments/[YOUR_ENV_NAME]/buckets/[YOUR_BUCKET_ID]/release_by_badge/latest/entry_by_path/content/?path=catalog_YYYY.MM.DD.HH.MM.SS.json` |
| `OTA_CONTENT_UPDATES` | JSON     | Indicates that there is new content to download. Each entry represents a new value in Remote Config. | `{"updates": [{"configKey": "OTA_NEW_CONTENT"}]}`                                                                                                                                                                                                                                                                                                            |
| `OTA_NEW_CONTENT`     | JSON     | Information about the new content. In this case it's simple, but you could have lots of info here.   | `{"prefabAddress":"NewContentPrefab"}`                                                                                                                                                                                                                                                                                                                       |
