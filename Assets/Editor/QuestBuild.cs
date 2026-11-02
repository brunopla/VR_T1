using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Compila la APK para Meta Quest con las escenas activas de Build Settings.
// Batch: Unity -batchmode -quit -buildTarget Android -executeMethod QuestBuild.BuildApk
public static class QuestBuild
{
    public const string Output = "Builds/Quest/devaZtation.apk";

    [MenuItem("VR_T1/Build APK para Quest")]
    public static void BuildApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(Output));

        // Firma de depuracion: user.keystore ya no se versiona y sus claves no
        // estan en el proyecto. Para publicar, configurar la clave propia.
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        PlayerSettings.Android.useCustomKeystore = false;
        try
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            });
            var summary = report.summary;
            Debug.Log("[QuestBuild] " + summary.result + ": " + Output + " (" + (summary.totalSize / (1024 * 1024)) +
                      " MB, " + summary.totalTime.ToString(@"mm\:ss") + ", errores: " + summary.totalErrors + ")");
            if (Application.isBatchMode && summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = customKeystore;
        }
    }
}
