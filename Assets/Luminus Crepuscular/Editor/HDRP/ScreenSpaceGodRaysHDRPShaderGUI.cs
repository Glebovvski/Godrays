using System;
using UnityEditor;
using UnityEngine;

namespace Luminus.Editor.HDRP
{

    public sealed class ScreenSpaceGodRaysHDRPShaderGUI : ShaderGUI
    {
        private const string DustKeyword = "_DUST_ON";
        private const string DustProperty = "_Dust";
        private const string SamplesProperty = "_GodRaySamples";

        private static readonly string[] SampleNames = { "8", "12", "30", "60", "100" };
        private static readonly string[] SampleKeywords =
        {
        "_GODRAYSAMPLES_8",
        "_GODRAYSAMPLES_12",
        "_GODRAYSAMPLES_30",
        "_GODRAYSAMPLES_60",
        "_GODRAYSAMPLES_100"
    };

        private static bool rayAppearanceFoldout = true;
        private static bool rayMarchingFoldout = true;
        private static bool dustFoldout = true;
        private static bool dustAngleFoldout = true;
        private static bool depthFoldout;

        private GUIStyle titleStyle;
        private MaterialProperty godRaySamples;
        private MaterialProperty dustEnabled;
        private MaterialProperty rayColor;
        private MaterialProperty exposure;
        private MaterialProperty exposureWeight;
        private MaterialProperty density;
        private MaterialProperty decay;
        private MaterialProperty weight;
        private MaterialProperty sunSourceRadius;
        private MaterialProperty sunSourcePower;
        private MaterialProperty dustIntensity;
        private MaterialProperty dustScale;
        private MaterialProperty dustSize;
        private MaterialProperty dustDensity;
        private MaterialProperty dustDistance;
        private MaterialProperty dustDepthFade;
        private MaterialProperty dustAnisotropy;
        private MaterialProperty dustVelocity;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            FindProperties(properties);
            SyncKeywords(materialEditor);

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUIUtility.labelWidth = 180f;

            try
            {
                DrawTitle();

                DrawSection("Ray Appearance", ref rayAppearanceFoldout, () =>
                {
                    materialEditor.ShaderProperty(rayColor, new GUIContent("Ray Color", "Final HDR color applied to the rays and dust."));
                    materialEditor.ShaderProperty(exposure, new GUIContent("Ray Exposure", "Brightness multiplier for the completed ray and dust result."));
                    materialEditor.ShaderProperty(exposureWeight, new GUIContent("HDRP Exposure Weight", "0 keeps artistic intensity independent of camera exposure. 1 applies HDRP exposure."));
                });

                DrawSection("Ray Marching", ref rayMarchingFoldout, () =>
                {
                    DrawSamples(materialEditor);
                    Space();
                    materialEditor.ShaderProperty(density, new GUIContent("Ray Length", "Fraction of the screen-space path sampled toward the sun."));
                    materialEditor.ShaderProperty(decay, new GUIContent("Decay", "Attenuation along the radial path, referenced to 60 samples."));
                    materialEditor.ShaderProperty(weight, new GUIContent("Weight", "Multiplier applied after the radial samples are normalized."));
                });

                DrawDustToggle(materialEditor);

                if (dustEnabled.hasMixedValue || dustEnabled.floatValue > 0.5f)
                {
                    DrawSection("Dust Pattern", ref dustFoldout, () =>
                    {
                        materialEditor.ShaderProperty(dustIntensity, new GUIContent("Dust Intensity", "Brightness of procedural dust inside the ray mask."));
                        materialEditor.ShaderProperty(dustScale, new GUIContent("Cells Per Meter", "World-space grid frequency. Higher values make the cells and particles smaller."));
                        materialEditor.ShaderProperty(dustSize, new GUIContent("Particle Radius", "Particle radius as a fraction of one grid cell."));
                        materialEditor.ShaderProperty(dustDensity, new GUIContent("Cell Occupancy", "Fraction of cells containing a dust particle."));
                        Space();
                        materialEditor.ShaderProperty(dustDistance, new GUIContent("Dust Distance", "Distance covered by the four procedural dust sampling layers."));
                        materialEditor.ShaderProperty(dustDepthFade, new GUIContent("Depth Fade", "Distance over which dust fades as a sample approaches scene geometry."));
                        materialEditor.ShaderProperty(dustVelocity, new GUIContent("World Velocity", "XYZ controls dust drift in meters per second. W is unused."));
                    });

                    DrawSection("Dust Lighting Angle", ref dustAngleFoldout, () =>
                    {
                        EditorGUILayout.HelpBox("Controls how strongly dust becomes visible when looking toward the sun.", MessageType.None);
                        materialEditor.ShaderProperty(dustAnisotropy, new GUIContent("Forward Scattering", "Higher values narrow the scattering peak toward the sun. 0 makes the angular response uniform."));
                    });
                }

                DrawSection("Depth Occlusion", ref depthFoldout, () =>
                {
                    EditorGUILayout.HelpBox("The light-source mask uses pixels at the camera's far depth. Occluding geometry must write depth.", MessageType.None);
                    materialEditor.ShaderProperty(sunSourceRadius, new GUIContent("Sun Source Radius", "Radius of the sky source around the projected sun, in screen-height units."));
                    materialEditor.ShaderProperty(sunSourcePower, new GUIContent("Source Falloff", "Higher values concentrate the source closer to the sun."));
                });

                EditorGUILayout.Space(8);
                DrawFooter();
            }
            finally
            {
                EditorGUIUtility.labelWidth = previousLabelWidth;
                EditorGUI.showMixedValue = previousMixedValue;
            }
        }

