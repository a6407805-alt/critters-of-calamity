using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CrittersOfCalamity.Visuals;

namespace CrittersOfCalamity.UI
{
    /// <summary>
    /// Settings window (main menu and top bar): screen resolution and display mode.
    /// The choice is also written to %LOCALAPPDATA%\CrittersOfCalamity\display.txt, which the launcher reads,
    /// so the launcher and the game always agree on how the game starts.
    /// </summary>
    public partial class CrittersTabletopHUD
    {
        private GameObject _settingsRoot;
        private Text _settingsResText;
        private Text _settingsInfoText;
        private readonly Button[] _settingsModeButtons = new Button[3];
        private List<Vector2Int> _settingsResolutions = new List<Vector2Int>();
        private int _settingsResIndex;
        private int _settingsMode;           // 0 = Fullscreen (borderless), 1 = Exclusive fullscreen, 2 = Windowed
        private Coroutine _settingsRevert;

        private static readonly string[] DisplayModeNames = { "Fullscreen", "Exclusive fullscreen", "Windowed" };
        private static readonly string[] DisplayModeKeys = { "borderless", "exclusive", "windowed" };

        /// <summary>The settings file shared with the launcher: "width height mode".</summary>
        public static string DisplaySettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrittersOfCalamity", "display.txt");

        private static FullScreenMode ToUnityMode(int mode) =>
            mode == 1 ? FullScreenMode.ExclusiveFullScreen : mode == 2 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;

        private static int FromUnityMode(FullScreenMode m) =>
            m == FullScreenMode.ExclusiveFullScreen ? 1 : (m == FullScreenMode.Windowed || m == FullScreenMode.MaximizedWindow) ? 2 : 0;

        private void BuildSettingsWindow()
        {
            if (_settingsRoot != null) return;

            // Its own canvas on top of everything, so it works from the main menu and during a game.
            var canvasGo = new GameObject("Settings_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _settingsRoot = canvasGo;

            // Dim the screen behind the window and block clicks
            var shade = CreatePanel(canvasGo.transform, "Shade", Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));

            var win = CreatePanel(shade, "SettingsWindow", new Vector2(0.30f, 0.25f), new Vector2(0.70f, 0.75f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.10f, 0.13f, 0.18f, 0.98f));

            CreateText(win, "Title", "SETTINGS", 26, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.86f), new Vector2(0.70f, 0.97f), Vector2.zero, Vector2.zero, Palette.AuthorityGold);
            CreateButton(win, "BtnCloseSettings", "Close", new Vector2(0.78f, 0.87f), new Vector2(0.95f, 0.96f),
                Palette.FromHex("#7A2828"), () => _settingsRoot.SetActive(false));

            // Display mode
            CreateText(win, "ModeLabel", "Display mode", 18, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.80f), Vector2.zero, Vector2.zero, Color.white);
            for (int i = 0; i < 3; i++)
            {
                int m = i;
                float x = 0.05f + i * 0.305f;
                _settingsModeButtons[i] = CreateButton(win, $"BtnMode{i}", DisplayModeNames[i], new Vector2(x, 0.61f), new Vector2(x + 0.29f, 0.71f),
                    Palette.UIPanelMid, () => { _settingsMode = m; RefreshSettingsWindow(); });
            }

            // Resolution: arrows cycle through the sizes this screen supports
            CreateText(win, "ResLabel", "Resolution", 18, TextAnchor.MiddleLeft,
                new Vector2(0.05f, 0.49f), new Vector2(0.95f, 0.57f), Vector2.zero, Vector2.zero, Color.white);
            CreateButton(win, "BtnResPrev", "<", new Vector2(0.05f, 0.38f), new Vector2(0.17f, 0.48f), Palette.UIButtonBlue,
                () => { _settingsResIndex = Mathf.Max(0, _settingsResIndex - 1); RefreshSettingsWindow(); });
            var resBg = CreatePanel(win, "ResBox", new Vector2(0.19f, 0.38f), new Vector2(0.81f, 0.48f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, new Color(0.18f, 0.22f, 0.28f, 1f));
            _settingsResText = CreateText(resBg, "ResValue", "", 20, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            CreateButton(win, "BtnResNext", ">", new Vector2(0.83f, 0.38f), new Vector2(0.95f, 0.48f), Palette.UIButtonBlue,
                () => { _settingsResIndex = Mathf.Min(_settingsResolutions.Count - 1, _settingsResIndex + 1); RefreshSettingsWindow(); });

            _settingsInfoText = CreateText(win, "Info", "", 15, TextAnchor.UpperLeft,
                new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.35f), Vector2.zero, Vector2.zero, Palette.ParchmentMap);

            CreateButton(win, "BtnMatchScreen", "Match my screen", new Vector2(0.05f, 0.04f), new Vector2(0.45f, 0.15f),
                Palette.UIPanelMid, () =>
                {
                    var native = NativeResolution();
                    _settingsResIndex = Mathf.Max(0, _settingsResolutions.FindIndex(r => r == native));
                    _settingsMode = 0;
                    RefreshSettingsWindow();
                });
            CreateButton(win, "BtnApplySettings", "Apply", new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.15f),
                Palette.FromHex("#2F7A3E"), ApplySettings);

