using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Multiplayer;
using UnityEngine;
using CrittersOfCalamity.Core;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;

namespace CrittersOfCalamity.Multiplayer
{
    public enum SessionMode
    {
        Menu,     // main menu, nothing running
        Local,    // everyone at one computer, taking turns
        Host,     // online, this computer runs the room
        Client    // online, joined someone else's room
    }

    [Serializable]
    public class LobbySeat
    {
        public int Seat;
        public string Name;
        public int Faction;        // FactionId as int
        public ulong ClientId;     // network client id (online only)
        public bool Connected = true;
    }

    [Serializable]
    public class LobbySnapshot
    {
        public string Code;
        public int MaxSeats;
        public bool Started;
        public List<LobbySeat> Seats = new List<LobbySeat>();
    }

    [Serializable]
    public class MatchStartInfo
    {
        public int Seed;
        public List<int> Factions = new List<int>();
        public List<string> Names = new List<string>();
    }

    /// <summary>
    /// Runs local and online play for Critters of Calamity.
    ///
    /// Online play is lockstep through Unity Multiplayer Services (Relay + Lobby, joined with a short code):
    /// - Everyone's computer runs the same rules engine, started from the same random seed.
    /// - A player's action is sent to the host as a GameCommand. The host checks it's that player's turn,
    ///   applies it, and sends it to everyone else in order, with a checksum to catch any drift.
    /// - Battle decisions (relic cards, Quick Feet) are commands from the player being asked.
    /// </summary>
    public class CrittersMultiplayerSessionManager : MonoBehaviour
    {
        const string MsgToHost = "COC_C2H";
        const string MsgToClients = "COC_H2C";

        public SessionMode Mode { get; private set; } = SessionMode.Menu;
        public ISession CurrentSession { get; private set; }
        public string ActiveJoinCode { get; private set; } = "";
        public string StatusMessage { get; private set; } = "";
        public bool IsBusy { get; private set; }
        public LobbySnapshot Lobby { get; private set; } = new LobbySnapshot();
        public int LocalSeat { get; private set; } = -1;
        public bool InMatch { get; private set; }
        public bool IsOnline => Mode == SessionMode.Host || Mode == SessionMode.Client;
        public bool OutOfSync { get; private set; }

        public CrittersGameEngine Engine { get; private set; }

        public event Action OnSessionUpdated;
        /// <summary>Raised on every computer when the match starts (seed, factions and names are identical everywhere).</summary>
        public event Action<MatchStartInfo> OnMatchStarted;
        /// <summary>Raised after a command is applied. bool = whether the engine accepted it.</summary>
        public event Action<GameCommand, bool> OnCommandApplied;
        /// <summary>A short message for this player only (not part of the shared game log).</summary>
        public event Action<string> OnLocalNotice;
        /// <summary>Raised when the session ends (left, host closed, disconnected).</summary>
        public event Action<string> OnReturnedToMenu;

        string _localName = "Player";
        int _commandSequence;

        // ------------------------------------------------------------------
        // MENU ACTIONS
        // ------------------------------------------------------------------

        public void SetLocalName(string name)
        {
            _localName = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            if (_localName.Length > 20) _localName = _localName.Substring(0, 20);
        }

        /// <summary>Everyone plays on this computer, passing turns.</summary>
        public void StartLocalLobby(int playerCount)
        {
            playerCount = Mathf.Clamp(playerCount, 2, 4);
            Mode = SessionMode.Local;
            Lobby = new LobbySnapshot { Code = "", MaxSeats = playerCount };
            for (int i = 0; i < playerCount; i++)
                Lobby.Seats.Add(new LobbySeat { Seat = i, Name = $"Player {i + 1}", Faction = FirstFreeFaction(i) });
            LocalSeat = -1;
            StatusMessage = $"Local game for {playerCount} players on this computer.";
            Changed();
        }

        public async Task HostOnlineAsync(int playerCount)
        {
            if (IsBusy) return;
            playerCount = Mathf.Clamp(playerCount, 2, 4);
            IsBusy = true;
            SetStatus("Connecting to Unity's online services...");
            try
            {
                if (!await EnsureSignedInAsync()) return;
                EnsureNetworkManager();
                var nm = NetworkManager.Singleton;

                var options = new SessionOptions
                {
                    Name = $"Critters-{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                    MaxPlayers = playerCount,
                    IsPrivate = true // joinable only with the code
                }.WithRelayNetwork();

                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                ActiveJoinCode = CurrentSession.Code;
                Mode = SessionMode.Host;
                HookSessionEvents();
                RegisterHandlers();

                Lobby = new LobbySnapshot { Code = ActiveJoinCode, MaxSeats = playerCount };
                Lobby.Seats.Add(new LobbySeat { Seat = 0, Name = _localName, Faction = FirstFreeFaction(0), ClientId = nm.LocalClientId });
                LocalSeat = 0;
                SetStatus($"Room open. Share the join code: {ActiveJoinCode}");
                BroadcastLobby();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Multiplayer] Host failed: {ex}");
                SetStatus($"Couldn't open an online room: {ex.Message}");
                await ResetToMenuAsync(null);
            }
            finally
            {
                IsBusy = false;
                Changed();
            }
        }

