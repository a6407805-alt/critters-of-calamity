using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CrittersOfCalamity.Visuals;

namespace ProjectBootstrap
{
    public static class VisualBaselineSetup
    {
        public static void Apply()
        {
            // 1. Quality tier: highest tier for desktop
            var names = QualitySettings.names;
            if (names != null && names.Length > 0)
            {
                QualitySettings.SetQualityLevel(names.Length - 1, true);
                Debug.Log($"[VisualBaselineSetup] Set quality tier to '{names[names.Length - 1]}' (index {names.Length - 1}).");
            }

            // 2. Ensure URP Asset supports HDR
            var urpAsset = UniversalRenderPipeline.asset;
            if (urpAsset != null)
            {
                urpAsset.supportsHDR = true;
                urpAsset.shadowDistance = 60f;
                EditorUtility.SetDirty(urpAsset);
            }

            // 3. Configure Key Light & Gradient Ambient Lighting (craft-game-floors/scene-lighting)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSkyCool;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;

            var lights = Object.FindObjectsByType<Light>();
            Light dirLight = null;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    dirLight = l;
                    break;
                }
            }
            if (dirLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.color = Palette.KeyLightWarm;
            dirLight.intensity = 1.2f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            // 4. Configure Camera & Post-Processing
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
            }
            cam.orthographic = false;
            cam.fieldOfView = 45f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.ShadowSlate;
            cam.transform.position = new Vector3(0f, 14f, -12f);
            cam.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

            if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var camData))
            {
                camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            camData.renderPostProcessing = true;

            // 5. Configure Global Volume (ACES Tonemapping, Bloom, Vignette, Color Adjustments)
            Volume globalVol = null;
            var volumes = Object.FindObjectsByType<Volume>();
            foreach (var v in volumes)
            {
                if (v.isGlobal)
                {
                    globalVol = v;
                    break;
                }
            }
            if (globalVol == null)
            {
                var volGo = new GameObject("Global Volume");
                globalVol = volGo.AddComponent<Volume>();
                globalVol.isGlobal = true;
            }

            var profile = globalVol.sharedProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                    AssetDatabase.CreateFolder("Assets", "Settings");
                AssetDatabase.CreateAsset(profile, "Assets/Settings/TabletopGlobalVolumeProfile.asset");
                globalVol.sharedProfile = profile;
            }

            if (!profile.TryGet<Tonemapping>(out var tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            if (!profile.TryGet<Bloom>(out var bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.1f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;

            if (!profile.TryGet<Vignette>(out var vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.22f;

            if (!profile.TryGet<ColorAdjustments>(out var colorAdj))
                colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.saturation.overrideState = true;
            colorAdj.saturation.value = 8f;
            colorAdj.contrast.overrideState = true;
            colorAdj.contrast.value = 5f;

            // 6. Ensure CrittersOfCalamity_GameController exists in SampleScene so it runs immediately in Editor and Standalone Builds
            var existingCtrl = Object.FindObjectsByType<CrittersOfCalamity.UI.CrittersTabletopHUD>();
            GameObject ctrlGo;
            if (existingCtrl != null && existingCtrl.Length > 0)
            {
                ctrlGo = existingCtrl[0].gameObject;
            }
            else
            {
                ctrlGo = new GameObject("CrittersOfCalamity_GameController");
                ctrlGo.AddComponent<CrittersOfCalamity.Multiplayer.CrittersMultiplayerSessionManager>();
                ctrlGo.AddComponent<CrittersOfCalamity.Visuals.Tabletop3DBoardRenderer>();
                ctrlGo.AddComponent<CrittersOfCalamity.UI.CrittersTabletopHUD>();
            }
            EditorUtility.SetDirty(ctrlGo);

            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[VisualBaselineSetup] URP visual baseline applied and saved.");
            EditorApplication.Exit(0);
        }
    }
}
