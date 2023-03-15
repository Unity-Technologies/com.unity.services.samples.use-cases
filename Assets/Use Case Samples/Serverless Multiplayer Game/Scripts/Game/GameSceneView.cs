
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class GameSceneView : SceneViewBase
    {
        [field: SerializeField]
        public ArenaUIOverlayPanelView arenaUiOverlayPanelView { get; private set; }

        public void ShowArenaPanel()
        {
            ShowPanel(arenaUiOverlayPanelView);
        }

        public void UpdateScores()
        {
            arenaUiOverlayPanelView.UpdateScores();
        }
    }
}
