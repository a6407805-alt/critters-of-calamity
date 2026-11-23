#Requires -Version 5.1
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Continue"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not (Test-Path (Join-Path $RepoRoot "ProjectSettings\ProjectVersion.txt"))) {
    $RepoRoot = "C:\Users\ahren\.gemini\antigravity\scratch\CrittersOfCalamity"
}

$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe"
$BuiltGameExe = Join-Path $RepoRoot "Builds\Windows\CrittersOfCalamity.exe"
$RulebookDocx = Join-Path $RepoRoot "Docs\Critters_of_Calamity_Rulebook.docx"
$GitHubUrl = "https://github.com/a6407805-alt/critters-of-calamity"

$form = New-Object System.Windows.Forms.Form
$form.Text = "Critters of Calamity - Official Launcher & GitHub Updater"
$form.Size = New-Object System.Drawing.Size(760, 520)
$form.StartPosition = "CenterScreen"
$form.BackColor = [System.Drawing.Color]::FromArgb(24, 30, 38)
$form.ForeColor = [System.Drawing.Color]::FromArgb(235, 222, 196)
$form.FormBorderStyle = "FixedDialog"
$form.MaximizeBox = $false

$fontTitle = New-Object System.Drawing.Font("Segoe UI", 20, [System.Drawing.FontStyle]::Bold)
$fontSub   = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Regular)
$fontBtn   = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
$fontLog   = New-Object System.Drawing.Font("Consolas", 9.5, [System.Drawing.FontStyle]::Regular)

$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = "CRITTERS OF CALAMITY"
$lblTitle.Font = $fontTitle
$lblTitle.ForeColor = [System.Drawing.Color]::FromArgb(242, 184, 59)
$lblTitle.Location = New-Object System.Drawing.Point(24, 18)
$lblTitle.Size = New-Object System.Drawing.Size(700, 38)
$form.Controls.Add($lblTitle)

$lblSub = New-Object System.Windows.Forms.Label
$lblSub.Text = "4-Player Asymmetrical 3D Hex Board Game  |  11 Factions  |  40 Relics  |  50 Resources  |  30 Authority to Win"
$lblSub.Font = $fontSub
$lblSub.ForeColor = [System.Drawing.Color]::FromArgb(190, 205, 220)
$lblSub.Location = New-Object System.Drawing.Point(26, 56)
$lblSub.Size = New-Object System.Drawing.Size(700, 24)
$form.Controls.Add($lblSub)

$txtLog = New-Object System.Windows.Forms.TextBox
$txtLog.Multiline = $true
$txtLog.ScrollBars = "Vertical"
$txtLog.ReadOnly = $true
$txtLog.Font = $fontLog
$txtLog.BackColor = [System.Drawing.Color]::FromArgb(15, 19, 24)
$txtLog.ForeColor = [System.Drawing.Color]::FromArgb(210, 225, 240)
$txtLog.Location = New-Object System.Drawing.Point(26, 92)
$txtLog.Size = New-Object System.Drawing.Size(690, 225)
$form.Controls.Add($txtLog)

function Append-LauncherLog([string]$msg) {
    $ts = (Get-Date).ToString("HH:mm:ss")
    $txtLog.AppendText("[$ts] $msg`r`n")
}

function Refresh-GitStatus {
    Push-Location $RepoRoot
    $branch = (git rev-parse --abbrev-ref HEAD 2>$null)
    $commit = (git log -1 --format="%h - %s (%cr)" 2>$null)
    Pop-Location
    Append-LauncherLog "Project Root : $RepoRoot"
    Append-LauncherLog "GitHub Repo  : $GitHubUrl (Branch: $branch)"
    Append-LauncherLog "Latest Commit: $commit"
    if (Test-Path $BuiltGameExe) {
        Append-LauncherLog "Standalone 3D Build READY at: $BuiltGameExe"
    } else {
        Append-LauncherLog "Standalone 3D Build not built yet (click 'Play 3D Game' to open in Unity or 'Build Standalone EXE')."
    }
}

# Row 1 Buttons
$btnUpdate = New-Object System.Windows.Forms.Button
$btnUpdate.Text = "1. Check & Pull Updates (GitHub)"
$btnUpdate.Font = $fontBtn
$btnUpdate.BackColor = [System.Drawing.Color]::FromArgb(46, 110, 145)
$btnUpdate.ForeColor = [System.Drawing.Color]::White
$btnUpdate.FlatStyle = "Flat"
$btnUpdate.Location = New-Object System.Drawing.Point(26, 332)
$btnUpdate.Size = New-Object System.Drawing.Size(220, 46)
$btnUpdate.Add_Click({
    Append-LauncherLog "Fetching latest updates from origin/main..."
    Push-Location $RepoRoot
    $out = (git pull --rebase origin main 2>&1 | Out-String)
    Pop-Location
    Append-LauncherLog $out.Trim()
    Refresh-GitStatus
})
$form.Controls.Add($btnUpdate)

