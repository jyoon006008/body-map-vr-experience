using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Empty class to be used in scenes and doesn't implement any additional overrides
public class FullscreenEffect : FullscreenEffectBase<FullscreenPassBase>
{
}

[ExecuteAlways]
public class FullscreenEffectBase<T> : MonoBehaviour where T:FullscreenPassBase, new()
{
    private T _pass;

    [SerializeField]
    private string _passName = "Fullscreen Pass";

    [SerializeField]
    private Material _material;

    [SerializeField]
    private RenderPassEvent _injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
    [SerializeField]
    private int _injectionPointOffset = 0;
    [SerializeField]
    private ScriptableRenderPassInput _inputRequirements = ScriptableRenderPassInput.None;
    [SerializeField]
    private CameraType _cameraType = CameraType.Game | CameraType.SceneView;


    private void OnEnable()
    {
        SetupPass();

        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
    }

    public virtual void SetupPass()
    {

        _pass ??= new T();

        // pass setup
        _pass.renderPassEvent = _injectionPoint + _injectionPointOffset;
        _pass.material = _material;
        if (_material != null)
        {
            _pass.hasYFlipKeyword = _material.shader.keywordSpace.keywordNames.Contains("_FLIPY");

            if (_pass.hasYFlipKeyword)
                _pass.yFlipKeyword = new LocalKeyword(_material.shader, "_FLIPY");
        }
        _pass.passName = _passName;

        _pass.ConfigureInput(_inputRequirements);
    }

    public virtual void OnBeginCamera( ScriptableRenderContext ctx, Camera cam )
    {
        // Skip if pass wasn't initialized or if material is empty
        if (_pass == null || _material == null)
            return;

        // Only draw for selected camera types
        if ( (cam.cameraType & _cameraType) == 0) return;

        // injection pass
        cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass( _pass );
    }

    private void OnValidate()
    {
        SetupPass();
    }
}

public class FullscreenPassBase : ScriptableRenderPass
{
    public Material material;

    public bool hasYFlipKeyword;
    public LocalKeyword yFlipKeyword;
    public string passName = "Fullscreen Pass";

    public System.Action<Material> additionalExecuteAction;

    public virtual bool ConfigureMaterialProperties()
    {
        return true;
    }

#if UNITY_6000_0_OR_NEWER
    private class PassData
    {
        public Material material;
    }

    public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, UnityEngine.Rendering.ContextContainer frameData)
    {
        var resourceData = frameData.Get<UnityEngine.Rendering.Universal.UniversalResourceData>();
        if (resourceData == null || material == null)
            return;

        if (!ConfigureMaterialProperties())
            return;

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
        {
            passData.material = material;
            builder.UseTexture(resourceData.activeColorTexture, UnityEngine.Rendering.RenderGraphModule.AccessFlags.ReadWrite);
            
            builder.SetRenderFunc((PassData data, UnityEngine.Rendering.RenderGraphModule.RasterGraphContext context) =>
            {
                if (additionalExecuteAction != null)
                {
                    additionalExecuteAction(data.material);
                }
                UnityEngine.Rendering.CoreUtils.DrawFullScreen(context.cmd, data.material);
            });
        }
    }
#else
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!ConfigureMaterialProperties())
            return;

        if (hasYFlipKeyword)
            material.SetKeyword(
                yFlipKeyword,
                renderingData.cameraData.IsRenderTargetProjectionMatrixFlipped(renderingData.cameraData.renderer.cameraColorTargetHandle)
                );

        var cmd = CommandBufferPool.Get(passName);

        if (additionalExecuteAction != null)
        {
            additionalExecuteAction(material);
        }

        CoreUtils.DrawFullScreen(cmd, material);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }
#endif
}
