using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests.Core
{
    /// <summary>
    /// Garante a regra de arquitetura nº 1: Game.Core não depende de UnityEngine/UnityEditor.
    /// C# puro, roda no Unity e no dotnet.
    /// </summary>
    public class ArchitectureTests
    {
        private static string FindProjectRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Core")))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("Raiz do projeto Unity não encontrada.");
            return null;
        }

        private static string CoreDir => Path.Combine(FindProjectRoot(), "Assets", "_Project", "Core");

        [Test]
        public void CoreAsmdef_HasNoEngineReferencesAndNoDependencies()
        {
            string json = File.ReadAllText(Path.Combine(CoreDir, "Game.Core.asmdef"));
            StringAssert.IsMatch("\"noEngineReferences\"\\s*:\\s*true", json);
            StringAssert.IsMatch("\"references\"\\s*:\\s*\\[\\s*\\]", json);
            // Única DLL externa permitida no Core: Newtonsoft (save).
            StringAssert.IsMatch("\"precompiledReferences\"\\s*:\\s*\\[\\s*\"Newtonsoft\\.Json\\.dll\"\\s*\\]", json);
        }

        // Pastas que precisam ser C# puro (também compiladas fora do Unity em Tools/CoreTests).
        [TestCase("Core")]
        [TestCase("Services/FileSystem")]
        public void PureSources_DoNotMentionUnityNamespaces(string folder)
        {
            var forbidden = new Regex(@"\bUnity(Engine|Editor)\b");
            string dir = Path.Combine(FindProjectRoot(), "Assets", "_Project", folder);
            string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            foreach (string file in files)
            {
                Assert.IsFalse(forbidden.IsMatch(File.ReadAllText(file)),
                    $"{Path.GetFileName(file)} referencia UnityEngine/UnityEditor; {folder} deve ser C# puro.");
            }
        }
    }
}