        public override void ValidateMaterial(Material material)
        {
            SyncKeywords(material);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SyncKeywords(material);
        }

        private void FindProperties(MaterialProperty[] properties)
        {
            godRaySamples = FindProperty(SamplesProperty, properties);
            dustEnabled = FindProperty(DustProperty, properties);
            rayColor = FindProperty("_RayColor", properties);
            exposure = FindProperty("_Exposure", properties);
            exposureWeight = FindProperty("_ExposureWeight", properties);
            density = FindProperty("_Density", properties);
            decay = FindProperty("_Decay", properties);
            weight = FindProperty("_Weight", properties);
            sunSourceRadius = FindProperty("_SunSourceRadius", properties);
            sunSourcePower = FindProperty("_SunSourcePower", properties);
            dustIntensity = FindProperty("_DustIntensity", properties);
            dustScale = FindProperty("_DustScale", properties);
            dustSize = FindProperty("_DustSize", properties);
            dustDensity = FindProperty("_DustDensity", properties);
            dustDistance = FindProperty("_DustDistance", properties);
            dustDepthFade = FindProperty("_DustDepthFade", properties);
            dustAnisotropy = FindProperty("_DustAnisotropy", properties);
            dustVelocity = FindProperty("_DustVelocity", properties);
        }

        private void DrawDustToggle(MaterialEditor materialEditor)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = dustEnabled.hasMixedValue;
            EditorGUI.BeginChangeCheck();

            bool enabled = EditorGUILayout.Toggle(new GUIContent("Enable Dust", "Enables four procedural dust layers in the full-resolution composite pass."), dustEnabled.floatValue > 0.5f);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("Toggle God Ray Dust");
                dustEnabled.floatValue = enabled ? 1f : 0f;
                foreach (Material material in materialEditor.targets)
                {
                    SyncKeywords(material);
                    EditorUtility.SetDirty(material);
                }
            }

            EditorGUI.showMixedValue = previousMixedValue;
            EditorGUILayout.EndVertical();
        }

        private void DrawSamples(MaterialEditor materialEditor)
        {
            int current = Mathf.Clamp(Mathf.RoundToInt(godRaySamples.floatValue), 0, SampleNames.Length - 1);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = godRaySamples.hasMixedValue;
            EditorGUI.BeginChangeCheck();

            int selected = EditorGUILayout.Popup(new GUIContent("Samples", "Higher values smooth radial rays at greater GPU cost. Procedural dust always uses four layers."), current, SampleNames);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo("God Ray Samples");
                godRaySamples.floatValue = selected;
                foreach (Material material in materialEditor.targets)
                {
                    SyncKeywords(material);
                    EditorUtility.SetDirty(material);
                }
            }

            EditorGUI.showMixedValue = previousMixedValue;
        }

        private static void SyncKeywords(MaterialEditor materialEditor)
        {
            // Read each selected material's own values; opening a mixed selection must not unify it.
            foreach (Material material in materialEditor.targets)
                SyncKeywords(material);
        }

        private static void SyncKeywords(Material material)
        {
            int selected = Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(SamplesProperty)), 0, SampleKeywords.Length - 1);
            for (int i = 0; i < SampleKeywords.Length; i++)
                SetKeyword(material, SampleKeywords[i], i == selected);

            SetKeyword(material, DustKeyword, material.GetFloat(DustProperty) > 0.5f);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (material.IsKeywordEnabled(keyword) == enabled)
                return;
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private void DrawTitle()
        {
            EditorGUILayout.Space(6);
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            EditorGUILayout.LabelField("Screen Space God Rays - HDRP", titleStyle);
            EditorGUILayout.LabelField("Ray marching, procedural dust and depth occlusion", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
        }

        private static void DrawSection(string title, ref bool foldout, Action drawContent)
        {
            EditorGUILayout.Space(3);
            foldout = EditorGUILayout.BeginFoldoutHeaderGroup(foldout, title);
            if (foldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.Space(3);
                drawContent();
                EditorGUILayout.Space(3);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void Space()
        {
            EditorGUILayout.Space(6);
        }

        private static void DrawFooter()
        {
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assign this material to GodRaysHDRPCustomPass at Before Post Process. Set ray resolution and overall intensity on the Custom Pass.", MessageType.None);
        }
    }

}