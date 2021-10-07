// This file is an inactive copy of what is published on the Cloud Code server for this sample, so changes made to
// this file will not have any effect locally. Changes to Cloud Code scripts are normally done directly in the 
// Unity Dashboard.

const { DataApi } = require("@unity-services/cloud-save-1.0");

module.exports = async ({ params, context, logger }) =>
{
    const { projectId, playerId, accessToken } = context;

    const cloudSave = new DataApi({ accessToken });

    const result = { claimed: false };

    const setResult = await cloudSave.setItem(projectId, playerId, { key: "STARTER_PACK_STATUS", value: result });

    return result;
};
