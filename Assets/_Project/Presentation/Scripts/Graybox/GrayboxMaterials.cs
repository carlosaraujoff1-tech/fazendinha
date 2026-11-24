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
                // Usa o material padrão do pipeline ativo (URP Lit) como base.
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _template = probe.GetComponent<Renderer>().sharedMaterial;
                Object.Destroy(probe);
            }
            mat = new Material(_template) { color = color, enableInstancing = true };
            Cache[key] = mat;
            return mat;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Color color, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = Get(color);
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
