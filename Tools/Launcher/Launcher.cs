// Critters of Calamity Launcher
// A small Windows app (.NET Framework 4.8, built into Windows 10/11) that keeps the game up to date:
//   1. reads latest.json from the public releases repo (ebenlyon/critters-of-calamity-releases),
//   2. downloads the game zip when there is a newer version, checks its SHA-256, and installs it,
//   3. launches the game.
// It also updates itself when latest.json lists a newer launcher.
// Build: see Tools/Launcher/build.sh (compiles with mono's C# compiler; no Visual Studio needed).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Critters of Calamity Launcher")]
[assembly: AssemblyProduct("Critters of Calamity")]
[assembly: AssemblyVersion("1.1.2.0")]
[assembly: AssemblyFileVersion("1.1.2.0")]

namespace CrittersOfCalamity.Launcher
{
    static class Config
    {
        // The public repository that holds the game builds and latest.json.
        public const string RawBase = "https://raw.githubusercontent.com/ebenlyon/critters-of-calamity-releases/main/";
        public const string Manifest = RawBase + "latest.json";
        public const string GameExe = "Critters of Calamity.exe";
        public static readonly Version LauncherVersion = new Version(1, 1, 2);

        public static string InstallRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrittersOfCalamity");
        public static string GameDir => Path.Combine(InstallRoot, "Game");
        public static string VersionFile => Path.Combine(GameDir, "version.txt");
        /// <summary>Screen settings shared with the game: "width height mode" (mode = borderless, exclusive or windowed).</summary>
        public static string DisplayFile => Path.Combine(InstallRoot, "display.txt");
    }

    class ReleaseInfo
    {
        public string Tag;            // e.g. v0.2.0
        public string Notes;
        public string GameUrl;
        public long GameSize;
        public string GameSha256;
        public string LauncherUrl;
        public Version LauncherVersion;
    }

    class LauncherForm : Form
    {
        // Colors from the game's Palette (art bible): slate panels, parchment text, gold accent
        static readonly Color Bg = ColorTranslator.FromHtml("#1A222D");
        static readonly Color Panel = ColorTranslator.FromHtml("#2A3442");
        static readonly Color Parchment = ColorTranslator.FromHtml("#E5D5B5");
        static readonly Color Gold = ColorTranslator.FromHtml("#F2B83B");
        static readonly Color Muted = ColorTranslator.FromHtml("#AAB8C6");
        static readonly Color Orange = ColorTranslator.FromHtml("#C87A28");

        readonly Label _status = new Label();
        readonly Label _versions = new Label();
        readonly ProgressBar _progress = new ProgressBar();
        readonly Button _play = new Button();
        readonly Button _check = new Button();
        readonly Button _settings = new Button();
        readonly TextBox _notes = new TextBox();
        ReleaseInfo _latest;
        bool _busy;

        public LauncherForm()
        {
            Text = "Critters of Calamity";
            ClientSize = new Size(620, 400);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Parchment;
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var title = new Label
            {
                Text = "CRITTERS OF CALAMITY", ForeColor = Gold, Font = new Font("Segoe UI", 22f, FontStyle.Bold),
                AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Bounds = new Rectangle(0, 18, 620, 50)
            };
            _versions.SetBounds(20, 72, 580, 22);
            _versions.ForeColor = Muted;
            _versions.TextAlign = ContentAlignment.MiddleCenter;

            _notes.SetBounds(20, 102, 580, 150);
            _notes.Multiline = true;
            _notes.ReadOnly = true;
            _notes.ScrollBars = ScrollBars.Vertical;
            _notes.BackColor = Panel;
            _notes.ForeColor = Parchment;
            _notes.BorderStyle = BorderStyle.None;

            _status.SetBounds(20, 262, 580, 24);
            _status.ForeColor = Parchment;
            _progress.SetBounds(20, 290, 580, 18);

            _play.SetBounds(330, 326, 270, 52);
            StyleButton(_play, Orange, 14f);
            _play.Click += async (s, e) => await PlayOrUpdate();

            _check.SetBounds(20, 326, 175, 52);
            StyleButton(_check, Panel, 11f);
            _check.Text = "Check for updates";
            _check.Click += async (s, e) => await CheckForUpdates();

            _settings.SetBounds(205, 326, 115, 52);
            StyleButton(_settings, Panel, 11f);
            _settings.Text = "Settings";
            _settings.Click += (s, e) => { using (var f = new SettingsForm()) f.ShowDialog(this); };

            Controls.AddRange(new Control[] { title, _versions, _notes, _status, _progress, _play, _check, _settings });
            Shown += async (s, e) => await CheckForUpdates();
        }

