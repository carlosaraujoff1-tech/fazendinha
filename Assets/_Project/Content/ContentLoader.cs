using Game.Core.Catalog;
using UnityEngine;

namespace Game.Content
{
    /// <summary>Converte os dados de conteúdo do projeto (JSON em TextAsset) no catálogo do Core.</summary>
    public static class ContentLoader
    {
        public static GameCatalog LoadCatalog(TextAsset catalogJson)
        {
            if (catalogJson == null) throw new CatalogException(new[] { "TextAsset do catálogo não atribuído." });
            return CatalogJson.Parse(catalogJson.text);
        }
    }
}
