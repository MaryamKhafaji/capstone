using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class MasarAndroidExport
{
    public static void ExportFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int output = Array.IndexOf(args, "-masarExportPath");
        if (output < 0 || output + 1 >= args.Length)
            throw new ArgumentException("Pass -masarExportPath with a Gradle export directory.");

        string destination = Path.GetFullPath(args[output + 1]);
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            target = BuildTarget.Android,
            locationPathName = destination,
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Android export failed: " + report.summary.result);
    }
}
