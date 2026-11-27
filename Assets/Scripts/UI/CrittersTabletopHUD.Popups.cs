using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Table-wide popups: announcements everyone sees (Grimoire mills, Forage cards, Fire Tile resolution),
    /// the Crusade side panel, and a confirm dialog (for leaving the game).
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private GameObject _announcePopup;
        private Text _announceTitle;
        private Text _announceBody;
        private Text _announceCount;
        private readonly Queue<KeyValuePair<string, string>> _announcements = new Queue<KeyValuePair<string, string>>();

        private GameObject _crusadePanel;
        private Text _crusadeText;

        private GameObject _confirmPopup;
        private Text _confirmTitle;
        private Text _confirmBody;
        private System.Action _confirmYes;

        private void BuildPopups(Transform canvasParent)
        {
            // Announcement popup (top middle, under the top bar)
            var ann = CreatePanel(canvasParent, "AnnouncementPopup", new Vector2(0.30f, 0.56f), new Vector2(0.70f, 0.90f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.09f, 0.08f, 0.12f, 0.97f));
            _announcePopup = ann.gameObject;
            var outline = _announcePopup.AddComponent<Outline>();
            outline.effectColor = Palette.AuthorityGold;
            outline.effectDistance = new Vector2(2f, -2f);
            _announceTitle = CreateText(ann, "Title", "", 20, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.84f), new Vector2(0.80f, 0.98f),
                Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _announceCount = CreateText(ann, "Count", "", 13, TextAnchor.MiddleRight, new Vector2(0.80f, 0.84f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, Palette.UITextMuted);
            _announceBody = CreateText(ann, "Body", "", 16, TextAnchor.UpperLeft, new Vector2(0.04f, 0.17f), new Vector2(0.96f, 0.83f),
                Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            _announceBody.resizeTextMinSize = 10;
            CreateButton(ann, "BtnOk", "OK", new Vector2(0.38f, 0.03f), new Vector2(0.62f, 0.14f), Palette.FromHex("#2F7A3E"), NextAnnouncement);
            _announcePopup.SetActive(false);

            // Crusade side panel (right edge, above the Turn Guide area)
            var cru = CreatePanel(canvasParent, "CrusadePanel", new Vector2(0.585f, 0.70f), new Vector2(0.745f, 0.93f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.16f, 0.30f, 0.34f, 0.95f));
            _crusadePanel = cru.gameObject;
            _crusadeText = CreateText(cru, "Text", "", 14, TextAnchor.UpperLeft, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f),
                Vector2.zero, Vector2.zero, Color.white);
            _crusadeText.resizeTextMinSize = 9;
            _crusadeText.raycastTarget = false;
            cru.GetComponent<Image>().raycastTarget = false;
            _crusadePanel.SetActive(false);

            // Confirm dialog
            var conf = CreatePanel(canvasParent, "ConfirmPopup", new Vector2(0.36f, 0.40f), new Vector2(0.64f, 0.60f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.08f, 0.10f, 0.99f));
            _confirmPopup = conf.gameObject;
            _confirmTitle = CreateText(conf, "Title", "", 20, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.70f), new Vector2(0.96f, 0.96f),
                Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _confirmBody = CreateText(conf, "Body", "", 15, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.36f), new Vector2(0.94f, 0.70f),
                Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            CreateButton(conf, "BtnYes", "Yes, leave", new Vector2(0.08f, 0.07f), new Vector2(0.46f, 0.30f), Palette.FromHex("#7A2828"), () =>
            {
                _confirmPopup.SetActive(false);
                var yes = _confirmYes;
                _confirmYes = null;
                yes?.Invoke();
            });
            CreateButton(conf, "BtnNo", "Stay", new Vector2(0.54f, 0.07f), new Vector2(0.92f, 0.30f), Palette.UIPanelMid, () =>
            {
                _confirmPopup.SetActive(false);
                _confirmYes = null;
            });
            _confirmPopup.SetActive(false);
        }

        private void ShowConfirm(string title, string body, System.Action onYes)
        {
            if (_confirmPopup == null) { onYes?.Invoke(); return; }
            _confirmTitle.text = title;
            _confirmBody.text = body;
            _confirmYes = onYes;
            _confirmPopup.SetActive(true);
            _confirmPopup.transform.SetAsLastSibling();
        }

        private void OnEngineAnnouncement(string title, string body)
        {
            _announcements.Enqueue(new KeyValuePair<string, string>(title, body));
            if (_announcePopup != null && !_announcePopup.activeSelf) NextAnnouncement();
            else UpdateAnnouncementCount();
        }

        private void NextAnnouncement()
        {
            if (_announcePopup == null) return;
            if (_announcements.Count == 0) { _announcePopup.SetActive(false); return; }
            var a = _announcements.Dequeue();
            _announceTitle.text = a.Key;
            _announceBody.text = a.Value;
            UpdateAnnouncementCount();
            _announcePopup.SetActive(true);
            _announcePopup.transform.SetAsLastSibling();
        }

        private void UpdateAnnouncementCount()
        {
            if (_announceCount != null) _announceCount.text = _announcements.Count > 0 ? $"{_announcements.Count} more" : "";
        }

        /// <summary>While the Platypus Papacy is on Crusade, a side panel says so, with the buffs and how long it lasts.</summary>
        private void RefreshCrusadePanel()
        {
            if (_crusadePanel == null) return;
            var pope = _engine != null && !_engine.IsPreGame
                ? _engine.Players.FirstOrDefault(p => p.Faction != null && p.Faction.Id == FactionId.PlatypusPapacy && (p.CrusadeActive || p.CrusadeTurnsRemaining > 0))
                : null;
            _crusadePanel.SetActive(pope != null);
            if (pope == null) return;
            var lines = new List<string>
            {
                $"<b><color=#F2B83B>{pope.DisplayName}'s Platypus Papacy is on Crusade!</color></b>",
                pope.CrusadeActive
                    ? $"Lasts until the start of their turn after next ({pope.CrusadeTurnsRemaining} more Crusade turn(s) after this one)."
                    : $"Starts at the beginning of their next turn ({pope.CrusadeTurnsRemaining} Crusade turn(s)).",
                "During the Crusade:",
                " • Crusaders gain +1 Attack.",
                " • +1 extra Authority for removing an enemy building, token or special unit.",
                " • Winning a battle gives 1 Faith."
            };
            if (pope.PlatyPopeLevel >= 4)
                lines.Add(" • Level 4: each enemy unit Crusaders remove gives 1 Authority (and enemies gain 1 per Crusader they remove).");
            _crusadeText.text = string.Join("\n", lines);
        }
    }
}
