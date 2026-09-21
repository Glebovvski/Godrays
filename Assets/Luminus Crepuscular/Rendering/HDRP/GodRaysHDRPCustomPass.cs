using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Luminus.Rendering.HDRP
{
    [Serializable]
    public sealed class GodRaysHDRPCustomPass : CustomPass
    {
        public enum Resolution { Full = 1, Half = 2, Quarter = 4 }

        [Tooltip("A material using Custom/HDRP/ScreenSpaceGodRays. Keep this reference for player builds.")]
        public Material material;
        [Tooltip("Directional light. Falls back to RenderSettings.sun when unassigned.")]
        public Light sun;
        public Resolution rayResolution = Resolution.Half;
        [Min(0f)] public float intensity = 1f;
        [Tooltip("Fade the effect as the sun approaches 90 degrees from the camera's forward direction.")]
        [Range(0.01f, 0.5f)] public float sunAngleFade = 0.1f;

        private RTHandle occlusionBuffer;
        private RTHandle raysBuffer;
        private Resolution allocatedResolution;

        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int SunVisibleId = Shader.PropertyToID("_SunVisible");
        private static readonly int SunAngleId = Shader.PropertyToID("_DecaySunAngleKoef");
        private static readonly int PassIntensityId = Shader.PropertyToID("_GodRaysPassIntensity");
        private static readonly int TargetSizeId = Shader.PropertyToID("_GodRaysTargetSize");
        private static readonly int OcclusionTextureId = Shader.PropertyToID("_OcclusionTexture");
        private static readonly int RaysTextureId = Shader.PropertyToID("_GodRaysTexture");
        private static readonly int OcclusionScaleId = Shader.PropertyToID("_OcclusionTextureScale");
        private static readonly int RaysScaleId = Shader.PropertyToID("_GodRaysTextureScale");

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            AllocateBuffers();
        }

        protected override void Execute(CustomPassContext ctx)
        {
            Camera camera = ctx.hdCamera.camera;
            Light directionalLight = sun != null ? sun : RenderSettings.sun;
            if (material == null || directionalLight == null || intensity <= 0f || fadeValue <= 0f)
                return;
            if (camera.orthographic || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;
            if (directionalLight.type != LightType.Directional || !directionalLight.isActiveAndEnabled || directionalLight.intensity <= 0f)
                return;

            Vector3 sunDirection = -directionalLight.transform.forward;
            float viewSunDot = Vector3.Dot(camera.transform.forward, sunDirection);
            if (viewSunDot <= 0f)
                return;

            if (occlusionBuffer == null || raysBuffer == null || allocatedResolution != rayResolution)
                AllocateBuffers();

            var fullSize = new Vector2Int(ctx.hdCamera.actualWidth, ctx.hdCamera.actualHeight);
            Vector2Int raysSize = raysBuffer.GetScaledSize(fullSize);
            MaterialPropertyBlock properties = ctx.propertyBlock;
            properties.SetVector(SunDirectionId, new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0f));
            properties.SetFloat(SunVisibleId, 1f);
            properties.SetFloat(SunAngleId, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(viewSunDot / sunAngleFade)));
            properties.SetFloat(PassIntensityId, intensity * fadeValue);
            properties.SetVector(OcclusionScaleId, GetTextureScale(occlusionBuffer, fullSize));
            properties.SetVector(RaysScaleId, GetTextureScale(raysBuffer, raysSize));

            // Full-resolution sky/depth mask. Explicit viewport sizes also cover dynamic resolution.
            SetIntermediateTarget(ctx.cmd, occlusionBuffer, fullSize);
            properties.SetVector(TargetSizeId, GetSizeVector(fullSize));
            CoreUtils.DrawFullScreen(ctx.cmd, material, properties, shaderPassId: 0);

            // Radial sampling into a separate buffer; no pass samples its own render target.
            properties.SetTexture(OcclusionTextureId, occlusionBuffer.rt);
            SetIntermediateTarget(ctx.cmd, raysBuffer, raysSize);
            properties.SetVector(TargetSizeId, GetSizeVector(raysSize));
            CoreUtils.DrawFullScreen(ctx.cmd, material, properties, shaderPassId: 1);

            // Hardware additive blending preserves camera color and alpha without a scene-color copy.
            properties.SetTexture(RaysTextureId, raysBuffer.rt);
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.None);
            CoreUtils.DrawFullScreen(ctx.cmd, material, properties, shaderPassId: 2);
        }

        private void AllocateBuffers()
        {
            ReleaseBuffers();
            allocatedResolution = rayResolution;
            int divisor = Mathf.Max(1, (int)rayResolution);

            // HDRP owns the RTHandle reference size. Intermediate textures use explicit viewports
            // instead of hardware dynamic scaling, so their allocation texel sizes remain unambiguous.
            occlusionBuffer = RTHandles.Alloc(Vector2.one, slices: TextureXR.slices,
                dimension: TextureXR.dimension, colorFormat: GraphicsFormat.R16_SFloat,
                filterMode: FilterMode.Bilinear, wrapMode: TextureWrapMode.Clamp,
                useDynamicScale: false, name: "God Rays Occlusion");

            raysBuffer = RTHandles.Alloc(size => new Vector2Int(
                    Mathf.Max(1, (size.x + divisor - 1) / divisor),
                    Mathf.Max(1, (size.y + divisor - 1) / divisor)),
                slices: TextureXR.slices, dimension: TextureXR.dimension,
                colorFormat: GraphicsFormat.R16_SFloat, filterMode: FilterMode.Bilinear,
                wrapMode: TextureWrapMode.Clamp, useDynamicScale: false, name: "God Rays Radial");
        }

        private static Vector4 GetSizeVector(Vector2Int size)
        {
            return new Vector4(size.x, size.y, 1f / size.x, 1f / size.y);
        }

        private static Vector4 GetTextureScale(RTHandle texture, Vector2Int viewport)
        {
            float width = texture.rt.width;
            float height = texture.rt.height;
            return new Vector4(viewport.x / width, viewport.y / height, 1f / width, 1f / height);
        }

        private static void SetIntermediateTarget(CommandBuffer cmd, RTHandle target, Vector2Int size)
        {
            CoreUtils.SetRenderTarget(cmd, target, ClearFlag.None);
            cmd.SetViewport(new Rect(0f, 0f, size.x, size.y));
        }

        protected override void Cleanup()
        {
            ReleaseBuffers();
        }

        private void ReleaseBuffers()
        {
            occlusionBuffer?.Release();
            raysBuffer?.Release();
            occlusionBuffer = null;
            raysBuffer = null;
        }
    }
}