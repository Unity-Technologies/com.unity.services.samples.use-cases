using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class HostSetupPanelView : PanelViewBase
    {
        [SerializeField]
        Toggle maxPlayers2Toggle;

        [SerializeField]
        Toggle maxPlayers3Toggle;

        [SerializeField]
        Toggle maxPlayers4Toggle;

        [SerializeField]
        Toggle publicToggle;

        [SerializeField]
        Toggle privateToggle;

        [SerializeField]
        TextMeshProUGUI lobbyNameText;

        public void SetMaxPlayers(int maxPlayers)
        {
            maxPlayers2Toggle.SetIsOnWithoutNotify(maxPlayers == 2);

            maxPlayers3Toggle.SetIsOnWithoutNotify(maxPlayers == 3);

            maxPlayers4Toggle.SetIsOnWithoutNotify(maxPlayers == 4);
        }

        public void SetPrivateGameFlag(bool privateGameFlag)
        {
            privateToggle.SetIsOnWithoutNotify(privateGameFlag);

            publicToggle.SetIsOnWithoutNotify(!privateGameFlag);
        }

        public void SetGameName(string gameName)
        {
            lobbyNameText.text = gameName;
        }
    }
}
