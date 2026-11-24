using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Build Linux (Mono, rápido) usado só no CI para rodar o jogo sob xvfb e tirar prints automáticos
    /// (ScreenshotDirector). Ex.: -executeMethod Game.Editor.BuildScreenshots.BuildLinux -customBuildPath build/x/game
    /// </summary>
    public static class BuildScreenshots
    {
        public static void BuildLinux()
        {
            int code = 1;
            try
            {
                string output = BuildAndroid.GetArg("-customBuildPath") ?? "Builds/Linux/fazenda.x86_64";
                if (!output.EndsWith(".x86_64")) output += ".x86_64";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

                var standalone = NamedBuildTarget.Standalone;
                PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                PlayerSettings.resizableWindow = false;
                PlayerSettings.runInBackground = true;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore });

                var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.StandaloneLinux64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                });
                Debug.Log($"[BuildScreenshots] {report.summary.result}, erros {report.summary.totalErrors}, saída {output}");
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            EditorApplication.Exit(code);
        }
    }
}