            _settingsRoot.SetActive(false);
        }

        /// <summary>The desktop's own resolution (what "Match my screen" picks).</summary>
        private static Vector2Int NativeResolution()
        {
            var r = Screen.currentResolution;
            return new Vector2Int(r.width, r.height);
        }

        private void OpenSettings()
        {
            BuildSettingsWindow();
            var native = NativeResolution();
            _settingsResolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Where(r => r.x >= 800 && r.y >= 600 && r.x <= native.x && r.y <= native.y)
                .Distinct()
                .OrderBy(r => r.x * r.y).ThenBy(r => r.x)
                .ToList();
            if (!_settingsResolutions.Contains(native)) _settingsResolutions.Add(native);
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!_settingsResolutions.Contains(current)) _settingsResolutions.Add(current);
            _settingsResolutions = _settingsResolutions.OrderBy(r => r.x * r.y).ThenBy(r => r.x).ToList();
            _settingsResIndex = _settingsResolutions.IndexOf(current);
            _settingsMode = FromUnityMode(Screen.fullScreenMode);
            _settingsRoot.SetActive(true);
            RefreshSettingsWindow();
        }

        private void RefreshSettingsWindow()
        {
            if (_settingsRoot == null) return;
            for (int i = 0; i < 3; i++)
                _settingsModeButtons[i].GetComponent<Image>().color = i == _settingsMode ? Palette.FromHex("#B86B28") : Palette.UIPanelMid;
            var r = _settingsResolutions.Count > 0 ? _settingsResolutions[Mathf.Clamp(_settingsResIndex, 0, _settingsResolutions.Count - 1)] : NativeResolution();
            var native = NativeResolution();
            _settingsResText.text = $"{r.x} x {r.y}" + (r == native ? "  (your screen)" : "");
            if (_settingsRevert == null)
                _settingsInfoText.text = $"Now: {Screen.width} x {Screen.height}, {DisplayModeNames[FromUnityMode(Screen.fullScreenMode)]}.\n" +
                    "If the game doesn't fit your screen, click \"Match my screen\" and then Apply. " +
                    "In Windowed mode you can also drag the window's edges.";
        }

        private void ApplySettings()
        {
            if (_settingsResolutions.Count == 0) return;
            var oldW = Screen.width; var oldH = Screen.height; var oldMode = Screen.fullScreenMode;
            var r = _settingsResolutions[Mathf.Clamp(_settingsResIndex, 0, _settingsResolutions.Count - 1)];
            Screen.SetResolution(r.x, r.y, ToUnityMode(_settingsMode));
            if (_settingsRevert != null) StopCoroutine(_settingsRevert);
            _settingsRevert = StartCoroutine(ConfirmOrRevert(oldW, oldH, oldMode, r, _settingsMode));
        }

        /// <summary>After a change, keep it only if the player clicks Keep within 15 seconds (a bad size can hide the window).</summary>
        private IEnumerator ConfirmOrRevert(int oldW, int oldH, FullScreenMode oldMode, Vector2Int r, int mode)
        {
            bool kept = false;
            var keep = CreateButton(_settingsInfoText.transform.parent, "BtnKeepSettings", "Keep these settings",
                new Vector2(0.55f, 0.17f), new Vector2(0.95f, 0.30f), Palette.FromHex("#B86B28"), () => kept = true);
            for (float t = 15f; t > 0f && !kept; t -= Time.unscaledDeltaTime)
            {
                _settingsInfoText.text = $"Keep {r.x} x {r.y}, {DisplayModeNames[mode]}?\nGoing back in {Mathf.CeilToInt(t)} s.";
                yield return null;
            }
            Destroy(keep.gameObject);
            _settingsRevert = null;
            if (kept)
            {
                SaveDisplaySettings(r.x, r.y, mode);
            }
            else
            {
                Screen.SetResolution(oldW, oldH, oldMode);
                yield return null;
            }
            RefreshSettingsWindow();
        }

        private static void SaveDisplaySettings(int w, int h, int mode)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DisplaySettingsPath));
                File.WriteAllText(DisplaySettingsPath, $"{w} {h} {DisplayModeKeys[mode]}");
            }
            catch (Exception e) { Debug.LogWarning("[Settings] Couldn't save display settings: " + e.Message); }
        }
    }
}
