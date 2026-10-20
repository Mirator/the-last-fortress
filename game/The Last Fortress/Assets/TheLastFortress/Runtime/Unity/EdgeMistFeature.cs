using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TheLastFortress
{
    /// <summary>
    /// Plan 016 edge mist: a full-screen pass after opaques and the sky that covers everything beyond the board with mist, in
    /// both the strategic (orthographic) and hero (perspective) cameras. Built-in fog depends on camera distance and does
    /// nothing for the strategic camera, so the mist is computed from each pixel's world position (depth texture) and the
    /// globals <see cref="WorldView"/> sets. Off (no pass at all) while the mist colour's strength is zero.
    /// </summary>
    public sealed class EdgeMistFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;

        Material material;
        MistPass pass;

        static readonly int MistColorId = Shader.PropertyToID("_TlfMistColor");

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/TheLastFortress/EdgeMist");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new MistPass(material) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox }; // before transparents: HUD, route lines and effects stay on top
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || Shader.GetGlobalVector(MistColorId).w <= 0f) return;
            var type = renderingData.cameraData.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView) return;
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

        sealed class MistPass : ScriptableRenderPass
        {
            readonly Material material;

            sealed class PassData
            {
                public Material material;
            }

            public MistPass(Material material)
            {
                this.material = material;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Edge mist", out var data))
                {
                    data.material = material;
                    if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture);
                    // Blended straight onto the frame (multisampled or not): one full-screen triangle, no colour copy.
                    builder.SetRenderAttachment(resources.activeColorTexture, 0);
                    builder.SetRenderFunc((PassData d, RasterGraphContext context) =>
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3));
                }
            }
        }
    }
}
