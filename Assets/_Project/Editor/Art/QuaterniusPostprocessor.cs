using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Game.Editor.Art
{
    /// <summary>
    /// Padroniza a importação dos modelos dos packs (Assets/ThirdParty): materiais viram URP Lit com a cor
    /// do FBX (independe de qual pipeline estava ativo na importação), sem câmeras/luzes, malha comprimida;
    /// animais com rig Generic e animações em loop.
    /// </summary>
    public sealed class QuaterniusPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/";
        private const string AnimalsFolder = "/FarmAnimals/";

        public override int GetPostprocessOrder() => 1000; // depois do conversor padrão do URP

        private bool IsOurs => assetPath.StartsWith(Root);

        private void OnPreprocessModel()
        {
            if (!IsOurs) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            bool animal = assetPath.Contains(AnimalsFolder);
            importer.animationType = animal ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = animal;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsOurs || !assetPath.Contains(AnimalsFolder)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                clip.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("run");
            }
            importer.clipAnimations = clips;
        }

        private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] animations)
        {
            if (!IsOurs) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            material.shader = shader;

            Color color = Color.white;
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse)) color = diffuse;
            else if (description.TryGetProperty("BaseColor", out Vector4 baseColor)) color = baseColor;
            color.a = 1f;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
        }
    }
}
