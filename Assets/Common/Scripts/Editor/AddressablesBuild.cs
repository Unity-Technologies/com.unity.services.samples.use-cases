using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public class AddressablesBuild
{
    static public void PreExport()
    {
        Debug.Log("AddressablesBuild.PreExport begin.");
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent();
        Debug.Log("AddressablesBuild.PreExport end.");
    }

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        BuildPlayerWindow.RegisterBuildPlayerHandler(BuildPlayerHandler);
    }

    static void BuildPlayerHandler(BuildPlayerOptions options)
    {
        PreExport();
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }
}
