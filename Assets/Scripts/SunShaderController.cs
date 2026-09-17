using UnityEngine;

[ExecuteInEditMode]
public sealed class SunShaderController : MonoBehaviour
{
    private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
    private static readonly int SunScreenPositionID = Shader.PropertyToID("_SunScreenPosition");
    private static readonly int SunVisibleID = Shader.PropertyToID("_SunVisible");
    private static readonly int decaySunAngleKoefID = Shader.PropertyToID("_DecaySunAngleKoef");

    [SerializeField] private Camera targetCamera;
    [SerializeField, Range(0f, 1f)] private float offscreenPadding = 0.15f;
    [SerializeField] private float maxSunAngle = 60f;

    private float normalizedSunAngleKoef = 0;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 sunDirection = -transform.forward;

        Vector3 sunWorldPosition = targetCamera.transform.position + sunDirection * 1000f;

        Vector3 viewportPosition = targetCamera.WorldToViewportPoint(sunWorldPosition);

        bool visible =
            viewportPosition.z > 0f &&
            viewportPosition.x >= -offscreenPadding &&
            viewportPosition.x <= 1f + offscreenPadding &&
            viewportPosition.y >= -offscreenPadding &&
            viewportPosition.y <= 1f + offscreenPadding;

        normalizedSunAngleKoef = transform.rotation.eulerAngles.x / maxSunAngle;
        Debug.LogError($"Rotation {transform.rotation.eulerAngles.x}, KOEF " + normalizedSunAngleKoef);

        Shader.SetGlobalVector(SunDirectionID, sunDirection);
        Shader.SetGlobalVector(SunScreenPositionID, new Vector4(viewportPosition.x, viewportPosition.y, viewportPosition.z, 0f));
        Shader.SetGlobalFloat(SunVisibleID, visible ? 1f : 0f);
        Shader.SetGlobalFloat(decaySunAngleKoefID, Mathf.Clamp01(1 - ExpoEase(normalizedSunAngleKoef)));
    }

    private float ExpoEase(float x)
    {
        return x == 0 ? 0 : Mathf.Pow(2, 10 * x - 10);
    }
}