using System;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;
using CrittersOfCalamity.Core;

namespace CrittersOfCalamity.Multiplayer
{
    /// <summary>
    /// Manages multiplayer room creation and joining via Unity Multiplayer Services (ISession / MultiplayerService.Instance)
    /// supporting up to 4 active players plus big-group spectators via join codes.
    /// Falls back gracefully to local hotseat / tabletop mode when offline.
    /// </summary>
    public class CrittersMultiplayerSessionManager : MonoBehaviour
    {
        public ISession CurrentSession { get; private set; }
        public string ActiveJoinCode { get; private set; } = "LOCAL-TABLE";
        public string StatusMessage { get; private set; } = "Local 4-Player Tabletop Mode Ready";

        public event Action OnSessionUpdated;

        public async Task<string> HostGroupRoomAsync(string sessionName = "CrittersContinent", int maxSeats = 12)
        {
            try
            {
                StatusMessage = "Connecting to Unity Multiplayer Services...";
                OnSessionUpdated?.Invoke();

                bool signedIn = PlayerAuthentication.SignedIn != null && await PlayerAuthentication.SignedIn;
                if (!signedIn)
                {
                    ActiveJoinCode = "LOCAL-" + UnityEngine.Random.Range(1000, 9999);
                    StatusMessage = $"Local Room Hosted ({ActiveJoinCode}) — 4 Players + Group Spectators";
                    OnSessionUpdated?.Invoke();
                    return ActiveJoinCode;
                }

                var options = new SessionOptions
                {
                    Name = sessionName,
                    MaxPlayers = maxSeats, // 4 Active Faction Players + up to 8 Spectator Seats for big group nights
                    IsPrivate = false
                }.WithRelayNetwork();

                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                ActiveJoinCode = CurrentSession.Code;
                StatusMessage = $"Online Room Hosted! Join Code: {ActiveJoinCode} ({CurrentSession.Players.Count}/{maxSeats} in room)";
                OnSessionUpdated?.Invoke();
                return ActiveJoinCode;
            }
            catch (Exception ex)
            {
                ActiveJoinCode = "TABLE-" + UnityEngine.Random.Range(1000, 9999);
                StatusMessage = $"Room Hosted ({ActiveJoinCode}) [Offline/Local Fallback: {ex.Message}]";
                OnSessionUpdated?.Invoke();
                return ActiveJoinCode;
            }
        }

        public async Task<bool> JoinGroupRoomByCodeAsync(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode)) return false;
            try
            {
                StatusMessage = $"Joining Room '{joinCode}'...";
                OnSessionUpdated?.Invoke();

                bool signedIn = PlayerAuthentication.SignedIn != null && await PlayerAuthentication.SignedIn;
                if (!signedIn)
                {
                    ActiveJoinCode = joinCode.Trim().ToUpperInvariant();
                    StatusMessage = $"Joined Local Room {ActiveJoinCode}";
                    OnSessionUpdated?.Invoke();
                    return true;
                }

                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());
                ActiveJoinCode = CurrentSession.Code;
                StatusMessage = $"Connected to Room {ActiveJoinCode} ({CurrentSession.Players.Count} players)";
                OnSessionUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not join code '{joinCode}': {ex.Message}";
                OnSessionUpdated?.Invoke();
                return false;
            }
        }
    }
}
