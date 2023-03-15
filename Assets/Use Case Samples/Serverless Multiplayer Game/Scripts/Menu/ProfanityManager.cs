using System;
using System.Collections.Generic;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    // This class handles preventing profanities using a 'white-list' approach rather than checking for explicitly black-listed words.
    // Please don't let the class name mislead you; it's intended to be easily-replacable with any approach you might prefer to prevent
    // profanities in your game. To modify, simply replace the public methods with your prefered method of preventing profanities and
    // the Profanity Manager can be expanded to only prevent explicit profanities instead of only permitting white-listed words.
    public static class ProfanityManager
    {
        const int k_MinWordsInLobbyName = 2;
        const int k_MaxWordsInLobbyName = 4;

        const int k_MaxAverageWordLength = 9;
        const int k_MaxSpaces = k_MaxWordsInLobbyName - 1;
        const int k_MaxLobbyNameLength = k_MaxWordsInLobbyName * k_MaxAverageWordLength + k_MaxSpaces;

        static HashSet<string> s_ValidPlayerNames;
        static HashSet<string> s_ValidLobbyNameWords;

        // Initialize the Profanity Manager. Please see comment at top of file for details on our design choices for this class.
        // This method simply adds all 'white-listed' words to an internal HashSets so we can quickly validate
        // that player names and lobby names are permitted.
        public static void Initialize()
        {
            s_ValidPlayerNames = new HashSet<string>(PlayerNameManager.namesPool);

            s_ValidLobbyNameWords = new HashSet<string>();
            var lobbyNamesPool = LobbyNameManager.namesPool;
            foreach (var lobbyName in lobbyNamesPool)
            {
                AddUniqueLobbyWords(lobbyName);
            }
        }

        // Verify that requested player name has been white listed. Please see comment at top of file for details on our design choices for this class.
        public static bool IsValidPlayerName(string playerName)
        {
            return s_ValidPlayerNames.Contains(playerName);
        }

        // Ensure specified player name is acceptable and modify it, if necessary.
        // Please see comment at top of file for details on our design choices for this class.
        public static string SanitizePlayerName(string playerName)
        {
            if (IsValidPlayerName(playerName))
            {
                return playerName;
            }

            if (playerName.Length > 0)
            {
                return $"{playerName[0]}****";
            }

            return "*****";
        }

        // Check if suggested lobby name is valid by ensuring all words have been white-listed and general structure seems valid.
        // Please see comment at top of file for details on our design choices for this class.
        public static bool IsValidLobbyName(string lobbyName)
        {
            if (IsLobbyNameTooLong(lobbyName))
            {
                return false;
            }

            var simplifiedName = lobbyName.Replace("'s", "");
            var lobbyNameWords = simplifiedName.Split(new char[] {' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (!HasCorrectNumberOfWords(lobbyNameWords))
            {
                return false;
            }

            if (!HasOnlyWhiteListedWords(lobbyNameWords))
            {
                return false;
            }

            if (!HasExactlyOnePlayerName(lobbyNameWords))
            {
                return false;
            }

            return true;
        }

        static void AddUniqueLobbyWords(string lobbyName)
        {
            var simplifiedName = lobbyName.Replace("'s", "").Replace("{0}", "");
            var splitName = simplifiedName.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            s_ValidLobbyNameWords.UnionWith(splitName);
        }

        static bool IsLobbyNameTooLong(string lobbyName)
        {
            if (lobbyName.Length > k_MaxLobbyNameLength)
            {
                return true;
            }

            return false;
        }

        static bool HasCorrectNumberOfWords(string[] lobbyNameWords)
        {
            // Ensure the lobby name has a valid number of words.
            if (lobbyNameWords.Length >= k_MinWordsInLobbyName && lobbyNameWords.Length <= k_MaxWordsInLobbyName)
            {
                return true;
            }

            return false;
        }

        static bool HasOnlyWhiteListedWords(string[] lobbyNameWords)
        {
            // Check that each word in the lobby name is either a player name or a valid lobby word.
            foreach (var word in lobbyNameWords)
            {
                // If the word is not in player names list and not in lobby name words list then it's invalid
                if (!s_ValidPlayerNames.Contains(word) && !s_ValidLobbyNameWords.Contains(word))
                {
                    return false;
                }
            }

            return true;
        }

        static bool HasExactlyOnePlayerName(string[] lobbyNameWords)
        {
            var wasPlayerNameFound = false;

            foreach (var word in lobbyNameWords)
            {
                if (IsValidPlayerName(word))
                {
                    // Lobby should only contain 1 player name, else it's invalid.
                    if (wasPlayerNameFound)
                    {
                        return false;
                    }

                    wasPlayerNameFound = true;
                }
            }

            return wasPlayerNameFound;
        }
    }
}
