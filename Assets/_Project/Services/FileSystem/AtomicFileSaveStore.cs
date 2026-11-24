using System;
using System.IO;
using System.Text;
using Game.Core.Save;

namespace Game.Services.FileSystem
{
    /// <summary>
    /// Save em disco com gravação atômica: escreve em "save.json.tmp", força para o disco e só então troca
    /// pelo principal; o principal anterior vira "save.json.bak". Se o app morrer no meio, sobra no máximo
    /// um .tmp órfão e o principal (ou o backup) continua inteiro.
    /// C# puro (só System.IO), para rodar também nos testes fora do Unity.
    /// </summary>
    public sealed class AtomicFileSaveStore : ISaveStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public AtomicFileSaveStore(string directory, string fileName = "save.json")
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Diretório vazio.", nameof(directory));
            Directory.CreateDirectory(directory);
            PrimaryPath = Path.Combine(directory, fileName);
            BackupPath = PrimaryPath + ".bak";
            TempPath = PrimaryPath + ".tmp";
        }

        public string PrimaryPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }

        public string ReadPrimary() => ReadIfExists(PrimaryPath);
        public string ReadBackup() => ReadIfExists(BackupPath);

        public void WriteAtomic(string content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            byte[] bytes = Utf8NoBom.GetBytes(content);
            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            if (!File.Exists(PrimaryPath))
            {
                File.Move(TempPath, PrimaryPath);
                return;
            }

            try
            {
                File.Replace(TempPath, PrimaryPath, BackupPath, true);
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is IOException || e is UnauthorizedAccessException)
            {
                // Alguns sistemas de arquivos (ex.: certos Android) não suportam Replace: troca manual.
                File.Copy(PrimaryPath, BackupPath, true);
                File.Delete(PrimaryPath);
                File.Move(TempPath, PrimaryPath);
            }
        }

        private static string ReadIfExists(string path)
        {
            if (!File.Exists(path)) return null;
            try { return File.ReadAllText(path, Encoding.UTF8); }
            catch (IOException) { return null; }
        }
    }
}
