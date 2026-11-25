using System;
using System.Collections.Generic;
using Game.Core.Commands;

namespace Game.Core.Display
{
    /// <summary>Formatação de texto para a UI (pt-BR). Localização completa entra na Etapa 5.</summary>
    public static class DisplayFormat
    {
        private static readonly Dictionary<string, string> Errors = new Dictionary<string, string>
        {
            { CommandErrors.InsufficientCoins, "Moedas insuficientes" },
            { CommandErrors.InsufficientGems, "Gemas insuficientes" },
            { CommandErrors.InsufficientStock, "Estoque insuficiente" },
            { CommandErrors.Locked, "Ainda bloqueado" },
            { CommandErrors.PlotOccupied, "Parcela ocupada" },
            { CommandErrors.PlotEmpty, "Parcela vazia" },
            { CommandErrors.NotReady, "Ainda não está pronto" },
            { CommandErrors.AlreadyReady, "Já está pronto para colher" },
            { CommandErrors.AlreadyWatered, "Já foi regado" },
            { CommandErrors.MaxReached, "Limite atingido" },
            { CommandErrors.NoSeeds, "Sem sementes: compre na vendinha" },
            { CommandErrors.NoFeed, "Sem comida: colha trigo ou milho" },
            { CommandErrors.AlreadyFed, "Já está alimentado" },
            { CommandErrors.ShelfFull, "Prateleira cheia" },
            { CommandErrors.WrongCategory, "Esse produto não vai nesse móvel" },
            { CommandErrors.ShelfHasOtherProduct, "Prateleira com outro produto" },
            { CommandErrors.NoCustomer, "Ninguém na fila" },
        };

        /// <summary>Texto amigável para um código de erro de comando.</summary>
        public static string Error(string code) =>
            code != null && Errors.TryGetValue(code, out var text) ? text : "Não foi possível";

        /// <summary>Duração curta: "45 s", "3 min", "1 min 30 s", "2 h 5 min".</summary>
        public static string Duration(long ms)
        {
            if (ms <= 0) return "0 s";
            long totalSeconds = (ms + 999) / 1000; // arredonda para cima: nunca mostra "0 s" faltando tempo
            long h = totalSeconds / 3600, m = totalSeconds % 3600 / 60, s = totalSeconds % 60;
            if (h > 0) return m > 0 ? $"{h} h {m} min" : $"{h} h";
            if (m > 0) return s > 0 ? $"{m} min {s} s" : $"{m} min";
            return $"{s} s";
        }

        /// <summary>Moedas com separador de milhar: 12.345. Manual (sem CultureInfo) para não depender
        /// dos dados de cultura no build IL2CPP.</summary>
        public static string Coins(long value)
        {
            string digits = value == long.MinValue ? "9223372036854775808" : Math.Abs(value).ToString();
            var sb = new System.Text.StringBuilder(digits.Length + digits.Length / 3 + 1);
            if (value < 0) sb.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }

        /// <summary>Cor estável (r, g, b em 0..1) para um ID, usada no gray box.</summary>
        public static (float r, float g, float b) ColorFor(string id)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char ch in id ?? "") hash = (hash ^ ch) * 16777619;
                float hue = (hash % 360) / 360f;
                return HsvToRgb(hue, 0.65f, 0.85f);
            }
        }

        public static (float r, float g, float b) HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float hp = h * 6f;
            float x = c * (1 - Math.Abs(hp % 2 - 1));
            float r = 0, g = 0, b = 0;
            if (hp < 1) { r = c; g = x; }
            else if (hp < 2) { r = x; g = c; }
            else if (hp < 3) { g = c; b = x; }
            else if (hp < 4) { g = x; b = c; }
            else if (hp < 5) { r = x; b = c; }
            else { r = c; b = x; }
            float mm = v - c;
            return (r + mm, g + mm, b + mm);
        }
    }
}
