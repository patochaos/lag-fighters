using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Dos formas de buildear:
//  - Desde el editor: menú "Lag Fighters → Build para compartir".
//  - Por línea de comandos (editor cerrado):
//    Unity.exe -batchmode -projectPath "..." -executeMethod BuildScript.Build -logFile build.log
public static class BuildScript
{
    const string OutputPath = "D:/Lag Fighters/Builds/LagFighters/LagFighters.exe";

    [MenuItem("Lag Fighters/Build para compartir")]
    public static void BuildFromMenu()
    {
        var report = DoBuild();
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(OutputPath);
    }

    public static void Build() // entrada batchmode
    {
        var report = DoBuild();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static BuildReport DoBuild()
    {
        PlayerSettings.companyName = "Patricio";
        PlayerSettings.productName = "Lag Fighters";

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BuildScript: {report.summary.result} · {report.summary.totalSize / (1024 * 1024)} MB · {report.summary.totalTime}");
        return report;
    }
}
