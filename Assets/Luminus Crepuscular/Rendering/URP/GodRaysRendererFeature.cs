using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Luminus.Rendering.URP
{

    public sealed class GodRaysRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public Material material;

            [Range(2, 24)]
            public int downsample = 4;

            public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        [SerializeField] private Settings settings = new();

        private GodRaysPass pass;

        public override void Create()
        {
            pass = new GodRaysPass(settings);
            pass.renderPassEvent = settings.injectionPoint;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.material == null)
                return;

            CameraType cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

            renderer.EnqueuePass(pass);
        }


        private sealed class GodRaysPass : ScriptableRenderPass
        {
            private static readonly int GodRaysTextureID = Shader.PropertyToID("_GodRaysTexture");

            private readonly Settings settings;


            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public int materialPass;
            }


            public GodRaysPass(Settings settings)
            {
                this.settings = settings;

                ConfigureInput(ScriptableRenderPassInput.Depth);
            }


            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle cameraColor = resourceData.activeColorTexture;

                TextureHandle cameraDepth = resourceData.activeDepthTexture;


                // =================================================
                // LOW RES TEXTURES
                // =================================================

                RenderTextureDescriptor lowResolutionDescriptor = cameraData.cameraTargetDescriptor;

                lowResolutionDescriptor.width = Mathf.Max(1, lowResolutionDescriptor.width / settings.downsample);

                lowResolutionDescriptor.height = Mathf.Max(1, lowResolutionDescriptor.height / settings.downsample);

                lowResolutionDescriptor.depthBufferBits = 0;
                lowResolutionDescriptor.msaaSamples = 1;

                // Single channel is enough.
                lowResolutionDescriptor.graphicsFormat = GraphicsFormat.R8_UNorm;


                TextureHandle occlusionMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowResolutionDescriptor, "_GodRaysOcclusionMask", false);


                TextureHandle godRays = UniversalRenderer.CreateRenderGraphTexture(renderGraph, lowResolutionDescriptor, "_GodRaysLowResolution", false);


                // =================================================
                // FULL RES FINAL TARGET
                // =================================================

                RenderTextureDescriptor finalDescriptor = cameraData.cameraTargetDescriptor;

                finalDescriptor.depthBufferBits = 0;
                finalDescriptor.msaaSamples = 1;


                TextureHandle finalColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, finalDescriptor, "_GodRaysFinalColor", false);


                // =================================================
                // PASS 1:
                // DEPTH -> LOW RES OCCLUSION
                // =================================================

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("God Rays - Occlusion Mask", out var passData))
                {
                    passData.source = cameraColor;
                    passData.material = settings.material;
                    passData.materialPass = 0;

                    builder.UseTexture(cameraColor, AccessFlags.Read);

                    builder.UseTexture(cameraDepth, AccessFlags.Read);

                    builder.SetRenderAttachment(occlusionMask, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(
                                context.cmd,
                                data.source,
                                new Vector4(1f, 1f, 0f, 0f),
                                data.material,
                                data.materialPass);
                        });
                }


                // =================================================
                // PASS 2:
                // LOW RES MASK -> LOW RES GOD RAYS
                // =================================================

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("God Rays - Radial Samples", out var passData))
                {
                    passData.source = occlusionMask;
                    passData.material = settings.material;
                    passData.materialPass = 1;

                    builder.UseTexture(occlusionMask, AccessFlags.Read);

                    builder.SetRenderAttachment(godRays, 0, AccessFlags.Write);

                    // Make it available to the composite shader.
                    builder.SetGlobalTextureAfterPass(godRays, GodRaysTextureID);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(
                                context.cmd,
                                data.source,
                                new Vector4(1f, 1f, 0f, 0f),
                                data.material,
                                data.materialPass);
                        });
                }


                // =================================================
                // PASS 3:
                // CAMERA COLOR + LOW RES RAYS -> FINAL
                // =================================================

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("God Rays - Composite", out var passData))
                {
                    passData.source = cameraColor;
                    passData.material = settings.material;
                    passData.materialPass = 2;

                    builder.UseTexture(cameraColor, AccessFlags.Read);

                    builder.UseGlobalTexture(GodRaysTextureID, AccessFlags.Read);

                    builder.SetRenderAttachment(finalColor, 0, AccessFlags.Write);

                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(
                                context.cmd,
                                data.source,
                                new Vector4(1f, 1f, 0f, 0f),
                                data.material,
                                data.materialPass);
                        });
                }


                // Avoid another final blit.
                resourceData.cameraColor = finalColor;
            }
        }
    }
}