using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Graybox
{
    /// <summary>Materiais compartilhados por cor (um por cor, não por objeto) para o gray box.</summary>
    public static class GrayboxMaterials
    {
        private static readonly Dictionary<Color32, Material> Cache = new Dictionary<Color32, Material>();
        private static Material _template;

        public static Material Get(Color color)
        {
            Color32 key = color;
            if (Cache.TryGetValue(key, out var mat) && mat != null) return mat;

            if (_template == null)
            {
                // Base = material ASSET referenciado pelo catálogo visual (garante o shader URP no build).
                // Criar a partir do material padrão de CreatePrimitive deixava tudo ROSA no Android (shader removido).
                var visuals = GameRoot.Instance != null ? GameRoot.Instance.Visuals : null;
                if (visuals != null && visuals.baseLit != null) _template = visuals.baseLit;
                else
                {
                    // Último recurso: shader URP Lit (está no build porque o chão/piso usam). Nunca o material
                    // padrão de CreatePrimitive, que não vai para o Android (rosa).
                    Debug.LogWarning("[Visuals] baseLit ausente; criando material a partir do shader URP Lit.");
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                    _template = new Material(shader);
                    _template.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    _template.SetFloat("_SpecularHighlights", 0f);
                }
            }
            mat = new Material(_template) { enableInstancing = true };
            mat.SetColor("_BaseColor", color);
            Cache[key] = mat;
            return mat;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color, bool keepCollider = false) =>
            Primitive(type, name, parent, Get(color), keepCollider);

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            return go;
        }

        public static Color ColorFor(string id)
        {
            var (r, g, b) = Game.Core.Display.DisplayFormat.ColorFor(id);
            return new Color(r, g, b);
        }
    }
}
