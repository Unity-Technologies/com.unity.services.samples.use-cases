const { DataApi } = require("@unity-services/cloud-save-1.0");

module.exports = async ({ params, context, logger }) =>
{
    const { projectId, playerId, accessToken } = context;

    const cloudSave = new DataApi({ accessToken });

    const result = { claimed: false };

    const setResult = await cloudSave.setItem(projectId, playerId, { key: "STARTER_PACK_STATUS", value: result });

    return result;
};
