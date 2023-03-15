namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public static class LobbyNameManager
    {
        static readonly string[] k_NamesPool =
        {
            "{0}'s Amazing Game",
            "The {0} Lobby",
            "The {0} Experience",
        };

        public static string[] namesPool => k_NamesPool;

        public static string GenerateRandomName(string playerName)
        {
            var i = UnityEngine.Random.Range(0, k_NamesPool.Length);

            var template = k_NamesPool[i];

            return template.Replace("{0}", playerName);
        }
    }
}
