using Luminus.Runtime.BuiltIn;
using UnityEngine;

namespace Luminus.Rendering.BuiltIn
{

    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public sealed class GodRaysImageEffect : MonoBehaviour
    {
        private static readonly int SunScreenPositionID = Shader.PropertyToID("_SunScreenPosition");
        private static readonly int SunVisibleID = Shader.PropertyToID("_SunVisible");

        [SerializeField] private Material material;
        [SerializeField] private SunShaderControllerBuiltIn sun;
        [SerializeField, Range(2, 24)] private int downsample = 4;

        [Header("Sun Culling")]
        [SerializeField] private bool cullEffect;
        [SerializeField, Range(0f, 20f)] private float offscreenPadding = 0.15f;

        private Camera targetCamera;

        private void OnEnable()
        {
            targetCamera = GetComponent<Camera>();
            targetCamera.depthTextureMode |= DepthTextureMode.Depth;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (material == null || sun == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            Camera renderingCamera = Camera.current != null ? Camera.current : targetCamera;
            renderingCamera.depthTextureMode |= DepthTextureMode.Depth;

            UpdateSunForCamera(renderingCamera);

            int width = Mathf.Max(1, source.width / downsample);
            int height = Mathf.Max(1, source.height / downsample);

            RenderTexture occlusionMask = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture godRays = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

            occlusionMask.filterMode = FilterMode.Bilinear;
            occlusionMask.wrapMode = TextureWrapMode.Clamp;
            godRays.filterMode = FilterMode.Bilinear;
            godRays.wrapMode = TextureWrapMode.Clamp;

            Graphics.Blit(source, occlusionMask, material, 0);   // _MainTex = scene
            Graphics.Blit(occlusionMask, godRays, material, 1); // _MainTex = occlusion

            Graphics.Blit(source, destination);                  // copy scene
            Graphics.Blit(godRays, destination, material, 2);   // _MainTex = rays, additive

            RenderTexture.ReleaseTemporary(occlusionMask);
            RenderTexture.ReleaseTemporary(godRays);
        }

        private void UpdateSunForCamera(Camera camera)
        {
            Vector3 sunWorldPosition = camera.transform.position + sun.SunDirection * 1000f;
            Vector3 viewportPosition = camera.WorldToViewportPoint(sunWorldPosition);

            bool visible = !cullEffect || viewportPosition.z > 0f &&
                viewportPosition.x >= -offscreenPadding && viewportPosition.x <= 1f + offscreenPadding &&
                viewportPosition.y >= -offscreenPadding && viewportPosition.y <= 1f + offscreenPadding;

            Shader.SetGlobalVector(SunScreenPositionID, new Vector4(viewportPosition.x, viewportPosition.y, viewportPosition.z, 0f));
            Shader.SetGlobalFloat(SunVisibleID, visible ? 1f : 0f);
        }
    }
}