using UnityEngine;

[ExecuteInEditMode]
public sealed class SunShaderController : MonoBehaviour
{
    private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
    private static readonly int SunScreenPositionID = Shader.PropertyToID("_SunScreenPosition");

    [SerializeField] private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        Vector3 sunDirection = -transform.forward;

        Vector3 sunWorldPosition = targetCamera.transform.position + sunDirection * 1000f;

        Vector3 screenPosition = targetCamera.WorldToViewportPoint(sunWorldPosition);

        Shader.SetGlobalVector(SunDirectionID, sunDirection);

        Shader.SetGlobalVector(SunScreenPositionID, new Vector4(screenPosition.x, screenPosition.y, screenPosition.z, 0f));
    }
}