        internal static void StyleButton(Button b, Color bg, float size)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", size, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
        }

        static string InstalledVersion() => File.Exists(Config.VersionFile) ? File.ReadAllText(Config.VersionFile).Trim() : null;
        static bool Installed() => File.Exists(Path.Combine(Config.GameDir, Config.GameExe));
        static string Clean(string tag) => (tag ?? "").Trim().TrimStart('v', 'V');

        bool UpdateAvailable => _latest != null && _latest.GameUrl != null && Clean(_latest.Tag) != Clean(InstalledVersion());

        void RefreshButtons()
        {
            string installed = InstalledVersion();
            _versions.Text = $"Installed: {(Installed() ? "v" + Clean(installed) : "not installed")}     " +
                             $"Latest: {(_latest != null ? _latest.Tag : "unknown")}     Launcher v{Config.LauncherVersion}";
            if (!Installed()) _play.Text = _latest?.GameUrl != null ? "Install" : "Not available";
            else _play.Text = UpdateAvailable ? $"Update to {_latest.Tag}" : "Play";
            _play.Enabled = !_busy && (Installed() || _latest?.GameUrl != null);
            _check.Enabled = !_busy;
        }

        async Task CheckForUpdates()
        {
            _busy = true;
            _status.Text = "Checking for updates...";
            RefreshButtons();
            try
            {
                _latest = await Task.Run(() => FetchLatest());
                _notes.Text = string.IsNullOrWhiteSpace(_latest.Notes) ? "(No release notes.)" : _latest.Notes.Replace("\n", Environment.NewLine);
                _status.Text = UpdateAvailable ? $"Version {_latest.Tag} is ready to {(Installed() ? "update to" : "install")}." : "You have the latest version.";
                if (_latest.LauncherUrl != null && _latest.LauncherVersion != null && _latest.LauncherVersion > Config.LauncherVersion)
                {
                    _busy = false;
                    await UpdateLauncher();
                    return;
                }
            }
            catch (Exception ex)
            {
                _status.Text = Installed() ? "Couldn't reach the update server. You can still play the installed version." : "Couldn't reach the update server: " + ex.Message;
            }
            _busy = false;
            RefreshButtons();
        }

        static WebClient NewClient()
        {
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "CrittersOfCalamity-Launcher";
            return wc;
        }

        static ReleaseInfo FetchLatest()
        {
            string json;
            using (var wc = NewClient())
            {
                wc.Headers[HttpRequestHeader.CacheControl] = "no-cache";
                json = wc.DownloadString(Config.Manifest + "?t=" + DateTime.UtcNow.Ticks);
            }
            var d = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            Func<string, string> str = k => d.ContainsKey(k) && d[k] != null ? d[k].ToString() : null;
            var info = new ReleaseInfo
            {
                Tag = "v" + Clean(str("version")),
                Notes = str("notes") ?? "",
                GameUrl = str("game") != null ? Config.RawBase + str("game") : null,
                GameSize = str("gameSize") != null ? long.Parse(str("gameSize")) : 0,
                GameSha256 = str("gameSha256"),
                LauncherUrl = str("launcher") != null ? Config.RawBase + str("launcher") : null,
            };
            if (str("launcherVersion") != null && Version.TryParse(str("launcherVersion"), out var lv)) info.LauncherVersion = lv;
            return info;
        }

        static string Sha256Of(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        async Task PlayOrUpdate()
        {
            if (_busy) return;
            if (!Installed() || UpdateAvailable)
            {
                if (!await InstallLatest()) return;
            }
            Launch();
        }

        async Task<bool> InstallLatest()
        {
            _busy = true;
            RefreshButtons();
            string tmpZip = Path.Combine(Path.GetTempPath(), "CrittersOfCalamity-update.zip");
            string staging = Config.GameDir + ".new";
            try
            {
                Directory.CreateDirectory(Config.InstallRoot);
                _status.Text = $"Downloading {_latest.Tag}...";
                using (var wc = NewClient())
                {
                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        _progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
                        _status.Text = $"Downloading {_latest.Tag}: {e.BytesReceived / (1024 * 1024)} of {Math.Max(1, (_latest.GameSize > 0 ? _latest.GameSize : e.TotalBytesToReceive) / (1024 * 1024))} MB";
                    };
                    await wc.DownloadFileTaskAsync(new Uri(_latest.GameUrl + "?t=" + DateTime.UtcNow.Ticks), tmpZip);
                }
                _status.Text = "Checking the download...";
                if (!string.IsNullOrEmpty(_latest.GameSha256))
                {
                    string got = await Task.Run(() => Sha256Of(tmpZip));
                    if (!string.Equals(got, _latest.GameSha256, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("the download was damaged (checksum mismatch). Please try again.");
                }
                _status.Text = "Installing...";
                await Task.Run(() =>
                {
                    if (Directory.Exists(staging)) Directory.Delete(staging, true);
                    ZipFile.ExtractToDirectory(tmpZip, staging);
                    File.WriteAllText(Path.Combine(staging, "version.txt"), Clean(_latest.Tag));
                    if (Directory.Exists(Config.GameDir)) Directory.Delete(Config.GameDir, true);
                    Directory.Move(staging, Config.GameDir);
                    File.Delete(tmpZip);
                });
                _progress.Value = 100;
                _status.Text = $"Installed {_latest.Tag}.";
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = "Update failed: " + ex.Message + (Installed() ? " (The installed version still works.)" : "");
                return false;
            }
            finally
            {
                _busy = false;
                RefreshButtons();
            }
        }

        void Launch()
        {
            string exe = Path.Combine(Config.GameDir, Config.GameExe);
            if (!File.Exists(exe)) { _status.Text = "The game isn't installed yet."; return; }
            string args = "";
            try { args = DisplaySettings.Load().ToArgs(); } catch { }   // a settings problem never stops the game from starting
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Config.GameDir, UseShellExecute = true, Arguments = args });
            Close();
        }

