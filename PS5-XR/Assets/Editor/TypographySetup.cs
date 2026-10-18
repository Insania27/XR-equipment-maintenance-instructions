using System;
using System.Linq;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ps5Maintenance.Editor
{
    public static class TypographySetup
    {
        public static void PrepareFont()
        {
            const string path = "Assets/Resources/InstructionFont.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path) != null) return;
            if (TMP_Settings.LoadDefaultSettings() == null || Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
                if (package == null) throw new InvalidOperationException("Не найден пакет интерфейса Unity.");
                AssetDatabase.ImportPackage(Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage"), false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (TMP_Settings.LoadDefaultSettings() == null) throw new InvalidOperationException("Не импортированы настройки TextMeshPro.");
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PTSans-Regular.ttf");
            if (source == null) throw new InvalidOperationException("Не найден шрифт PT Sans.");
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            font.name = "InstructionFont";
            string chars = new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray()) +
                "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя–—·›";
            if (!font.TryAddCharacters(chars, out string missing)) Debug.LogWarning("PS5_XR_FONT_MISSING: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = "InstructionFont Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = "InstructionFont Atlas";
                atlas.filterMode = FilterMode.Bilinear;
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log("PS5_XR_SDF_FONT_CREATED");
        }

        [MenuItem("PS5 XR/Чёткое изображение в Game")]
        public static void SharpenGameView()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.GameView"))
                .FirstOrDefault(candidate => candidate != null);
            if (type == null) { Debug.LogWarning("Отключите Low Resolution Aspect Ratios в меню окна Game."); return; }
            var window = EditorWindow.GetWindow(type);
            var serialized = new SerializedObject(window);
            var lowResolution = serialized.FindProperty("m_LowResolutionForAspectRatios");
            if (lowResolution != null && lowResolution.isArray)
                for (int i = 0; i < lowResolution.arraySize; i++)
                {
                    var value = lowResolution.GetArrayElementAtIndex(i);
                    if (value.propertyType == SerializedPropertyType.Boolean) value.boolValue = false;
                    else if (value.propertyType == SerializedPropertyType.Integer) value.intValue = 0;
                }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            window.Repaint();
            Debug.Log("Низкое разрешение Game отключено. Для проверки телефона выберите 9:16 или 1080 × 1920.");
        }

        public static void ValidateSharpnessCommand()
        {
            SharpenGameView();
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("UnityEditor.GameView"))
                .First(candidate => candidate != null);
            var property = new SerializedObject(EditorWindow.GetWindow(type)).FindProperty("m_LowResolutionForAspectRatios");
            if (property == null || !property.isArray) throw new InvalidOperationException("Не найдена настройка качества Game.");
            for (int i = 0; i < property.arraySize; i++)
            {
                var value = property.GetArrayElementAtIndex(i);
                if (value.propertyType == SerializedPropertyType.Boolean && value.boolValue ||
                    value.propertyType == SerializedPropertyType.Integer && value.intValue != 0)
                    throw new InvalidOperationException("Не удалось отключить низкое разрешение Game.");
            }
            Debug.Log("PS5_XR_GAME_QUALITY_CHECK_PASSED");
        }
    }
}
