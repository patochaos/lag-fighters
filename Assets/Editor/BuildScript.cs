using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build por línea de comandos:
// Unity.exe -batchmode -projectPath "..." -executeMethod BuildScript.Build -logFile build.log
public static class BuildScript
{
    public static void Build()
    {
        PlayerSettings.companyName = "Patricio";
        PlayerSettings.productName = "Lag Fighters";

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = "D:/Lag Fighters/Builds/LagFighters/LagFighters.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BuildScript: {report.summary.result}, {report.summary.totalSize} bytes, {report.summary.totalTime}");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
