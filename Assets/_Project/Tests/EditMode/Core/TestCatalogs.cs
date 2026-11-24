using Game.Core.Catalog;

namespace Game.Tests.Core
{
    /// <summary>Catálogo do MVP (tabela da seção 1 do PROMPT_MESTRE) para os testes.</summary>
    public static class TestCatalogs
    {
        public const int OneHour = 3600;

        public static GameCatalog Mvp(EconomyConfig economy = null) => GameCatalog.Build(
            new[]
            {
                new ProductDef("alface", "hortifruti", 10, 6 * OneHour),
                new ProductDef("cenoura", "hortifruti", 14, 12 * OneHour),
                new ProductDef("batata", "hortifruti", 18, 24 * OneHour),
                new ProductDef("tomate", "hortifruti", 24, 8 * OneHour),
                new ProductDef("milho", "hortifruti", 30, 12 * OneHour),
                new ProductDef("arroz", "mercearia", 20, 0),
            },
            new[]
            {
                new CropDef("alface", "alface", 60, 10, 4),
                new CropDef("cenoura", "cenoura", 180, 18, 5),
                new CropDef("batata", "batata", 480, 27, 6),
                new CropDef("tomate", "tomate", 900, 36, 6),
                new CropDef("milho", "milho", 1800, 60, 8, unlockLevel: 5),
            },
            economy);
    }
}
