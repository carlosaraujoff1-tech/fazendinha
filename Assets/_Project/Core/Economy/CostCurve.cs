using System;

namespace Game.Core.Economy
{
    /// <summary>
    /// Curva de custo para compras repetíveis (parcela nova, prateleira, nível de funcionário):
    /// custo(n) = custo_base × crescimento^n, arredondado para o inteiro mais próximo.
    /// O crescimento padrão (1,15) virá do catálogo de dados a partir da Etapa 1.
    /// </summary>
    public static class CostCurve
    {
        public const double DefaultGrowth = 1.15;

        /// <param name="baseCost">Custo da primeira compra (n = 0). Deve ser &gt;= 0.</param>
        /// <param name="purchasesSoFar">Quantas unidades já foram compradas (n). Deve ser &gt;= 0.</param>
        /// <param name="growth">Fator de crescimento por compra. Deve ser &gt;= 1.</param>
        public static long RepeatableCost(long baseCost, int purchasesSoFar, double growth = DefaultGrowth)
        {
            if (baseCost < 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            if (purchasesSoFar < 0) throw new ArgumentOutOfRangeException(nameof(purchasesSoFar));
            if (growth < 1.0) throw new ArgumentOutOfRangeException(nameof(growth));

            double cost = baseCost * Math.Pow(growth, purchasesSoFar);
            if (cost >= long.MaxValue) return long.MaxValue;
            return (long)Math.Round(cost, MidpointRounding.AwayFromZero);
        }
    }
}
