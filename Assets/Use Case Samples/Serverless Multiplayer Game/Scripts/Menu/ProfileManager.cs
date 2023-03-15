using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    // Important implentation note: The following class is primarily used to facilitate testing by defaulting each Unity
    // instance to the same profile index last used. This makes is less likely that 2 instances will share the same profile,
    // thus preventing this sample from functioning correctly.
    public static class ProfileManager
    {
        // Prefix for key which includes a Unity project path to find index of last profile index used.
        const string k_LatestProfileForPathPrefix = "ProfilePath_";

        // This is used to strip out non-alpha-numeric characters from path name to make a cleaner Player Prefs key.
        static readonly Regex k_ReplacePathCharacters = new Regex("[^A-Za-z0-9]");

        public static int LookupPreviousProfileIndex()
        {
            var key = GetProfileIndexForPathKey();

            // If we don't have a previous profile index used then just default to 0 until user changes it.
            var profileIndex = 0;

            if (PlayerPrefs.HasKey(key))
            {
                profileIndex = PlayerPrefs.GetInt(key);
            }
            else
            {
                SaveLatestProfileIndexForProjectPath(profileIndex);
            }

            return profileIndex;
        }

        public static void SaveLatestProfileIndexForProjectPath(int profileIndex)
        {
            var key = GetProfileIndexForPathKey();

            PlayerPrefs.SetInt(key, profileIndex);
            PlayerPrefs.Save();
        }

        static string GetProfileIndexForPathKey()
        {
            return k_LatestProfileForPathPrefix +
                k_ReplacePathCharacters.Replace(Application.dataPath, "-");
        }
    }
}
