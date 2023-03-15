using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.CloudAIMiniGame
{
    public class CloudAIMiniGameSampleView : MonoBehaviour
    {
        const int k_PlayfieldSize = CloudAIMiniGameSceneManager.k_PlayfieldSize;

        public RewardPopupView rewardPopup;
        public MessagePopup messagePopup;
        public Button newGameButton;
        public TextMeshProUGUI newGameButtonText;
        public Button resetGameButton;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI winCountText;
        public TextMeshProUGUI lossCountText;
        public TextMeshProUGUI tieCountText;
        public TextMeshProUGUI winPercentText;
        public TextMeshProUGUI lossPercentText;
        public TextMeshProUGUI tiePercentText;

        GridEntityView[,] m_GridEntityView = new GridEntityView[k_PlayfieldSize, k_PlayfieldSize];

        public void SetInteractable(bool isInteractable = true)
        {
            newGameButton.interactable = isInteractable;
            resetGameButton.interactable = isInteractable;

            foreach (var gridEntityView in m_GridEntityView)
            {
                gridEntityView.SetInteractable(isInteractable);
            }
        }

        public void ShowState(UpdatedState updatedState)
        {
            if (updatedState.isNewGame)
            {
                ShowNewGamePopup(updatedState.aiPieces.Count == 0);
            }

            ClearGridContents();

            ShowPieces(updatedState.aiPieces, GridEntityView.GridContents.AiPiece);
            ShowPieces(updatedState.playerPieces, GridEntityView.GridContents.PlayerPiece);

            ShowStateText(updatedState);

            ShowWinLossTieCounts(updatedState);

            SetNewGameButtonText(updatedState.isGameOver);
        }

        public void ShowAiTurn()
        {
            statusText.text = "Playing\nCloud AI Turn.";
        }

        void ShowNewGamePopup(bool isPlayerFirst)
        {
            const string playerOneMessage = "A new game was started.\n\nCongratulations, you're\nplayer number one.";
            const string playerTwoMessage = "A new game was started.\nCloud AI played first.\n\nNow it's your turn.";

            messagePopup.Show("New Game", isPlayerFirst ? playerOneMessage : playerTwoMessage);
        }

        public void ShowSpaceOccupiedErrorPopup()
        {
            messagePopup.Show("Unable to place piece",
                "Space is occupied.\n\nPlease ensure target space is empty.");
        }

        public void ShowGameOverErrorPopup()
        {
            messagePopup.Show("Game Over",
                "Please select\n[New Game]\nto begin a new game.");
        }

        public void ShowGameOverPopup(string status)
        {
            switch (status)
            {
                case "playerWon":
                    ShowRewardPopup("Congratulations!", "You received 100 Coins for winning!", 100);
                    break;

                case "aiWon":
                    messagePopup.Show("Game Over", "Sorry, you lost the game.");
                    break;

                case "draw":
                    ShowRewardPopup("Congratulations!", "You received 25 Coins for tying!", 25);
                    break;
            }
        }

        void ShowRewardPopup(string title, string subTitle, int coinCount)
        {
            rewardPopup.headerText.text = $"<size=45>{title}</size>\n" +
                "<size=10>\n</size>" +
                $"<size=20>{subTitle}</size>";

            rewardPopup.closeButtonText.text = "Yay!";

            var rewards = new List<RewardDetail>();
            rewards.Add(new RewardDetail
            {
                id = "COIN",
                spriteAddress = "Sprites/Currency/Coin",
                quantity = coinCount
            });

            rewardPopup.Show(rewards);
        }

        public void CloseRewardPopup()
        {
            rewardPopup.Close();
        }

        void ClearGridContents()
        {
            Coord coord;
            for (coord.x = 0; coord.x < k_PlayfieldSize; coord.x++)
            {
                for (coord.y = 0; coord.y < k_PlayfieldSize; coord.y++)
                {
                    SetGridEntityState(coord, GridEntityView.GridContents.Empty);
                }
            }
        }

        void ShowPieces(List<Coord> pieces, GridEntityView.GridContents contents)
        {
            foreach (var piece in pieces)
            {
                SetGridEntityState(piece, contents);
            }
        }

        void ShowStateText(UpdatedState updatedState)
        {
            if (updatedState.isPlayerTurn)
            {
                if (updatedState.isNewGame)
                {
                    statusText.text = "New Game\nYour Turn.";
                }
                else
                {
                    statusText.text = "Playing\nYour Turn.";
                }
            }
            else if (updatedState.isGameOver)
            {
                switch (updatedState.status)
                {
                    case "playerWon":
                        statusText.text = "Game Over,\nYou Won!";
                        break;
                    case "aiWon":
                        statusText.text = "Game Over,\nCloud AI Won.";
                        break;
                    case "draw":
                        statusText.text = "Game Over,\nTie!";
                        break;
                    default:
                        statusText.text = "Game Over";
                        break;
                }
            }
        }

        void ShowWinLossTieCounts(UpdatedState updatedState)
        {
            float totalCount = updatedState.winCount + updatedState.lossCount + updatedState.tieCount;
            winCountText.text = $"{updatedState.winCount}";
            lossCountText.text = $"{updatedState.lossCount}";
            tieCountText.text = $"{updatedState.tieCount}";
            if (totalCount > 0)
            {
                winPercentText.text = $"{updatedState.winCount / totalCount * 100:0}%";
                lossPercentText.text = $"{updatedState.lossCount / totalCount * 100:0}%";
                tiePercentText.text = $"{updatedState.tieCount / totalCount * 100:0}%";
            }
            else
            {
                winPercentText.text = "0%";
                lossPercentText.text = "0%";
                tiePercentText.text = "0%";
            }
        }

        void SetGridEntityState(Coord coord, GridEntityView.GridContents buttonState)
        {
            var gridEntityView = m_GridEntityView[coord.x, coord.y];
            gridEntityView.SetGridContents(buttonState);
        }

        public void ShowInProgress(Coord coord, bool flag)
        {
            var gridEntityView = m_GridEntityView[coord.x, coord.y];
            gridEntityView.ShowInProgress(flag);
        }

        void SetNewGameButtonText(bool isGameOver)
        {
            newGameButtonText.text = isGameOver ? "New Game" : "Forfeit";
        }

        public void RegisterGridEntityView(GridEntityView gridEntity)
        {
            m_GridEntityView[gridEntity.coord.x, gridEntity.coord.y] = gridEntity;
        }
    }
}
