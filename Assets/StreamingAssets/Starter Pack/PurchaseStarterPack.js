const { PurchasesApi } = require("@unity-services/economy-2.0");
const { DataApi } = require("@unity-services/cloud-save-1.0");

module.exports = async ({ params, context, logger }) =>
{
    const { projectId, playerId, accessToken } = context;
    const purchasesApi = new PurchasesApi({ accessToken });
    const cloudSaveApi = new DataApi({ accessToken });

    // Prevent the purchase if Cloud Save confirms the player already claimed a Starter Pack.

    const getItemsResponse = await cloudSaveApi.getItems(projectId, playerId, [ "STARTER_PACK_STATUS" ]);

    if (getItemsResponse.data.results &&
        getItemsResponse.data.results.length > 0 &&
        getItemsResponse.data.results[0] &&
        getItemsResponse.data.results[0].value)
    {
        if (getItemsResponse.data.results[0].value.claimed &&
            getItemsResponse.data.results[0].value.claimed === true)
        {
            logger.error("The Starter Pack has already been claimed by this player.");
            throw Error("The Starter Pack has already been claimed by this player.");
        }
    }

    // Call Economy to make the purchase. An error will be thrown if the player can't afford it.

    const purchaseResult = await purchasesApi.makeVirtualPurchase(projectId, playerId, { id: "STARTER_PACK" });

    result = purchaseResult.data;

    // Let Cloud Save know that the Starter Pack has been claimed by this player.

    await cloudSaveApi.setItem(projectId, playerId, { key: "STARTER_PACK_STATUS", value: { claimed: true } });

    return result;
};