$btnPlay = New-Object System.Windows.Forms.Button
$btnPlay.Text = "2. Launch 3D Board Game"
$btnPlay.Font = $fontBtn
$btnPlay.BackColor = [System.Drawing.Color]::FromArgb(62, 130, 68)
$btnPlay.ForeColor = [System.Drawing.Color]::White
$btnPlay.FlatStyle = "Flat"
$btnPlay.Location = New-Object System.Drawing.Point(260, 332)
$btnPlay.Size = New-Object System.Drawing.Size(220, 46)
$btnPlay.Add_Click({
    if (Test-Path $BuiltGameExe) {
        Append-LauncherLog "Launching Standalone 3D Build: $BuiltGameExe"
        Start-Process -FilePath $BuiltGameExe
    } elseif (Test-Path $UnityExe) {
        Append-LauncherLog "Opening Critters of Calamity in Unity 6000.6.8f1..."
        Start-Process -FilePath "unity" -ArgumentList "open `"$RepoRoot`""
    } else {
        Append-LauncherLog "Unity Editor not found at $UnityExe."
    }
})
$form.Controls.Add($btnPlay)

$btnOpenUnity = New-Object System.Windows.Forms.Button
$btnOpenUnity.Text = "3. Open in Unity Editor"
$btnOpenUnity.Font = $fontBtn
$btnOpenUnity.BackColor = [System.Drawing.Color]::FromArgb(140, 92, 42)
$btnOpenUnity.ForeColor = [System.Drawing.Color]::White
$btnOpenUnity.FlatStyle = "Flat"
$btnOpenUnity.Location = New-Object System.Drawing.Point(496, 332)
$btnOpenUnity.Size = New-Object System.Drawing.Size(220, 46)
$btnOpenUnity.Add_Click({
    Append-LauncherLog "Launching Unity Editor for $RepoRoot..."
    Start-Process -FilePath "unity" -ArgumentList "open `"$RepoRoot`""
})
$form.Controls.Add($btnOpenUnity)

# Row 2 Buttons: Invite Collaborator (Eben), Open Rulebook, Open GitHub
$lblCollab = New-Object System.Windows.Forms.Label
$lblCollab.Text = "Invite Friend (GitHub Username):"
$lblCollab.Font = $fontSub
$lblCollab.Location = New-Object System.Drawing.Point(26, 396)
$lblCollab.Size = New-Object System.Drawing.Size(200, 22)
$form.Controls.Add($lblCollab)

$txtCollabUser = New-Object System.Windows.Forms.TextBox
$txtCollabUser.Font = $fontSub
$txtCollabUser.Text = ""
$txtCollabUser.Location = New-Object System.Drawing.Point(26, 420)
$txtCollabUser.Size = New-Object System.Drawing.Size(145, 28)
$form.Controls.Add($txtCollabUser)

$btnInvite = New-Object System.Windows.Forms.Button
$btnInvite.Text = "Grant Push Access"
$btnInvite.Font = $fontSub
$btnInvite.BackColor = [System.Drawing.Color]::FromArgb(115, 66, 145)
$btnInvite.ForeColor = [System.Drawing.Color]::White
$btnInvite.FlatStyle = "Flat"
$btnInvite.Location = New-Object System.Drawing.Point(178, 418)
$btnInvite.Size = New-Object System.Drawing.Size(135, 30)
$btnInvite.Add_Click({
    $u = $txtCollabUser.Text.Trim()
    if ([string]::IsNullOrWhiteSpace($u)) {
        Append-LauncherLog "Enter Eben's GitHub username in the box first, then click 'Grant Push Access'."
        return
    }
    Append-LauncherLog "Inviting '$u' as a collaborator with write/push access to a6407805-alt/critters-of-calamity..."
    $res = (gh api --method PUT "repos/a6407805-alt/critters-of-calamity/collaborators/$u" -f permission=push 2>&1 | Out-String)
    Append-LauncherLog "GitHub API Response: $($res.Trim())"
})
$form.Controls.Add($btnInvite)

$btnSyncPush = New-Object System.Windows.Forms.Button
$btnSyncPush.Text = "Commit & Push Changes"
$btnSyncPush.Font = $fontSub
$btnSyncPush.BackColor = [System.Drawing.Color]::FromArgb(55, 85, 115)
$btnSyncPush.ForeColor = [System.Drawing.Color]::White
$btnSyncPush.FlatStyle = "Flat"
$btnSyncPush.Location = New-Object System.Drawing.Point(325, 412)
$btnSyncPush.Size = New-Object System.Drawing.Size(185, 38)
$btnSyncPush.Add_Click({
    Push-Location $RepoRoot
    Append-LauncherLog "Staging, committing, and syncing changes to GitHub..."
    git add -A
    $msg = "Launcher sync: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    git commit -m $msg 2>&1 | Out-Null
    $out = (git push origin main 2>&1 | Out-String)
    Pop-Location
    Append-LauncherLog $out.Trim()
    Refresh-GitStatus
})
$form.Controls.Add($btnSyncPush)

$btnRulebook = New-Object System.Windows.Forms.Button
$btnRulebook.Text = "Open Rulebook & Repo"
$btnRulebook.Font = $fontSub
$btnRulebook.BackColor = [System.Drawing.Color]::FromArgb(80, 75, 65)
$btnRulebook.ForeColor = [System.Drawing.Color]::White
$btnRulebook.FlatStyle = "Flat"
$btnRulebook.Location = New-Object System.Drawing.Point(522, 412)
$btnRulebook.Size = New-Object System.Drawing.Size(194, 38)
$btnRulebook.Add_Click({
    if (Test-Path $RulebookDocx) {
        Start-Process $RulebookDocx
    }
    Start-Process $GitHubUrl
})
$form.Controls.Add($btnRulebook)

Append-LauncherLog "Welcome to the Critters of Calamity Launcher!"
Refresh-GitStatus

[void]$form.ShowDialog()
