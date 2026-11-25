using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Game.Editor.Art
{
    /// <summary>
    /// Padroniza a importação de tudo que vem dos packs (Assets/ThirdParty):
    /// - modelos: materiais URP Lit com a cor/textura do arquivo (cores do FBX vêm em espaço LINEAR, convertidas
    ///   para sRGB; sem isso tudo fica escuro), sem câmeras/luzes, malha comprimida;
    /// - personagens e animais: rig Generic, animações de idle/andar em loop;
    /// - texturas: atlas "colormap" sem mipmap e com filtro pontual (paleta), sprites de UI com borda 9-slice,
    ///   partículas com alfa;
    /// - áudio: comprimido (Vorbis), carregado na memória (são curtos).
    /// </summary>
    public sealed class ThirdPartyPostprocessor : AssetPostprocessor
    {
        private const string Root = "Assets/ThirdParty/";

        public override int GetPostprocessOrder() => 1000; // depois do conversor padrão do URP

        private bool IsOurs => assetPath.StartsWith(Root);

        private bool IsAnimated =>
            assetPath.Contains("/FarmAnimals/") || assetPath.Contains("/MiniCharacters/") || assetPath.Contains("/CubePets/") ||
            System.IO.Path.GetFileName(assetPath).StartsWith("character-");

        private void OnPreprocessModel()
        {
            if (!IsOurs) return;
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.animationType = IsAnimated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = IsAnimated;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsOurs || !IsAnimated) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                clip.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("sprint") || n.Contains("run");
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
            if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse)) color = ((Color)diffuse).gamma;
            color.a = 1f;

            Texture texture = null;
            if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription tex) && tex.texture != null)
            {
                texture = tex.texture;
                color = Color.white; // a cor vem do atlas
            }

            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true;
        }

        private void OnPreprocessTexture()
        {
            if (!IsOurs && !assetPath.StartsWith("Assets/_Project/Presentation/Textures/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            if (name == "colormap" || name.StartsWith("variation-"))
            {
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed; // paleta: compressão borra as cores
                return;
            }
            if (assetPath.Contains("/UI/Sprites/"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = name.StartsWith("panel") ? new Vector4(14, 16, 14, 14) : new Vector4(14, 18, 14, 14);
                return;
            }
            if (assetPath.Contains("/Particles/"))
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 128;
                return;
            }
            if (assetPath.StartsWith("Assets/_Project/Presentation/Textures/"))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 512;
            }
        }

        private void OnPreprocessAudio()
        {
            if (!IsOurs) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
        }
    }
}
