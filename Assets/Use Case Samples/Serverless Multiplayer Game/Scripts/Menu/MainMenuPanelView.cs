using TMPro;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class MainMenuPanelView : PanelViewBase
    {
        [SerializeField]
        TextMeshProUGUI playerName;

        public void SetLocalPlayerName(string localPlayerName)
        {
            playerName.SetText(localPlayerName);
        }
    }
}