        /// <summary>Downloads the newer launcher next to this one, then swaps it in with a tiny script and restarts.</summary>
        async Task UpdateLauncher()
        {
            _busy = true;
            RefreshButtons();
            try
            {
                string me = Application.ExecutablePath;
                string fresh = me + ".new";
                _status.Text = "Updating the launcher...";
                using (var wc = NewClient()) await wc.DownloadFileTaskAsync(new Uri(_latest.LauncherUrl + "?t=" + DateTime.UtcNow.Ticks), fresh);
                // Only swap in a launcher that really is newer (a stale cached copy would otherwise be installed)
                Version got;
                if (!Version.TryParse(FileVersionInfo.GetVersionInfo(fresh).FileVersion ?? "", out got) || got <= new Version(Config.LauncherVersion.Major, Config.LauncherVersion.Minor, Config.LauncherVersion.Build, 0))
                {
                    try { File.Delete(fresh); } catch { }
                    _status.Text = "A launcher update is on its way; try again in a few minutes.";
                    _busy = false;
                    RefreshButtons();
                    return;
                }
                string script = Path.Combine(Path.GetTempPath(), "coc-launcher-update.cmd");
                File.WriteAllText(script,
                    "@echo off\r\n" +
                    "ping 127.0.0.1 -n 3 > nul\r\n" +
                    $"move /y \"{fresh}\" \"{me}\" > nul\r\n" +
                    $"start \"\" \"{me}\"\r\n");
                Process.Start(new ProcessStartInfo(script) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, UseShellExecute = true });
                Application.Exit();
            }
            catch (Exception ex)
            {
                _status.Text = "Launcher update failed (" + ex.Message + "). Continuing with this version.";
                _busy = false;
                RefreshButtons();
            }
        }
    }

    /// <summary>The screen size and display mode the game starts with. Shared with the game's own Settings window.</summary>
    class DisplaySettings
    {
        public int Width, Height;
        public string Mode = "borderless";   // borderless (fullscreen window), exclusive (fullscreen), windowed

        public static readonly string[] Modes = { "borderless", "exclusive", "windowed" };
        public static readonly string[] ModeNames = { "Fullscreen", "Exclusive fullscreen", "Windowed" };

        /// <summary>Saved settings, or the desktop's own resolution in fullscreen if none were saved.</summary>
        public static DisplaySettings Load()
        {
            var d = new DisplaySettings();
            var native = Screens.Native();
            d.Width = native.Width; d.Height = native.Height;
            try
            {
                if (File.Exists(Config.DisplayFile))
                {
                    var p = File.ReadAllText(Config.DisplayFile).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3 && int.TryParse(p[0], out var w) && int.TryParse(p[1], out var h) && w >= 640 && h >= 480 && Modes.Contains(p[2]))
                    { d.Width = w; d.Height = h; d.Mode = p[2]; }
                }
            }
            catch { }
            // Never start bigger than the screen
            if (d.Width > native.Width || d.Height > native.Height) { d.Width = native.Width; d.Height = native.Height; }
            return d;
        }

        public void Save()
        {
            Directory.CreateDirectory(Config.InstallRoot);
            File.WriteAllText(Config.DisplayFile, $"{Width} {Height} {Mode}");
        }

        /// <summary>Unity's standard command-line options for the player window.</summary>
        public string ToArgs() =>
            $"-screen-width {Width} -screen-height {Height} -screen-fullscreen {(Mode == "windowed" ? 0 : 1)} -window-mode {Mode}";
    }

    /// <summary>Reads the real (not DPI-scaled) display modes of the main screen.</summary>
    static class Screens
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        const int ENUM_CURRENT_SETTINGS = -1;

        public static Size Native()
        {
            try
            {
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmPelsWidth > 0) return new Size(dm.dmPelsWidth, dm.dmPelsHeight);
            }
            catch { }
            return Screen.PrimaryScreen.Bounds.Size;
        }

        public static List<Size> All()
        {
            var native = Native();
            var list = new List<Size>();
            try
            {
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                for (int i = 0; EnumDisplaySettings(null, i, ref dm); i++)
                {
                    var sz = new Size(dm.dmPelsWidth, dm.dmPelsHeight);
                    if (sz.Width >= 800 && sz.Height >= 600 && sz.Width <= native.Width && sz.Height <= native.Height && !list.Contains(sz)) list.Add(sz);
                }
            }
            catch { }
            if (!list.Contains(native)) list.Add(native);
            return list.OrderByDescending(z => z.Width * z.Height).ThenByDescending(z => z.Width).ToList();
        }
    }

    /// <summary>Lets the player pick the game's screen size and display mode before playing.</summary>
    class SettingsForm : Form
    {
        static readonly Color Bg = ColorTranslator.FromHtml("#1A222D");
        static readonly Color Panel = ColorTranslator.FromHtml("#2A3442");
        static readonly Color Parchment = ColorTranslator.FromHtml("#E5D5B5");
        static readonly Color Gold = ColorTranslator.FromHtml("#F2B83B");
        static readonly Color Muted = ColorTranslator.FromHtml("#AAB8C6");
        static readonly Color Green = ColorTranslator.FromHtml("#2F7A3E");

        readonly ComboBox _res = new ComboBox();
        readonly ComboBox _mode = new ComboBox();
        readonly List<Size> _sizes = Screens.All();

        public SettingsForm()
        {
            Text = "Settings";
            ClientSize = new Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Bg; ForeColor = Parchment;
            Font = new Font("Segoe UI", 10f);

            var title = new Label { Text = "SCREEN SETTINGS", ForeColor = Gold, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Bounds = new Rectangle(20, 14, 380, 32) };
            var native = Screens.Native();
            var cur = DisplaySettings.Load();

            var resLabel = new Label { Text = "Resolution", Bounds = new Rectangle(20, 60, 120, 24) };
            _res.SetBounds(150, 58, 250, 28);
            _res.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (var z in _sizes) _res.Items.Add($"{z.Width} x {z.Height}" + (z == native ? "  (your screen)" : ""));
            int idx = _sizes.FindIndex(z => z.Width == cur.Width && z.Height == cur.Height);
            _res.SelectedIndex = idx >= 0 ? idx : _sizes.FindIndex(z => z == native);

            var modeLabel = new Label { Text = "Display mode", Bounds = new Rectangle(20, 102, 120, 24) };
            _mode.SetBounds(150, 100, 250, 28);
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Items.AddRange(DisplaySettings.ModeNames);
            _mode.SelectedIndex = Math.Max(0, Array.IndexOf(DisplaySettings.Modes, cur.Mode));

            var hint = new Label
            {
                Text = "If the game doesn't fit your screen, click \"Match my screen\" and Save. You can also change this in the game's Settings button.",
                ForeColor = Muted, Bounds = new Rectangle(20, 140, 380, 60)
            };

            var match = new Button { Text = "Match my screen", Bounds = new Rectangle(20, 214, 150, 44) };
            LauncherForm.StyleButton(match, Panel, 10f);
            match.Click += (s, e) => { _res.SelectedIndex = _sizes.FindIndex(z => z == native); _mode.SelectedIndex = 0; };

            var save = new Button { Text = "Save", Bounds = new Rectangle(250, 214, 150, 44) };
            LauncherForm.StyleButton(save, Green, 11f);
            save.Click += (s, e) =>
            {
                var z = _sizes[Math.Max(0, _res.SelectedIndex)];
                var d = new DisplaySettings { Width = z.Width, Height = z.Height, Mode = DisplaySettings.Modes[Math.Max(0, _mode.SelectedIndex)] };
                try { d.Save(); } catch (Exception ex) { MessageBox.Show(this, "Couldn't save: " + ex.Message, "Settings"); return; }
                Close();
            };

            Controls.AddRange(new Control[] { title, resLabel, _res, modeLabel, _mode, hint, match, save });
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; // GitHub requires TLS 1.2
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LauncherForm());
        }
    }
}
