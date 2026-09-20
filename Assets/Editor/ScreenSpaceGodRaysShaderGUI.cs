using UnityEditor;
using UnityEngine;

public sealed class ScreenSpaceGodRaysShaderGUI : ShaderGUI
{
    private static bool rayAppearanceFoldout = true;
    private static bool rayMarchingFoldout = true;
    private static bool dustFoldout = true;
    private static bool dustAngleFoldout = true;
    private static bool depthFoldout = false;

    private MaterialProperty noise;
    private MaterialProperty noiseStrength;
    private MaterialProperty noiseThreshold;
    private MaterialProperty noiseSoftness;
    private MaterialProperty dustDistance;
    private MaterialProperty noiseScale;
    private MaterialProperty noiseSpeed;
    private MaterialProperty dustMinStrength;
    private MaterialProperty dustMaxStrength;
    private MaterialProperty dustAngleStart;
    private MaterialProperty dustAngleEnd;
    private MaterialProperty dustAnglePower;
    private MaterialProperty rayColor;
    private MaterialProperty intensity;
    private MaterialProperty density;
    private MaterialProperty decay;
    private MaterialProperty weight;
    private MaterialProperty exposure;
    private MaterialProperty maxRayDistance;
    private MaterialProperty depthThreshold;
    private MaterialProperty depthSoftness;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        FindProperties(properties);

        EditorGUIUtility.labelWidth = 180f;

        DrawTitle();

        DrawSection("Ray Appearance", ref rayAppearanceFoldout, () =>
        {
            materialEditor.ShaderProperty(rayColor, new GUIContent("Ray Color", "Final color applied to the god rays."));
            materialEditor.ShaderProperty(intensity, new GUIContent("Intensity", "Final god ray intensity added to the scene."));
        });

        DrawSection("Ray Marching", ref rayMarchingFoldout, () =>
        {
            materialEditor.ShaderProperty(density, new GUIContent("Density", "How far ray-march samples travel toward the sun."));
            materialEditor.ShaderProperty(decay, new GUIContent("Decay", "How quickly samples lose brightness along the ray."));
            materialEditor.ShaderProperty(weight, new GUIContent("Weight", "Brightness contribution of each ray-march sample."));
            materialEditor.ShaderProperty(exposure, new GUIContent("Exposure", "Final multiplier applied to the accumulated ray result."));
            materialEditor.ShaderProperty(maxRayDistance, new GUIContent("Max Ray Distance", "Maximum screen-space distance from the projected sun where rays are evaluated."));
        });

        DrawSection("Dust & Noise", ref dustFoldout, () =>
        {
            materialEditor.TexturePropertySingleLine(new GUIContent("Noise Texture", "Noise texture used to generate visible dust particles."), noise);
            materialEditor.TextureScaleOffsetProperty(noise);

            Space();

            materialEditor.ShaderProperty(noiseStrength, new GUIContent("Noise Strength", "Global multiplier for dust brightness."));
            materialEditor.ShaderProperty(noiseThreshold, new GUIContent("Particle Threshold", "Higher values make the dust more sparse."));
            materialEditor.ShaderProperty(noiseSoftness, new GUIContent("Particle Softness", "Controls softness around each dust particle."));
            
            Space();

            materialEditor.ShaderProperty(dustDistance, new GUIContent("Dust Distance", "Maximum virtual world-space distance used when sampling dust layers."));
            materialEditor.ShaderProperty(noiseScale, new GUIContent("Noise Scale", "World-space scale of the dust pattern. Higher values produce smaller features."));
            materialEditor.ShaderProperty(noiseSpeed, new GUIContent("Noise Speed", "World-space movement direction and speed of the dust."));
        });

        DrawSection("Dust Lighting Angle", ref dustAngleFoldout, () =>
        {
            EditorGUILayout.HelpBox("Controls how strongly dust becomes visible as the camera view direction aligns with the sun direction.", MessageType.None);

            materialEditor.ShaderProperty(dustMinStrength, new GUIContent("Minimum Strength", "Dust brightness when viewed away from the ideal scattering angle."));
            materialEditor.ShaderProperty(dustMaxStrength, new GUIContent("Maximum Strength", "Dust brightness when viewed close to the ideal scattering angle."));

            Space();

            materialEditor.ShaderProperty(dustAngleStart, new GUIContent("Angle Start", "Sun/view alignment where dust starts becoming noticeably brighter."));
            materialEditor.ShaderProperty(dustAngleEnd, new GUIContent("Angle End", "Sun/view alignment where dust reaches maximum angular strength."));
            materialEditor.ShaderProperty(dustAnglePower, new GUIContent("Angle Curve", "Shapes the transition from minimum to maximum dust brightness."));
        });

        DrawSection("Depth Occlusion", ref depthFoldout, () =>
        {
            EditorGUILayout.HelpBox("Controls conversion of the camera depth texture into the low-resolution sky/occlusion mask.", MessageType.None);

            materialEditor.ShaderProperty(depthThreshold, new GUIContent("Depth Threshold", "Depth value considered far enough to be treated as sky."));
            materialEditor.ShaderProperty(depthSoftness, new GUIContent("Depth Softness", "Soft transition around the sky depth threshold."));
        });

        EditorGUILayout.Space(8);
        DrawFooter(materialEditor);
    }

    private void FindProperties(MaterialProperty[] properties)
    {
        noise = FindProperty("_Noise", properties);
        noiseStrength = FindProperty("_NoiseStrength", properties);
        noiseThreshold = FindProperty("_NoiseThreshold", properties);
        noiseSoftness = FindProperty("_NoiseSoftness", properties);

        dustDistance = FindProperty("_DustDistance", properties);
        noiseScale = FindProperty("_NoiseScale", properties);
        noiseSpeed = FindProperty("_NoiseSpeed", properties);

        dustMinStrength = FindProperty("_DustMinStrength", properties);
        dustMaxStrength = FindProperty("_DustMaxStrength", properties);
        dustAngleStart = FindProperty("_DustAngleStart", properties);
        dustAngleEnd = FindProperty("_DustAngleEnd", properties);
        dustAnglePower = FindProperty("_DustAnglePower", properties);

        rayColor = FindProperty("_RayColor", properties);
        intensity = FindProperty("_Intensity", properties);

        density = FindProperty("_Density", properties);
        decay = FindProperty("_Decay", properties);
        weight = FindProperty("_Weight", properties);
        exposure = FindProperty("_Exposure", properties);
        maxRayDistance = FindProperty("_MaxRayDistance", properties);

        depthThreshold = FindProperty("_DepthThreshold", properties);
        depthSoftness = FindProperty("_DepthSoftness", properties);
    }

    private static void DrawTitle()
    {
        EditorGUILayout.Space(6);

        GUIStyle titleStyle = new(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };

        EditorGUILayout.LabelField("Screen Space God Rays", titleStyle);
        EditorGUILayout.LabelField("Ray marching, world-space dust and depth occlusion", EditorStyles.miniLabel);

        EditorGUILayout.Space(6);
    }

    private static void DrawSection(string title, ref bool foldout, System.Action drawContent)
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

    private static void DrawFooter(MaterialEditor materialEditor)
    {
        EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }
}