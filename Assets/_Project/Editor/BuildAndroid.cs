using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Builds Android por linha de comando. Ex.:
    /// Unity -batchmode -quit -buildTarget Android -executeMethod Game.Editor.BuildAndroid.BuildDebugApk -outputPath Builds/Android/app-debug.apk
    /// </summary>
    public static class BuildAndroid
    {
        public const string DefaultDebugApk = "Builds/Android/FazendaMercadinho-debug.apk";

        [MenuItem("Fazenda/Build/Android APK debug")]
        public static void BuildDebugApkMenu() => BuildDebugApkTo(DefaultDebugApk);

        public static void BuildDebugApk()
        {
            int code = 1;
            try
            {
                // -outputPath (Tools/*.ps1) ou -customBuildPath (GameCI na nuvem).
                code = BuildDebugApkTo(GetArg("-outputPath") ?? GetArg("-customBuildPath") ?? DefaultDebugApk) ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }

        private static bool BuildDebugApkTo(string outputPath)
        {
            ProjectSetup.Apply();
            if (ProjectSetup.InputHandlerChangedThisSession)
            {
                Debug.LogError("[BuildAndroid] O Input System acabou de ser ativado: rode a configuração e reinicie o Unity " +
                               "(ou rode setup-project.ps1) antes de buildar.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Nenhuma cena na Build Settings.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[BuildAndroid] Resultado: {s.result}, erros: {s.totalErrors}, tamanho: {s.totalSize / (1024f * 1024f):F1} MB, saída: {s.outputPath}");
            return s.result == BuildResult.Succeeded;
        }

        public static string GetArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
