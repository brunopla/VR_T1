using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

// Ajustes de rendimiento para Meta Quest (Android). Se puede repetir sin problema.
// Batch: Unity -batchmode -quit -executeMethod QuestOptimizer.Run
public static class QuestOptimizer
{
    const int MaxTextureSize = 2048;
    const string LevelScene = "Assets/Scenes/level_00.unity";
    static readonly string[] StaticScenes = { "Assets/Scenes/Prototipable.unity", LevelScene };

    static StringBuilder log;

    [MenuItem("VR_T1/Optimizar para Quest")]
    public static void Run()
    {
        log = new StringBuilder();
        ConfigurePlayer();
        ConfigureOpenXR();
        ConfigureQuality();
        ConfigureImporters();
        foreach (var scene in StaticScenes)
            MarkStatic(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[QuestOptimizer] Terminado\n" + log);
    }

    [MenuItem("VR_T1/Calcular occlusion culling (level_00)")]
    public static void BakeOcclusion()
    {
        var scene = EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);
        StaticOcclusionCulling.Compute();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[QuestOptimizer] Occlusion culling calculado para " + LevelScene);
    }

    static void ConfigurePlayer()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        // OpenXR en Quest exige Android 10 (API 29) como minimo
        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
        PlayerSettings.SetMobileMTRendering(android, true);
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        log.AppendLine("Android: IL2CPP, ARM64, API minima 29, Vulkan, render multihilo, ASTC");
    }

    static void ConfigureOpenXR()
    {
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        if (settings == null)
        {
            log.AppendLine("AVISO: no hay ajustes de OpenXR para Android");
            return;
        }

        settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        SetFeature<MetaQuestFeature>(settings, true);
        SetFeature<OculusTouchControllerProfile>(settings, true);
#pragma warning disable 618 // Se apaga la funcion antigua, reemplazada por MetaQuestFeature
        SetFeature<UnityEngine.XR.OpenXR.Features.OculusQuestSupport.OculusQuestFeature>(settings, false);
#pragma warning restore 618
        EditorUtility.SetDirty(settings);
        log.AppendLine("OpenXR Android: Single Pass Instanced, Meta Quest Support y Oculus Touch activos");
    }

    static void SetFeature<T>(OpenXRSettings settings, bool enabled) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
    {
        var feature = settings.GetFeature<T>();
        if (feature == null)
            return;
        feature.enabled = enabled;
        EditorUtility.SetDirty(feature);
    }

    // Ajusta el nivel de calidad que usa Android por defecto
    static void ConfigureQuality()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var so = new SerializedObject(asset);
        int level = -1;
        var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
        for (int i = 0; i < perPlatform.arraySize; i++)
        {
            var pair = perPlatform.GetArrayElementAtIndex(i);
            if (pair.FindPropertyRelative("first").stringValue == "Android")
                level = pair.FindPropertyRelative("second").intValue;
        }

        var levels = so.FindProperty("m_QualitySettings");
        if (level < 0 || level >= levels.arraySize)
        {
            log.AppendLine("AVISO: no se encontro el nivel de calidad de Android");
            return;
        }

        var q = levels.GetArrayElementAtIndex(level);
        q.FindPropertyRelative("pixelLightCount").intValue = 1;
        q.FindPropertyRelative("shadows").intValue = 1;            // Solo sombras duras
        q.FindPropertyRelative("shadowResolution").intValue = 1;   // Media
        q.FindPropertyRelative("shadowCascades").intValue = 1;
        q.FindPropertyRelative("shadowDistance").floatValue = 25f;
        q.FindPropertyRelative("antiAliasing").intValue = 4;
        q.FindPropertyRelative("anisotropicTextures").intValue = 0;
        q.FindPropertyRelative("softParticles").boolValue = false;
        q.FindPropertyRelative("realtimeReflectionProbes").boolValue = false;
        q.FindPropertyRelative("lodBias").floatValue = 1f;
        so.ApplyModifiedProperties();
        log.AppendLine("Calidad Android (nivel " + level + "): 1 luz por pixel, sombras duras a 25 m, MSAA 4x");
    }

    static void ConfigureImporters()
    {
        int textures = 0, models = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                    continue;

                var android = importer.GetPlatformTextureSettings("Android");
                int size = Mathf.Min(importer.maxTextureSize, MaxTextureSize);
                if (android.overridden && android.maxTextureSize <= size)
                    continue;

                android.overridden = true;
                android.maxTextureSize = size;
                android.format = TextureImporterFormat.Automatic;
                android.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(android);
                importer.SaveAndReimport();
                textures++;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer) || importer.meshCompression != ModelImporterMeshCompression.Off)
                    continue;

                importer.meshCompression = ModelImporterMeshCompression.Low;
                importer.optimizeMeshPolygons = true;
                importer.optimizeMeshVertices = true;
                importer.SaveAndReimport();
                models++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        log.AppendLine("Texturas con limite Android de " + MaxTextureSize + ": " + textures + "; modelos con compresion de malla: " + models);
    }

    // Marca como estatico el escenario que no se mueve, para batching y occlusion culling
    static void MarkStatic(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = renderer.gameObject;
                if (!IsSceneryChain(go.transform))
                    continue;

                var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                if (renderer.bounds.size.magnitude > 3f)
                    flags |= StaticEditorFlags.OccluderStatic;
                var current = GameObjectUtility.GetStaticEditorFlags(go);
                if ((current & flags) == flags)
                    continue;
                GameObjectUtility.SetStaticEditorFlags(go, current | flags);
                count++;
            }
        }
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Objetos estaticos en " + scenePath + ": " + count);
    }

    // El objeto y todos sus padres solo tienen componentes de escenario (nada que se mueva)
    static bool IsSceneryChain(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null)
                    return false;
                if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider || c is LODGroup))
                    return false;
            }
        }
        return true;
    }
}
