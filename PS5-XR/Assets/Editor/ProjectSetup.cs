using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARSubsystems;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;

namespace Ps5Maintenance.Editor
{
    [InitializeOnLoad]
    public static class FirstOpen
    {
        static FirstOpen()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Assets/Scenes/Maintenance.unity") && !EditorApplication.isPlayingOrWillChangePlaymode)
                    ProjectSetup.CreateProject();
            };
        }
    }

    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Maintenance.unity";

        [MenuItem("PS5 XR/Создать или обновить сцену")]
        public static void CreateProject()
        {
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            ConfigureAndroidXr();
            TypographySetup.PrepareFont();
            const string overlayMaterialPath = "Assets/Resources/OverlayMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(overlayMaterialPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Standard")), overlayMaterialPath);
            PlayerSettings.companyName = "StudentProject";
            PlayerSettings.productName = "PS5 XR Maintenance";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "ru.student.ps5xr");
            PlayerSettings.defaultScreenWidth = 1100;
            PlayerSettings.defaultScreenHeight = 1500;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.stripEngineCode = false;
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 1; settings.ApplyModifiedPropertiesWithoutUndo(); }

            const string libraryPath = "Assets/Resources/MarkerLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
                library.Add();
                library.SetName(0, "ps5-maintenance");
                library.SetTexture(0, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ps5-marker.png"), false);
                library.SetSpecifySize(0, true);
                library.SetSize(0, new Vector2(.12f, .12f));
                EditorUtility.SetDirty(library);
            }
            const string layoutPath = "Assets/Resources/EquipmentLayout.asset";
            var layout = AssetDatabase.LoadAssetAtPath<EquipmentLayout>(layoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<EquipmentLayout>();
                layout.targets = InstructionData.Load().steps.Select(step => step.target).ToArray();
                AssetDatabase.CreateAsset(layout, layoutPath);
            }
            foreach (string java in Directory.GetFiles("Assets/Plugins/Android", "*.java"))
            {
                if (AssetImporter.GetAtPath(java.Replace('\\', '/')) is PluginImporter plugin)
                {
                    plugin.SetCompatibleWithAnyPlatform(false);
                    plugin.SetCompatibleWithEditor(false);
                    plugin.SetCompatibleWithPlatform(BuildTarget.Android, true);
                    plugin.SaveAndReimport();
                }
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var session = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
            session.SetActive(false);
            var origin = new GameObject("XR Origin", typeof(XROrigin));
            var offset = new GameObject("Camera Offset"); offset.transform.SetParent(origin.transform, false);
            var cameraObj = new GameObject("AR Camera", typeof(Camera), typeof(AudioListener), typeof(ARCameraManager), typeof(ARCameraBackground));
            cameraObj.tag = "MainCamera";
            cameraObj.transform.SetParent(offset.transform, false);
            var camera = cameraObj.GetComponent<Camera>(); camera.nearClipPlane = .01f; camera.farClipPlane = 30;
            cameraObj.GetComponent<ARCameraManager>().enabled = false;
            cameraObj.GetComponent<ARCameraBackground>().enabled = false;
            var xrOrigin = origin.GetComponent<XROrigin>(); xrOrigin.Camera = camera; xrOrigin.CameraFloorOffsetObject = offset;
            xrOrigin.CameraYOffset = 0;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            var pose = cameraObj.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(new InputAction("AR position", binding: "<HandheldARInputDevice>/devicePosition"));
            pose.rotationInput = new InputActionProperty(new InputAction("AR rotation", binding: "<HandheldARInputDevice>/deviceRotation"));
            pose.ignoreTrackingState = true;
            var manager = origin.AddComponent<ARTrackedImageManager>();
            manager.referenceLibrary = library;
            manager.enabled = false;
            var anchor = new GameObject("Marker-relative instruction");
            var app = new GameObject("Maintenance application").AddComponent<MaintenanceApp>();
            app.arSession = session.GetComponent<ARSession>(); app.imageManager = manager;
            app.sceneCamera = camera; app.contentAnchor = anchor.transform; app.layout = layout;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            VerifyLogic();
            Debug.Log("PS5_XR_PROJECT_CREATED");
        }

        [MenuItem("PS5 XR/Проверить логику инструкции")]
        public static void VerifyLogic()
        {
            var data = InstructionData.Load();
            Require(data.steps.Length == 10, "Expected 10 steps");
            Require(data.steps.Select(step => step.id).Distinct().Count() == data.steps.Length, "Duplicate step IDs");
            var flow = new MaintenanceSession(data.steps.Length);
            Require(!flow.Advance(false, true, true), "Unconfirmed advance");
            Require(!flow.Advance(true, false, true), "Marker-loss advance");
            Require(!flow.Advance(true, true, false), "Unknown model advance");
            Require(!flow.Back(), "Back before first step");
            Require(flow.Advance(true, true, true) && flow.Index == 1, "First advance");
            Require(flow.Back() && flow.Index == 0, "Back transition");
            for (int i = 0; i < data.steps.Length; i++) Require(flow.Advance(true, true, true), "Advance failed");
            Require(flow.Finished && flow.Completed.All(value => value), "Incomplete final state");
            Require(!flow.Advance(true, true, true) && !flow.Back(), "Mutation after completion");
            var uncalibrated = ScriptableObject.CreateInstance<EquipmentLayout>();
            Require(!uncalibrated.CanShowRealAr(ConsoleModel.Original, data.steps.Length), "Uncalibrated real arrows visible");
            UnityEngine.Object.DestroyImmediate(uncalibrated);
            Debug.Log("PS5_XR_LOGIC_CHECKS_PASSED");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        private static void ConfigureAndroidXr()
        {
            Directory.CreateDirectory("Assets/XR");
            const string xrPath = "Assets/XR/XRGeneralSettings.asset";
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(xrPath);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, xrPath);
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var android = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            android.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(android.Manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
                throw new Exception("Could not assign ARCore loader");
            EditorUtility.SetDirty(perTarget);
            var coreSettings = UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();
            coreSettings.requirement = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            coreSettings.depth = UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;
            AssetDatabase.SaveAssets();
        }

        public static void BuildPreview()
        {
            CreateProject();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = "Builds/Windows/PS5-XR.exe", options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Preview build failed: " + report.summary.result);
            Debug.Log("PS5_XR_PREVIEW_BUILD_PASSED");
        }
    }
}
