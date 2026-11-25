using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Main menu and pre-game lobby: play on one computer (2-4 players), host an online game (2-4 players,
    /// friends join with a code), or join a friend's game. Each player picks a different faction, then the
    /// host starts the match.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private GameObject _mainMenuRoot;
        private GameObject _menuView;
        private GameObject _lobbyView;
        private InputField _playerNameInput;
        private InputField _menuJoinCodeInput;
        private Text _menuStatusText;
        private Text _lobbyTitleText;
        private Text _lobbyCodeText;
        private Text _lobbyStartHintText;
        private Transform _lobbySeatsRoot;
        private Button _lobbyStartButton;

        private void BuildMainMenu(Transform canvasParent)
        {
            var root = CreatePanel(canvasParent, "MainMenu",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero,
                new Color(0.06f, 0.08f, 0.11f, 0.97f));
            _mainMenuRoot = root.gameObject;

            CreateText(root, "Title", "CRITTERS OF CALAMITY", 44, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.84f), new Vector2(0.9f, 0.95f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            CreateText(root, "Version", $"Version {Application.version}", 13, TextAnchor.LowerRight,
                new Vector2(0.75f, 0.005f), new Vector2(0.995f, 0.04f), Vector2.zero, Vector2.zero, Palette.UITextMuted);

            _menuStatusText = CreateText(root, "Status", "", 16, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.09f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            // ----- MENU VIEW -----
            var menu = CreatePanel(root, "MenuView", new Vector2(0.30f, 0.12f), new Vector2(0.70f, 0.82f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.13f, 0.18f, 0.95f));
            _menuView = menu.gameObject;

            CreateText(menu, "NameLabel", "Your name", 16, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.88f), new Vector2(0.40f, 0.95f), Vector2.zero, Vector2.zero, Color.white);
            _playerNameInput = CreateInputField(menu, "NameInput", "Player", "Type your name",
                new Vector2(0.40f, 0.88f), new Vector2(0.94f, 0.95f));
            _playerNameInput.characterLimit = 20;

            CreateText(menu, "LocalLabel", "PLAY ON THIS COMPUTER (take turns at one screen)", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.81f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            for (int n = 2; n <= 4; n++)
            {
                int count = n;
                float x = 0.06f + (n - 2) * 0.30f;
                CreateButton(menu, $"BtnLocal{n}", $"{n} Players", new Vector2(x, 0.64f), new Vector2(x + 0.28f, 0.73f),
                    Palette.FromHex("#3A4B63"), () =>
                    {
                        _mpManager.SetLocalName(_playerNameInput.text);
                        _mpManager.StartLocalLobby(count);
                    });
            }

            CreateText(menu, "HostLabel", "HOST AN ONLINE GAME (friends join with a code)", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.57f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            for (int n = 2; n <= 4; n++)
            {
                int count = n;
                float x = 0.06f + (n - 2) * 0.30f;
                CreateButton(menu, $"BtnHost{n}", $"Host for {n}", new Vector2(x, 0.40f), new Vector2(x + 0.28f, 0.49f),
                    Palette.FromHex("#2E7D5B"), async () =>
                    {
                        _mpManager.SetLocalName(_playerNameInput.text);
                        await _mpManager.HostOnlineAsync(count);
                    });
            }

            CreateText(menu, "JoinLabel", "JOIN A FRIEND'S GAME", 15, TextAnchor.MiddleLeft,
                new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.33f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _menuJoinCodeInput = CreateInputField(menu, "JoinCodeInput", "", "Join code",
                new Vector2(0.06f, 0.15f), new Vector2(0.62f, 0.24f));
            _menuJoinCodeInput.characterLimit = 12;
            CreateButton(menu, "BtnJoin", "Join", new Vector2(0.66f, 0.15f), new Vector2(0.94f, 0.24f),
                Palette.FromHex("#3A6B8C"), async () =>
                {
                    _mpManager.SetLocalName(_playerNameInput.text);
                    await _mpManager.JoinOnlineAsync(_menuJoinCodeInput.text);
                });

            CreateButton(menu, "BtnQuit", "Quit Game", new Vector2(0.36f, 0.03f), new Vector2(0.64f, 0.10f),
                Palette.FromHex("#5A2A2A"), () => Application.Quit());

            // ----- LOBBY VIEW -----
            var lobby = CreatePanel(root, "LobbyView", new Vector2(0.20f, 0.12f), new Vector2(0.80f, 0.82f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.13f, 0.18f, 0.95f));
            _lobbyView = lobby.gameObject;

            _lobbyTitleText = CreateText(lobby, "LobbyTitle", "GAME LOBBY", 24, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.89f), new Vector2(0.60f, 0.97f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _lobbyCodeText = CreateText(lobby, "LobbyCode", "", 22, TextAnchor.MiddleRight,
                new Vector2(0.50f, 0.89f), new Vector2(0.96f, 0.97f), Vector2.zero, Vector2.zero, Color.white);

            var seatsGo = new GameObject("Seats");
            seatsGo.transform.SetParent(lobby, false);
            var seatsRt = seatsGo.AddComponent<RectTransform>();
            seatsRt.anchorMin = new Vector2(0.04f, 0.20f);
            seatsRt.anchorMax = new Vector2(0.96f, 0.87f);
            seatsRt.offsetMin = Vector2.zero;
            seatsRt.offsetMax = Vector2.zero;
            var seatsLayout = seatsGo.AddComponent<VerticalLayoutGroup>();
            seatsLayout.spacing = 8f;
            seatsLayout.childControlHeight = true;
            seatsLayout.childControlWidth = true;
            seatsLayout.childForceExpandHeight = false;
            seatsLayout.childForceExpandWidth = true;
            _lobbySeatsRoot = seatsGo.transform;

            _lobbyStartHintText = CreateText(lobby, "StartHint", "", 15, TextAnchor.MiddleLeft,
                new Vector2(0.04f, 0.04f), new Vector2(0.50f, 0.16f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            _lobbyStartButton = CreateButton(lobby, "BtnStart", "Start Game", new Vector2(0.52f, 0.04f), new Vector2(0.76f, 0.16f),
                Palette.FromHex("#B86B28"), () => _mpManager.StartMatch());
            CreateButton(lobby, "BtnLeaveLobby", "Back", new Vector2(0.78f, 0.04f), new Vector2(0.96f, 0.16f),
                Palette.FromHex("#7A2828"), async () => await _mpManager.LeaveAsync());

            _lobbyView.SetActive(false);
        }

        private void ShowMainMenu()
        {
            if (_mainMenuRoot == null) return;
            _mainMenuRoot.SetActive(true);
            _mainMenuRoot.transform.SetAsLastSibling();
            RefreshMainMenu();
        }

        private void HideMainMenu()
        {
            if (_mainMenuRoot != null) _mainMenuRoot.SetActive(false);
        }

        private void RefreshMainMenu()
        {
            if (_mainMenuRoot == null || _mpManager == null) return;
            if (_mpManager.InMatch)
            {
                _mainMenuRoot.SetActive(false);
                return;
            }

            bool inLobby = _mpManager.Mode != SessionMode.Menu;
            _menuView.SetActive(!inLobby);
            _lobbyView.SetActive(inLobby);
            _menuStatusText.text = _mpManager.IsBusy ? _mpManager.StatusMessage + " ..." : _mpManager.StatusMessage;
            if (!inLobby) return;

            var lobby = _mpManager.Lobby;
            _lobbyTitleText.text = _mpManager.Mode == SessionMode.Local
                ? $"LOCAL GAME ({lobby.MaxSeats} PLAYERS)"
                : $"ONLINE GAME ({lobby.Seats.Count}/{lobby.MaxSeats} JOINED)";
            _lobbyCodeText.text = _mpManager.IsOnline && !string.IsNullOrEmpty(lobby.Code)
                ? $"Join code: <b>{lobby.Code}</b>"
                : "";

            foreach (Transform child in _lobbySeatsRoot) Destroy(child.gameObject);

            for (int seatIdx = 0; seatIdx < lobby.MaxSeats; seatIdx++)
            {
                var seat = lobby.Seats.FirstOrDefault(s => s.Seat == seatIdx);
                BuildSeatRow(seatIdx, seat);
            }

            bool canStart = _mpManager.CanStartMatch(out var reason);
            _lobbyStartButton.gameObject.SetActive(_mpManager.Mode != SessionMode.Client);
            _lobbyStartButton.interactable = canStart;
            _lobbyStartHintText.text = (canStart ? "Everyone's here." : reason) +
                "\nOnce the game starts, everyone rolls dice to decide who picks a faction first.";
        }

        private void BuildSeatRow(int seatIdx, LobbySeat seat)
        {
            var rowGo = new GameObject($"Seat_{seatIdx}");
            rowGo.transform.SetParent(_lobbySeatsRoot, false);
            var rowRt = rowGo.AddComponent<RectTransform>();
            var rowImg = rowGo.AddComponent<Image>();
            var le = rowGo.AddComponent<LayoutElement>();
            le.preferredHeight = 70f;

            if (seat == null)
            {
                rowImg.color = new Color(0.14f, 0.16f, 0.20f, 0.9f);
                CreateText(rowRt, "Empty", $"Seat {seatIdx + 1}: waiting for a player to join...", 16, TextAnchor.MiddleLeft,
                    new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero, new Color(0.7f, 0.7f, 0.7f));
                return;
            }

            rowImg.color = new Color(0.20f, 0.24f, 0.30f, 0.95f);
            string you = _mpManager.IsOnline && seat.Seat == _mpManager.LocalSeat ? " (you)" : "";
            string offline = seat.Connected ? "" : " [disconnected]";

            if (_mpManager.Mode == SessionMode.Local)
            {
                CreateText(rowRt, "SeatLabel", $"Seat {seatIdx + 1}", 16, TextAnchor.MiddleLeft,
                    new Vector2(0.03f, 0f), new Vector2(0.16f, 1f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
                var nameInput = CreateInputField(rowRt, "SeatName", seat.Name, $"Player {seatIdx + 1}",
                    new Vector2(0.17f, 0.18f), new Vector2(0.60f, 0.82f));
                nameInput.characterLimit = 20;
                int captured = seatIdx;
                nameInput.onEndEdit.AddListener(v => _mpManager.RenameLocalSeat(captured, v));
            }
            else
            {
                CreateText(rowRt, "SeatName", $"Seat {seatIdx + 1}: <b>{seat.Name}</b>{you}{offline}", 18, TextAnchor.MiddleLeft,
                    new Vector2(0.03f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero, Color.white);
            }
        }

        private InputField CreateInputField(Transform parent, string name, string value, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            var input = go.AddComponent<InputField>();
            var text = CreateText(go.transform, "Text", "", 16, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                new Vector2(8f, 2f), new Vector2(-8f, -2f), Color.white);
            text.supportRichText = false;
            var ph = CreateText(go.transform, "Placeholder", placeholder, 16, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one,
                new Vector2(8f, 2f), new Vector2(-8f, -2f), new Color(1f, 1f, 1f, 0.35f));
            ph.fontStyle = FontStyle.Italic;
            input.textComponent = text;
            input.placeholder = ph;
            input.targetGraphic = img;
            input.text = value ?? "";
            return input;
        }
    }
}
