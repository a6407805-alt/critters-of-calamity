using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Engine;
using CrittersOfCalamity.Model;
using CrittersOfCalamity.Multiplayer;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// The popup for a decision the game is waiting on (Prophetic Vision, Martyrdom, "When Crafted" relic choices,
    /// Plague, Ignite...). The deciding player sees buttons and/or green tiles to click; everyone else sees who
    /// the game is waiting on.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private GameObject _choicePopup;
        private Text _choiceTitle;
        private Text _choicePrompt;
        private Transform _choiceList;
        private string _choiceShownKey;

        private void BuildChoicePopup(Transform canvasParent)
        {
            var root = CreatePanel(canvasParent, "ChoicePopup", new Vector2(0.27f, 0.50f), new Vector2(0.58f, 0.88f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.10f, 0.08f, 0.14f, 0.97f));
            _choicePopup = root.gameObject;
            _choiceTitle = CreateText(root, "Title", "", 20, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.86f), new Vector2(0.96f, 0.98f),
                Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            _choicePrompt = CreateText(root, "Prompt", "", 15, TextAnchor.UpperLeft, new Vector2(0.04f, 0.64f), new Vector2(0.96f, 0.86f),
                Vector2.zero, Vector2.zero, Palette.ParchmentMap);
            _choiceList = BuildScrollList(root, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.63f));
            _choicePopup.SetActive(false);
        }

        private void RefreshChoicePopup()
        {
            if (_choicePopup == null) return;
            var c = _engine != null && _engine.Players.Count > 0 ? _engine.PendingChoice : null;
            if (c == null) { _choicePopup.SetActive(false); _choiceShownKey = null; return; }

            // A choice (Prophetic Vision and others) waits until the table-wide popups (End phase summary etc.) are closed.
            if (_announcePopup != null && _announcePopup.activeSelf) { _choicePopup.SetActive(false); return; }

            bool mine = ControlsSeat(c.PlayerIndex);
            string key = $"{c.Kind}|{c.PlayerIndex}|{c.Options.Count}|{c.Tiles.Count}|{mine}|{_engine.TurnNumber}";
            _choicePopup.SetActive(true);
            _choicePopup.transform.SetAsLastSibling();
            if (key == _choiceShownKey) return; // don't rebuild (and lose scroll) on every refresh
            _choiceShownKey = key;

            foreach (Transform t in _choiceList) Destroy(t.gameObject);
            var who = _engine.Players[c.PlayerIndex];
            _choiceTitle.text = c.Title;
            if (!mine)
            {
                _choicePrompt.text = $"Waiting on <b>{who.DisplayName}</b> to decide.";
                return;
            }
            _choicePrompt.text = (_mpManager.IsOnline ? "" : $"<b>{who.DisplayName}</b>: ") + c.Prompt +
                                 (c.Tiles.Count > 0 ? "\n<color=#7CE07C>Click one of the green tiles on the board.</color>" : "");

            for (int i = 0; i < c.Options.Count; i++)
            {
                int opt = i;
                var btn = CreateLayoutButton(_choiceList, "Opt", c.Options[i], new Color(0.30f, 0.18f, 0.38f, 1f),
                    () => _mpManager.Submit(GameCommand.Choice(opt, default(HexCoord))));
                btn.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                btn.interactable = c.OptionEnabled[i];
                SetFontSize(btn.GetComponentInChildren<Text>(), 14);
                // Choices about relic cards (Prophetic Vision, Ignite, ...): hover a card's button to see the whole card
                if (c.Cards != null && i < c.Cards.Count) AttachCardHover(btn.gameObject, c.Cards[i]);
            }
            if (c.CanSkip)
            {
                var skip = CreateLayoutButton(_choiceList, "Skip", c.SkipLabel, Palette.FromHex("#4A4F5A"),
                    () => _mpManager.Submit(GameCommand.Choice(CrittersGameEngine.ChooseSkip, default(HexCoord))));
                skip.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
                SetFontSize(skip.GetComponentInChildren<Text>(), 14);
            }
        }

        /// <summary>A board click while a choice wants a tile. Returns true if the click was used.</summary>
        private bool HandleChoiceTileClick(HexCoord coord)
        {
            var c = _engine?.PendingChoice;
            if (c == null || c.Tiles.Count == 0 || !ControlsSeat(c.PlayerIndex)) return false;
            if (!c.Tiles.Contains(coord)) { ShowLocalNotice("Pick one of the green tiles."); return true; }
            _mpManager.Submit(GameCommand.Choice(CrittersGameEngine.ChooseTile, coord));
            return true;
        }
    }
}
