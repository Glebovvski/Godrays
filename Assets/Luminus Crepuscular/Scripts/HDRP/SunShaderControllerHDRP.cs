using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Luminus.Runtime.HDRP
{

    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    [RequireComponent(typeof(HDAdditionalLightData))]
    public sealed class SunShaderControllerHDRP : MonoBehaviour
    {
        private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
        private static readonly int SunScreenPositionID = Shader.PropertyToID("_SunScreenPosition");
        private static readonly int SunVisibleID = Shader.PropertyToID("_SunVisible");
        private static readonly int DecaySunAngleKoefID = Shader.PropertyToID("_DecaySunAngleKoef");

        [Header("HDRP Sun")]
        [SerializeField] private bool affectPhysicallyBasedSky = true;
        [SerializeField, Min(0f)] private float intensityLux = 100000f;

        [Header("God Rays")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool cullEffect;
        [SerializeField, Range(0f, 20f)] private float offscreenPadding = 0.15f;

        [Tooltip("Sun X rotation angle at which the god-ray decay coefficient reaches zero.")]
        [SerializeField, Min(0.01f)] private float maxSunAngle = 60f;

        private Light sunLight;
        private HDAdditionalLightData hdLightData;

        private void OnEnable()
        {
            sunLight = GetComponent<Light>();
            hdLightData = GetComponent<HDAdditionalLightData>();
            ApplyHDRPSettings();
        }

        private void OnValidate()
        {
            if (sunLight == null)
                sunLight = GetComponent<Light>();

            if (hdLightData == null)
                hdLightData = GetComponent<HDAdditionalLightData>();

            ApplyHDRPSettings();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera == null)
                return;

            ApplyHDRPSettings();

            Vector3 sunDirection = -transform.forward;
            Vector3 sunWorldPosition = targetCamera.transform.position + sunDirection * 1000f;
            Vector3 viewportPosition = targetCamera.WorldToViewportPoint(sunWorldPosition);

            bool visible = !cullEffect || viewportPosition.z > 0f &&
                viewportPosition.x >= -offscreenPadding && viewportPosition.x <= 1f + offscreenPadding &&
                viewportPosition.y >= -offscreenPadding && viewportPosition.y <= 1f + offscreenPadding;

            float normalizedSunAngle = transform.rotation.eulerAngles.x / maxSunAngle;
            float decaySunAngleKoef = Mathf.Clamp01(1f - ExpoEase(normalizedSunAngle));

            Shader.SetGlobalVector(SunDirectionID, sunDirection);
            Shader.SetGlobalVector(SunScreenPositionID, new Vector4(viewportPosition.x, viewportPosition.y, viewportPosition.z, 0f));
            Shader.SetGlobalFloat(SunVisibleID, visible ? 1f : 0f);
            Shader.SetGlobalFloat(DecaySunAngleKoefID, decaySunAngleKoef);
        }

        private void ApplyHDRPSettings()
        {
            if (hdLightData == null)
                return;

            hdLightData.interactsWithSky = affectPhysicallyBasedSky;
            hdLightData.SetIntensity(intensityLux, UnityEngine.Rendering.LightUnit.Lux);
        }

        private static float ExpoEase(float x)
        {
            return x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
        }
    }
}