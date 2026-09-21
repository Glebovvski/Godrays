using UnityEngine;

namespace Luminus.Runtime.BuiltIn
{

    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    public sealed class SunShaderControllerBuiltIn : MonoBehaviour
    {
        private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
        private static readonly int DecaySunAngleKoefID = Shader.PropertyToID("_DecaySunAngleKoef");

        [SerializeField] private bool setAsRenderSettingsSun = true;

        [Tooltip("Sun X rotation angle at which the god-ray strength reaches zero.")]
        [SerializeField, Min(0.01f)] private float maxSunAngle = 60f;

        private Light sunLight;

        public Vector3 SunDirection => -transform.forward;

        private void OnEnable()
        {
            sunLight = GetComponent<Light>();

            if (setAsRenderSettingsSun)
                RenderSettings.sun = sunLight;

            UpdateSun();
        }

        private void OnValidate()
        {
            if (sunLight == null)
                sunLight = GetComponent<Light>();

            if (setAsRenderSettingsSun && sunLight != null)
                RenderSettings.sun = sunLight;

            UpdateSun();
        }

        private void LateUpdate()
        {
            UpdateSun();
        }

        private void UpdateSun()
        {
            float normalizedSunAngle = transform.rotation.eulerAngles.x / maxSunAngle;
            float decaySunAngleKoef = Mathf.Clamp01(1f - ExpoEase(normalizedSunAngle));

            Shader.SetGlobalVector(SunDirectionID, SunDirection);
            Shader.SetGlobalFloat(DecaySunAngleKoefID, decaySunAngleKoef);
        }

        private static float ExpoEase(float x)
        {
            return x == 0f ? 0f : Mathf.Pow(2f, 10f * x - 10f);
        }
    }
}