        public async Task JoinOnlineAsync(string code)
        {
            if (IsBusy) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length < 4) { SetStatus("Type the join code your host gave you."); return; }
            IsBusy = true;
            SetStatus($"Joining room {code}...");
            try
            {
                if (!await EnsureSignedInAsync()) return;
                EnsureNetworkManager();
                var nm = NetworkManager.Singleton;
                nm.OnClientConnectedCallback -= HandleClientConnectedLocal;
                nm.OnClientConnectedCallback += HandleClientConnectedLocal;

                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                ActiveJoinCode = CurrentSession.Code;
                Mode = SessionMode.Client;
                HookSessionEvents();
                RegisterHandlers();
                Lobby = new LobbySnapshot { Code = ActiveJoinCode };
                SetStatus($"Joined room {ActiveJoinCode}. Waiting for the host...");

                // If the connection finished during the await, say hello now.
                if (nm.IsConnectedClient) SendHello();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Multiplayer] Join failed: {ex}");
                SetStatus($"Couldn't join room {code}: {ex.Message}");
                await ResetToMenuAsync(null);
            }
            finally
            {
                IsBusy = false;
                Changed();
            }
        }

        public async Task LeaveAsync()
        {
            await ResetToMenuAsync("You left the game.");
        }

        /// <summary>Change a seat's faction in the lobby. Local: any seat. Online: only your own seat.</summary>
        public void CycleFaction(int seat, int direction)
        {
            if (InMatch || seat < 0 || seat >= Lobby.Seats.Count) return;
            int count = Enum.GetValues(typeof(FactionId)).Length;
            int current = Lobby.Seats[seat].Faction;
            for (int step = 1; step <= count; step++)
            {
                int candidate = ((current + direction * step) % count + count) % count;
                if (!Lobby.Seats.Any(s => s.Seat != seat && s.Faction == candidate))
                {
                    RequestFaction(seat, candidate);
                    return;
                }
            }
        }

        public void RenameLocalSeat(int seat, string name)
        {
            if (Mode != SessionMode.Local || InMatch || seat < 0 || seat >= Lobby.Seats.Count) return;
            Lobby.Seats[seat].Name = string.IsNullOrWhiteSpace(name) ? $"Player {seat + 1}" : name.Trim();
            Changed();
        }

        void RequestFaction(int seat, int faction)
        {
            switch (Mode)
            {
                case SessionMode.Local:
                    Lobby.Seats[seat].Faction = faction;
                    Changed();
                    break;
                case SessionMode.Host:
                    if (seat != LocalSeat) return;
                    TryAssignFaction(seat, faction);
                    break;
                case SessionMode.Client:
                    if (seat != LocalSeat) return;
                    SendToHost("PICK", faction.ToString());
                    break;
            }
        }

        public bool CanStartMatch(out string reason)
        {
            reason = null;
            if (Mode == SessionMode.Client) { reason = "Waiting for the host to start."; return false; }
            if (Mode == SessionMode.Menu) { reason = ""; return false; }
            if (Lobby.Seats.Count < Lobby.MaxSeats) { reason = $"Waiting for players ({Lobby.Seats.Count}/{Lobby.MaxSeats})."; return false; }
            if (Lobby.Seats.Select(s => s.Faction).Distinct().Count() != Lobby.Seats.Count) { reason = "Each player needs a different faction."; return false; }
            return true;
        }

        /// <summary>Host (or local) starts the match. Every computer then builds the same game from the same seed.</summary>
        public void StartMatch()
        {
            if (!CanStartMatch(out var why)) { SetStatus(why); return; }
            var info = new MatchStartInfo { Seed = NewSeed() };
            foreach (var s in Lobby.Seats.OrderBy(x => x.Seat))
            {
                info.Factions.Add(s.Faction);
                info.Names.Add(s.Name);
            }
            Lobby.Started = true;
            if (Mode == SessionMode.Host)
            {
                BroadcastLobby();
                BroadcastToClients("START", JsonUtility.ToJson(info));
            }
            BeginMatch(info);
        }

        // ------------------------------------------------------------------
        // IN-GAME COMMANDS
        // ------------------------------------------------------------------

        /// <summary>The seat whose screen this is: your seat online, or whoever's turn it is (or who's being asked) locally.</summary>
        public int ViewingSeat
        {
            get
            {
                if (IsOnline) return LocalSeat;
                if (Engine == null || Engine.Players.Count == 0) return 0;
                return Engine.CurrentTurnPlayerIndex;
            }
        }

        /// <summary>Send an action. Locally it runs right away; online it goes through the host.</summary>
        public void Submit(GameCommand cmd)
        {
            if (Engine == null || !InMatch) return;
            cmd.Seat = IsOnline ? LocalSeat : GameCommandExecutor.RequiredSeat(Engine, cmd);

            if (!GameCommandExecutor.IsAllowed(Engine, cmd, out var reason))
            {
                OnLocalNotice?.Invoke(reason);
                return;
            }

            switch (Mode)
            {
                case SessionMode.Local:
                    ApplyCommand(cmd, -1);
                    break;
                case SessionMode.Host:
                    HostAcceptCommand(cmd);
                    break;
                case SessionMode.Client:
                    SendToHost("REQ", cmd.Encode());
                    break;
            }
        }

        void HostAcceptCommand(GameCommand cmd)
        {
            if (!GameCommandExecutor.IsAllowed(Engine, cmd, out var reason))
            {
                Debug.Log($"[Multiplayer] Rejected {cmd}: {reason}");
                return;
            }
            ApplyCommand(cmd, -1);
            _commandSequence++;
            int checksum = GameCommandExecutor.Checksum(Engine);
            BroadcastToClients("CMD", $"{_commandSequence};{checksum};{cmd.Encode()}");
        }

        void ApplyCommand(GameCommand cmd, int expectedChecksum)
        {
            bool ok = GameCommandExecutor.Apply(Engine, cmd);
            if (expectedChecksum != -1 && !OutOfSync)
            {
                int mine = GameCommandExecutor.Checksum(Engine);
                if (mine != expectedChecksum)
                {
                    OutOfSync = true;
                    Debug.LogError($"[Multiplayer] Out of sync with the host after {cmd}.");
                    OnLocalNotice?.Invoke("Warning: your game is out of sync with the host's. Please tell the host; the game may need restarting.");
                }
            }
            OnCommandApplied?.Invoke(cmd, ok);
        }

        void BeginMatch(MatchStartInfo info)
        {
            Engine = new CrittersGameEngine(info.Seed);
            Engine.StartNewMatch(info.Factions.Select(f => (FactionId)f).ToList(), info.Names.ToList());
            InMatch = true;
            OutOfSync = false;
            _commandSequence = 0;
            SetStatus(IsOnline ? $"Match started. You are {info.Names[Mathf.Max(0, LocalSeat)]}." : "Local match started.");
            OnMatchStarted?.Invoke(info);
        }

        // ------------------------------------------------------------------
        // NETWORK PLUMBING
        // ------------------------------------------------------------------

        static async Task<bool> EnsureSignedInAsync()
        {
            bool signedIn = PlayerAuthentication.SignedIn != null && await PlayerAuthentication.SignedIn;
            if (!signedIn)
                throw new Exception("couldn't sign in to Unity's online services. Check your internet connection.");
            return true;
        }

        static void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton != null) return;

            // Build the NetworkManager while inactive so its config is in place before Awake/OnEnable run.
            var go = new GameObject("CrittersNetworkManager");
            go.SetActive(false);
            DontDestroyOnLoad(go);
            var transport = go.AddComponent<UnityTransport>();
            var nm = go.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = false };
            go.SetActive(true);
        }

        void HookSessionEvents()
        {
            if (CurrentSession == null) return;
            CurrentSession.PlayerHasLeft += HandlePlayerLeftSession;
            CurrentSession.RemovedFromSession += HandleRemovedFromSession;
            CurrentSession.Deleted += HandleRemovedFromSession;
        }

        void UnhookSessionEvents()
        {
            if (CurrentSession == null) return;
            CurrentSession.PlayerHasLeft -= HandlePlayerLeftSession;
            CurrentSession.RemovedFromSession -= HandleRemovedFromSession;
            CurrentSession.Deleted -= HandleRemovedFromSession;
        }

        void RegisterHandlers()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgToHost);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgToClients);
            if (Mode == SessionMode.Host)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgToHost, HandleMessageToHost);
                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
                nm.OnClientDisconnectCallback += HandleClientDisconnected;
            }
            else
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgToClients, HandleMessageToClient);
                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
                nm.OnClientDisconnectCallback += HandleClientDisconnected;
            }
        }

        void HandleClientConnectedLocal(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || clientId != nm.LocalClientId) return;
            RegisterHandlers();
            SendHello();
        }

        void SendHello() => SendToHost("HELLO", _localName);

        // ----- messages arriving at the host -----
        void HandleMessageToHost(ulong senderClientId, FastBufferReader reader)
        {
            if (Mode != SessionMode.Host) return;
            reader.ReadValueSafe(out string payload);
            SplitMessage(payload, out var kind, out var body);

            switch (kind)
            {
                case "HELLO":
                {
                    if (Lobby.Started)
                    {
                        SendToClient(senderClientId, "REJECT", "That game has already started.");
                        return;
                    }
                    var existing = Lobby.Seats.FirstOrDefault(s => s.ClientId == senderClientId);
                    if (existing == null)
                    {
                        if (Lobby.Seats.Count >= Lobby.MaxSeats)
                        {
                            SendToClient(senderClientId, "REJECT", "The room is full.");
                            return;
                        }
                        int seat = Enumerable.Range(0, Lobby.MaxSeats).First(i => Lobby.Seats.All(s => s.Seat != i));
                        string name = string.IsNullOrWhiteSpace(body) ? $"Player {seat + 1}" : body.Trim();
                        if (name.Length > 20) name = name.Substring(0, 20);
                        Lobby.Seats.Add(new LobbySeat { Seat = seat, Name = name, Faction = FirstFreeFaction(seat), ClientId = senderClientId });
                        Lobby.Seats = Lobby.Seats.OrderBy(s => s.Seat).ToList();
                    }
                    BroadcastLobby();
                    break;
                }
                case "PICK":
                {
                    var seat = Lobby.Seats.FirstOrDefault(s => s.ClientId == senderClientId);
                    if (seat != null && int.TryParse(body, out int faction)) TryAssignFaction(seat.Seat, faction);
                    break;
                }
                case "REQ":
                {
                    if (!InMatch || !GameCommand.TryDecode(body, out var cmd)) return;
                    var seat = Lobby.Seats.FirstOrDefault(s => s.ClientId == senderClientId);
                    if (seat == null) return;
                    cmd.Seat = seat.Seat; // never trust the seat a client claims
                    HostAcceptCommand(cmd);
                    break;
                }
            }
        }

        // ----- messages arriving at a client -----
        void HandleMessageToClient(ulong senderClientId, FastBufferReader reader)
        {
            if (Mode != SessionMode.Client) return;
            reader.ReadValueSafe(out string payload);
            SplitMessage(payload, out var kind, out var body);

            switch (kind)
            {
                case "LOBBY":
                {
                    var snap = JsonUtility.FromJson<LobbySnapshot>(body);
                    if (snap == null) return;
                    Lobby = snap;
                    var nm = NetworkManager.Singleton;
                    var mine = Lobby.Seats.FirstOrDefault(s => nm != null && s.ClientId == nm.LocalClientId);
                    LocalSeat = mine != null ? mine.Seat : -1;
                    if (!InMatch)
                        SetStatus(LocalSeat >= 0 ? $"You're in seat {LocalSeat + 1}. Pick your faction, then wait for the host to start." : "Waiting for a seat...");
                    Changed();
                    break;
                }
                case "START":
                {
                    var info = JsonUtility.FromJson<MatchStartInfo>(body);
                    if (info != null) BeginMatch(info);
                    break;
                }
                case "CMD":
                {
                    // seq;checksum;command
                    var parts = body.Split(new[] { ';' }, 3);
                    if (parts.Length < 3 || !int.TryParse(parts[1], out int checksum)) return;
                    if (int.TryParse(parts[0], out int seq))
                    {
                        if (seq != _commandSequence + 1 && !OutOfSync)
                        {
                            OutOfSync = true;
                            OnLocalNotice?.Invoke("Warning: a game update was missed. Your game may be out of sync with the host's.");
                        }
                        _commandSequence = seq;
                    }
                    if (GameCommand.TryDecode(parts[2], out var cmd)) ApplyCommand(cmd, checksum);
                    break;
                }
                case "REJECT":
                    _ = ResetToMenuAsync($"The host turned you away: {body}");
                    break;
                case "NOTICE":
                    OnLocalNotice?.Invoke(body);
                    break;
            }
        }

        void TryAssignFaction(int seat, int faction)
        {
            var s = Lobby.Seats.FirstOrDefault(x => x.Seat == seat);
            if (s == null || Lobby.Started) return;
            if (Lobby.Seats.Any(x => x.Seat != seat && x.Faction == faction)) return;
            s.Faction = faction;
            BroadcastLobby();
        }

        void HandleClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (Mode == SessionMode.Client)
            {
                if (nm == null || clientId == nm.LocalClientId || clientId == NetworkManager.ServerClientId)
                    _ = ResetToMenuAsync("Lost connection to the host.");
                return;
            }
            if (Mode != SessionMode.Host) return;
            var seat = Lobby.Seats.FirstOrDefault(s => s.ClientId == clientId);
            if (seat == null) return;
            if (!Lobby.Started)
            {
                Lobby.Seats.Remove(seat);
            }
            else
            {
                seat.Connected = false;
                string msg = $"{seat.Name} disconnected. The game can't continue past their turn until they're back.";
                OnLocalNotice?.Invoke(msg);
                BroadcastToClients("NOTICE", msg);
            }
            BroadcastLobby();
        }

        void HandlePlayerLeftSession(string playerId) => Changed();

        void HandleRemovedFromSession()
        {
            if (Mode == SessionMode.Client) _ = ResetToMenuAsync("The host closed the room.");
        }

        void BroadcastLobby()
        {
            if (Mode == SessionMode.Host)
                BroadcastToClients("LOBBY", JsonUtility.ToJson(Lobby));
            Changed();
        }

        void BroadcastToClients(string kind, string body)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.CustomMessagingManager == null) return;
            var targets = nm.ConnectedClientsIds.Where(id => id != nm.LocalClientId).ToList();
            if (targets.Count == 0) return;
            string payload = kind + "\n" + body;
            using (var writer = MakeWriter(payload))
                nm.CustomMessagingManager.SendNamedMessage(MsgToClients, targets, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void SendToClient(ulong clientId, string kind, string body)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || nm.CustomMessagingManager == null) return;
            using (var writer = MakeWriter(kind + "\n" + body))
                nm.CustomMessagingManager.SendNamedMessage(MsgToClients, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        void SendToHost(string kind, string body)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null || !nm.IsConnectedClient)
            {
                OnLocalNotice?.Invoke("Not connected to the host.");
                return;
            }
            using (var writer = MakeWriter(kind + "\n" + body))
                nm.CustomMessagingManager.SendNamedMessage(MsgToHost, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        static FastBufferWriter MakeWriter(string payload)
        {
            int size = 16 + payload.Length * 2;
            var writer = new FastBufferWriter(size, Allocator.Temp, size + 1024);
            writer.WriteValueSafe(payload);
            return writer;
        }

        static void SplitMessage(string payload, out string kind, out string body)
        {
            int nl = payload.IndexOf('\n');
            kind = nl < 0 ? payload : payload.Substring(0, nl);
            body = nl < 0 ? "" : payload.Substring(nl + 1);
        }

        async Task ResetToMenuAsync(string reason)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgToHost);
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgToClients);
            }
            if (nm != null)
            {
                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
                nm.OnClientConnectedCallback -= HandleClientConnectedLocal;
            }

            var session = CurrentSession;
            UnhookSessionEvents();
            CurrentSession = null;
            if (session != null)
            {
                try { await session.LeaveAsync(); }
                catch (Exception ex) { Debug.LogWarning($"[Multiplayer] Leave: {ex.Message}"); }
            }

            Mode = SessionMode.Menu;
            InMatch = false;
            Engine = null;
            LocalSeat = -1;
            ActiveJoinCode = "";
            Lobby = new LobbySnapshot();
            if (reason != null) SetStatus(reason);
            Changed();
            OnReturnedToMenu?.Invoke(reason);
        }

        void OnApplicationQuit()
        {
            if (CurrentSession != null)
            {
                try { CurrentSession.LeaveAsync(); } catch { /* quitting */ }
            }
        }

        // ------------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------------

        int FirstFreeFaction(int seat)
        {
            int count = Enum.GetValues(typeof(FactionId)).Length;
            for (int i = 0; i < count; i++)
            {
                int candidate = (seat + i) % count;
                if (!Lobby.Seats.Any(s => s.Faction == candidate)) return candidate;
            }
            return seat % count;
        }

        static int NewSeed()
        {
            int seed = Guid.NewGuid().GetHashCode();
            return seed == 0 ? 1 : seed;
        }

        void SetStatus(string message)
        {
            StatusMessage = message;
            Changed();
        }

        void Changed() => OnSessionUpdated?.Invoke();
    }
}
