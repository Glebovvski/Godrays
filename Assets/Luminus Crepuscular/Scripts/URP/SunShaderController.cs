using UnityEngine;

namespace Luminus.Runtime.URP
{

#if UNITY_EDITOR
    using UnityEditor;
#endif

    [ExecuteInEditMode]
    public sealed class SunShaderController : MonoBehaviour
    {
        private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
        private static readonly int SunScreenPositionID = Shader.PropertyToID("_SunScreenPosition");
        private static readonly int SunVisibleID = Shader.PropertyToID("_SunVisible");
        private static readonly int DecaySunAngleKoefID = Shader.PropertyToID("_DecaySunAngleKoef");

        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool cullEffect = false;
        [SerializeField, Range(0f, 20f)] private float offscreenPadding = 0.15f;

        [Tooltip("Sun X rotation angle at which the sun-angle coefficient reaches zero. Used to gradually reduce the god ray effect as the sun approaches this angle.")]
        [SerializeField] private float maxSunAngle = 60f;

        private float normalizedSunAngleKoef;

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
                return;

            Vector3 sunDirection = -transform.forward;
            Vector3 sunWorldPosition = targetCamera.transform.position + sunDirection * 1000f;
            Vector3 viewportPosition = targetCamera.WorldToViewportPoint(sunWorldPosition);

            bool visible = cullEffect
                ? viewportPosition.z > 0f &&
                  viewportPosition.x >= -offscreenPadding &&
                  viewportPosition.x <= 1f + offscreenPadding &&
                  viewportPosition.y >= -offscreenPadding &&
                  viewportPosition.y <= 1f + offscreenPadding
                : true;

            normalizedSunAngleKoef = transform.rotation.eulerAngles.x / maxSunAngle;

            Shader.SetGlobalVector(SunDirectionID, sunDirection);
            Shader.SetGlobalVector(SunScreenPositionID, new Vector4(viewportPosition.x, viewportPosition.y, viewportPosition.z, 0f));
            Shader.SetGlobalFloat(SunVisibleID, visible ? 1f : 0f);
            Shader.SetGlobalFloat(DecaySunAngleKoefID, Mathf.Clamp01(1f - ExpoEase(normalizedSunAngleKoef)));
        }

        private float ExpoEase(float x)
        {
            return x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SunShaderController))]
    public sealed class SunShaderControllerEditor : Editor
    {
        private SerializedProperty targetCamera;
        private SerializedProperty cullEffect;
        private SerializedProperty offscreenPadding;
        private SerializedProperty maxSunAngle;

        private void OnEnable()
        {
            targetCamera = serializedObject.FindProperty("targetCamera");
            cullEffect = serializedObject.FindProperty("cullEffect");
            offscreenPadding = serializedObject.FindProperty("offscreenPadding");
            maxSunAngle = serializedObject.FindProperty("maxSunAngle");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(targetCamera);
            EditorGUILayout.PropertyField(cullEffect);

            if (cullEffect.boolValue)
                EditorGUILayout.PropertyField(offscreenPadding);

            EditorGUILayout.PropertyField(maxSunAngle);

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}