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
    const string WebOutputPath = "D:/Lag Fighters/Builds/LagFightersWeb";

    [MenuItem("Lag Fighters/Build para compartir")]
    public static void BuildFromMenu()
    {
        var report = DoBuild();
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(OutputPath);
    }

    [MenuItem("Lag Fighters/Build WebGL (itch.io)")]
    public static void BuildWebGLFromMenu()
    {
        var report = DoBuildWebGL();
        if (report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(WebOutputPath + "/index.html");
    }

    public static void Build() // entrada batchmode
    {
        var report = DoBuild();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void BuildWebGL() // entrada batchmode
    {
        var report = DoBuildWebGL();
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // WebGL para itch.io: gzip + fallback de descompresión (funciona sin
    // headers especiales del server). Subir la carpeta entera zipeada.
    static BuildReport DoBuildWebGL()
    {
        PlayerSettings.companyName = "Patricio";
        PlayerSettings.productName = "Lag Fighters";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.runInBackground = true;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = WebOutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BuildScript WebGL: {report.summary.result} · {report.summary.totalSize / (1024 * 1024)} MB · {report.summary.totalTime}");
        if (report.summary.result == BuildResult.Succeeded)
            PatchWebIndex();
        return report;
    }

    // devicePixelRatio fijo en 1: en pantallas retina el canvas renderizaba
    // 4x los píxeles (lento) y el Input System recibía los clicks en px CSS
    // mientras la UI mide en px del buffer (clicks corridos).
    static void PatchWebIndex()
    {
        string index = WebOutputPath + "/index.html";
        if (!System.IO.File.Exists(index)) { Debug.LogWarning("BuildScript: no encontré index.html para parchear"); return; }
        string html = System.IO.File.ReadAllText(index);
        // el template trae la línea comentada: descomentarla
        string patched = html.Replace("// config.devicePixelRatio = 1;", "config.devicePixelRatio = 1;");
        if (patched == html)
            patched = html.Replace("var config = {",
                "var config = {\n        devicePixelRatio: 1, // 1:1 con CSS: menos fill-rate y clicks alineados");
        if (patched == html) { Debug.LogWarning("BuildScript: el template cambió, no pude inyectar devicePixelRatio"); return; }
        System.IO.File.WriteAllText(index, patched);
        Debug.Log("BuildScript: index.html parcheado con devicePixelRatio = 1");
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
