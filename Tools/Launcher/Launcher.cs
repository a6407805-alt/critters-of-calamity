// Critters of Calamity Launcher
// A small Windows app (.NET Framework 4.8, built into Windows 10/11) that keeps the game up to date:
//   1. asks GitHub for the latest release of the public releases repo,
//   2. downloads and installs the game zip when there is a newer version,
//   3. launches the game.
// It also updates itself when a release carries a newer Launcher.exe.
// Build: see Tools/Launcher/build.sh (compiles with mono's C# compiler; no Visual Studio needed).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Critters of Calamity Launcher")]
[assembly: AssemblyProduct("Critters of Calamity")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace CrittersOfCalamity.Launcher
{
    static class Config
    {
        // The public repository whose GitHub Releases hold the game builds.
        public const string Owner = "a6407805-alt";
        public const string Repo = "critters-of-calamity-releases";
        public const string GameAsset = "CrittersOfCalamity-win64.zip";
        public const string LauncherAsset = "Launcher.exe";
        public const string GameExe = "Critters of Calamity.exe";
        public static readonly Version LauncherVersion = new Version(1, 0, 0);

        public static string InstallRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrittersOfCalamity");
        public static string GameDir => Path.Combine(InstallRoot, "Game");
        public static string VersionFile => Path.Combine(GameDir, "version.txt");
    }

    class ReleaseInfo
    {
        public string Tag;            // e.g. v0.2.0
        public string Notes;
        public string GameUrl;
        public long GameSize;
        public string LauncherUrl;
        public Version LauncherVersion; // from the release body line "launcher: 1.0.1", if present
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

            _check.SetBounds(20, 326, 200, 52);
            StyleButton(_check, Panel, 11f);
            _check.Text = "Check for updates";
            _check.Click += async (s, e) => await CheckForUpdates();

            Controls.AddRange(new Control[] { title, _versions, _notes, _status, _progress, _play, _check });
            Shown += async (s, e) => await CheckForUpdates();
        }

        static void StyleButton(Button b, Color bg, float size)
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
            string url = $"https://api.github.com/repos/{Config.Owner}/{Config.Repo}/releases/latest";
            string json;
            using (var wc = NewClient())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                json = wc.DownloadString(url);
            }
            var data = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            var info = new ReleaseInfo { Tag = data["tag_name"] as string, Notes = data.ContainsKey("body") ? data["body"] as string : "" };
            if (data.ContainsKey("assets") && data["assets"] is object[] assets)
                foreach (Dictionary<string, object> a in assets)
                {
                    string name = a["name"] as string;
                    if (name == Config.GameAsset) { info.GameUrl = a["browser_download_url"] as string; info.GameSize = Convert.ToInt64(a["size"]); }
                    if (name == Config.LauncherAsset) info.LauncherUrl = a["browser_download_url"] as string;
                }
            foreach (var line in (info.Notes ?? "").Split('\n'))
            {
                var l = line.Trim();
                if (l.StartsWith("launcher:", StringComparison.OrdinalIgnoreCase) && Version.TryParse(l.Substring(9).Trim(), out var v))
                    info.LauncherVersion = v;
            }
            return info;
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
                    await wc.DownloadFileTaskAsync(new Uri(_latest.GameUrl), tmpZip);
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
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Config.GameDir, UseShellExecute = true });
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
                using (var wc = NewClient()) await wc.DownloadFileTaskAsync(new Uri(_latest.LauncherUrl), fresh);
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
