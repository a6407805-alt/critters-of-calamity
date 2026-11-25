using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CrittersOfCalamity.EditorTools
{
    /// <summary>
    /// Keeps the in-game rulebook (Assets/Resources/Rulebook/CoreRulebook.txt) identical to
    /// Docs/Critters_of_Calamity_Core_Rulebook_Eight_Factions.txt. Runs when the editor loads scripts
    /// and before every build, so editing the Docs file is all that's needed.
    /// </summary>
    [InitializeOnLoad]
    public static class RulebookSync
    {
        const string DocsFile = "Docs/Critters_of_Calamity_Core_Rulebook_Eight_Factions.txt";
        const string ResourceFile = "Assets/Resources/Rulebook/CoreRulebook.txt";

        static RulebookSync()
        {
            EditorApplication.delayCall += () => Sync(false);
        }

        [MenuItem("Critters/Sync Rulebook Into Game")]
        static void SyncFromMenu() => Sync(true);

        public static void Sync(bool log)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string src = Path.Combine(root, DocsFile);
            string dst = Path.Combine(root, ResourceFile);
            if (!File.Exists(src)) return;

            string text = File.ReadAllText(src);
            if (File.Exists(dst) && File.ReadAllText(dst) == text)
            {
                if (log) Debug.Log("[RulebookSync] In-game rulebook is already up to date.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.WriteAllText(dst, text);
            AssetDatabase.ImportAsset(ResourceFile);
            Debug.Log("[RulebookSync] Copied the Docs rulebook into the game.");
        }

        class BeforeBuild : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;
            public void OnPreprocessBuild(BuildReport report) => Sync(true);
        }
    }
}
