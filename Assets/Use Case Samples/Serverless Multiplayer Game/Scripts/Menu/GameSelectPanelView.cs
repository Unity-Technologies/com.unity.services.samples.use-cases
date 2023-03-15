using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.Services.Samples.ServerlessMultiplayerGame
{
    public class GameSelectPanelView : PanelViewBase
    {
        [SerializeField]
        MenuSceneManager menuSceneManager;

        [SerializeField]
        GameListItemView gameListItemViewPrefab;

        [SerializeField]
        Transform gamesListContainer;

        [SerializeField]
        Button joinButton;

        List<Lobby> m_LobbiesList;

        List<GameListItemView> m_GameListItems = new List<GameListItemView>();

        // Index of currently-highlighted lobby game so it can be deselected, if necessary.
        int m_LobbyIndexSelected = -1;

        enum MaxPlayersFilter
        {
            Any,
            Max2,
            Max3,
            Max4,
        }
        MaxPlayersFilter m_MaxPlayersFilter;

        enum HideFullFilter
        {
            Any,
            Yes,
        }
        HideFullFilter m_HideFullFilter;

        public void SetLobbies(List<Lobby> lobbiesList)
        {
            m_LobbiesList = lobbiesList;

            RefreshLobbies();
        }

        public void UpdateLobbies(List<Lobby> lobbiesList)
        {
            if (!DidLobbiesChange(lobbiesList))
            {
                return;
            }

            LobbyManager.Log("Updating Lobbies list for changes", lobbiesList);

            string oldSelectedLobbyId = null;
            if (m_LobbyIndexSelected >= 0 && m_LobbyIndexSelected < m_LobbiesList.Count)
            {
                oldSelectedLobbyId = m_LobbiesList[m_LobbyIndexSelected].Id;
            }

            m_LobbyIndexSelected = -1;
            SetLobbies(lobbiesList);

            if (oldSelectedLobbyId != null)
            {
                for (int lobbyIndex = 0; lobbyIndex < m_LobbiesList.Count; lobbyIndex++)
                {
                    if (m_LobbiesList[lobbyIndex].Id == oldSelectedLobbyId)
                    {
                        m_LobbyIndexSelected = lobbyIndex;
                        break;
                    }
                }
            }

            this.menuSceneManager.UpdateLobbySelectionIndex(m_LobbyIndexSelected);
        }

        public void SetLobbyIndexSelected(int lobbyIndex)
        {
            ClearLobbySelection();

            m_LobbyIndexSelected = lobbyIndex;

            if (m_LobbyIndexSelected >= 0 && m_LobbyIndexSelected < m_GameListItems.Count)
            {
                m_GameListItems[m_LobbyIndexSelected].SetSelected(true);
            }

            UpdateJoinButtonInteractability();
        }

        public void ClearLobbySelection()
        {
            if (m_LobbyIndexSelected >= 0 && m_LobbyIndexSelected < m_GameListItems.Count)
            {
                m_GameListItems[m_LobbyIndexSelected].SetSelected(false);
            }

            m_LobbyIndexSelected = -1;

            joinButton.interactable = false;
        }

        public void OnGameSelectMaxPlayersChanged(int dropdownSelectionIndex)
        {
            m_MaxPlayersFilter = (MaxPlayersFilter)dropdownSelectionIndex;
            Debug.Log($"Filter max players: {m_MaxPlayersFilter}");

            RefreshLobbies();
        }

        public void OnGameSelectHideFullChanged(int dropdownSelectionIndex)
        {
            m_HideFullFilter = (HideFullFilter)dropdownSelectionIndex;
            Debug.Log($"Filter full: {m_HideFullFilter}");

            RefreshLobbies();
        }

        void RefreshLobbies()
        {
            RemoveAllLobbies();

            joinButton.interactable = false;

            int lobbyIndex = 0;
            foreach (var lobby in m_LobbiesList)
            {
                if (IsFilteredIn(lobby))
                {
                    AddLobby(lobby, lobbyIndex);
                }
                else if (lobbyIndex == m_LobbyIndexSelected)
                {
                    m_LobbyIndexSelected = -1;

                    menuSceneManager.ClearLobbySelection();
                }

                lobbyIndex++;
            }

            UpdateJoinButtonInteractability();
        }

        bool IsFilteredIn(Lobby lobby)
        {
            return IsLobbyPlayersFilteredIn(lobby)
                && IsLobbyFullGamesFilteredIn(lobby)
                && IsLobbyNameFilteredIn(lobby);
        }

        bool IsLobbyPlayersFilteredIn(Lobby lobby)
        {
            switch (m_MaxPlayersFilter)
            {
                case MaxPlayersFilter.Max2:
                    if (lobby.MaxPlayers != 2)
                    {
                        return false;
                    }
                    break;
                case MaxPlayersFilter.Max3:
                    if (lobby.MaxPlayers != 3)
                    {
                        return false;
                    }
                    break;
                case MaxPlayersFilter.Max4:
                    if (lobby.MaxPlayers != 4)
                    {
                        return false;
                    }
                    break;
            }

            return true;
        }

        bool IsLobbyFullGamesFilteredIn(Lobby lobby)
        {
            if (m_HideFullFilter == HideFullFilter.Yes && lobby.Players.Count >= lobby.MaxPlayers)
            {
                return false;
            }

            return true;
        }

        bool IsLobbyNameFilteredIn(Lobby lobby)
        {
            return ProfanityManager.IsValidLobbyName(lobby.Name);
        }

        void UpdateJoinButtonInteractability()
        {
            joinButton.interactable = m_LobbyIndexSelected >= 0 &&
                m_LobbyIndexSelected < m_GameListItems.Count;
        }

        void AddLobby(Lobby lobby, int lobbyIndex)
        {
            var gameListItem = GameObject.Instantiate(gameListItemViewPrefab, gamesListContainer);

            gameListItem.SetData(menuSceneManager, lobby, lobbyIndex, lobbyIndex == m_LobbyIndexSelected);

            m_GameListItems.Add(gameListItem);
        }

        void RemoveAllLobbies()
        {
            for (var i = gamesListContainer.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(gamesListContainer.GetChild(i).gameObject);
            }

            m_GameListItems.Clear();
        }

        bool DidLobbiesChange(List<Lobby> lobbiesList)
        {
            if (lobbiesList.Count != m_LobbiesList.Count)
            {
                return true;
            }

            for (int i = 0; i < lobbiesList.Count; i++)
            {
                if (lobbiesList[i].LobbyCode != m_LobbiesList[i].LobbyCode)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
