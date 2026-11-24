using System.IO;
using UnityEngine;

namespace Game.Services
{
    public static class UnitySavePaths
    {
        /// <summary>Pasta privada do app (Android: /data/data/&lt;pacote&gt;/files/saves).</summary>
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
    }
}
