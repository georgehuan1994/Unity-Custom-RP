using UnityEngine;
using UnityEngine.Rendering;

public partial class PostFXStack
{
    private ScriptableRenderContext _context;
    
    private Camera _camera;

    private PostFXSettings _settings;
    
    private const string BufferName = "Post FX";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName,
    };
    
    private enum Pass
    {
        Prefilter,
        BloomCombine,
        BloomVertical,
        BloomHorizontal,
        Copy,
    };

    private int _bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
    private int _bloomPrefilterId = Shader.PropertyToID("_BloomPrefilter");
    private int _bloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    private int _bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    private int _fxSourceId = Shader.PropertyToID("_PostFXSource");
    private int _fxSource2Id = Shader.PropertyToID("_PostFXSource2");

    // 辉光纹理采样金字塔最大等级
    private const int MaxBloomPyramidLevels = 16;

    // 辉光采样纹理标识符
    private int _bloomPyramidId;
    
    // 当前 stack 是否处于活动状态
    public bool IsActive => _settings != null;
    
    public PostFXStack()
    {
        // 在构造 PostFXStack 时创建辉光采样纹理标识符
        _bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
        
        // for (int i = 0; i < MaxBloomPyramidLevels; i++)
        // {
        //     Shader.PropertyToID("_BloomPyramid" + i);
        // }
        
        for (int i = 0; i < MaxBloomPyramidLevels * 2; i++)
        {
            Shader.PropertyToID("_BloomPyramid" + i);
        }
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        _context = context;
        _camera = camera;
        _settings = camera.cameraType != CameraType.SceneView ? settings : null;
        
#if UNITY_EDITOR
        ApplySceneViewState();
#endif
    }

    public void Render(int sourceId)
    {
        // _buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        // Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        
        // GaussianBlurring(sourceId);
        DoBloom(sourceId);
        
        _context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }

    private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass)
    {
        // 从标识符为 from 的 RenderTarget 中获取纹理，复制到标识符为 _PostFXSource 的纹理
        _buffer.SetGlobalTexture(_fxSourceId, from);
        // 将标识符为 to 的 RenderTarget 作为绘制目标
        _buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        // 使用后处理材质在 RenderTarget 上绘制三角形
        _buffer.DrawProcedural(Matrix4x4.identity, _settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }

    private void GaussianBlurring(int sourceId)
    {
        _buffer.BeginSample("Gaussian Blur");
        PostFXSettings.BloomSettings bloom = _settings.Bloom;
        
        int width = _camera.pixelWidth / 2;
        int height = _camera.pixelHeight / 2;
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        
        _buffer.GetTemporaryRT(_bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, _bloomPrefilterId, Pass.Prefilter);
        width /= 2;
        height /= 2;
        
        int fromId = sourceId;
        int toId = _bloomPyramidId + 1;

        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }
            
            int midId = toId - 1;
            _buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            _buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);

            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }
        
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

        for (i -= 1; i > 0; i--)
        {
            _buffer.ReleaseTemporaryRT(fromId);
            _buffer.ReleaseTemporaryRT(fromId - 1);
            fromId -= 2;

        } 
        
        _buffer.EndSample("Gaussian Blur");
    }

    private void DoBloom(int sourceId)
    {
        _buffer.BeginSample("Bloom");

        PostFXSettings.BloomSettings bloom = _settings.Bloom;
        
        int width = _camera.pixelWidth / 2;
        int height = _camera.pixelHeight / 2;

        int resScale = bloom.halfRes ? 2 : 1;

        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downscaleLimit * resScale || width < bloom.downscaleLimit * resScale)
        {
            Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
            _buffer.EndSample("Bloom");
            return;
        }

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = .25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        _buffer.SetGlobalVector(_bloomThresholdId, threshold);
        
        RenderTextureFormat format = RenderTextureFormat.Default;

        _buffer.GetTemporaryRT(_bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, _bloomPrefilterId, Pass.Prefilter);
        
        if (bloom.halfRes)
        {
            width /= 2;
            height /= 2;
        }

        int fromId = _bloomPrefilterId;
        int toId = _bloomPyramidId + 1;

        int i;
        for (i = 0; i < bloom.maxIterations; i++)
        {
            if (height < bloom.downscaleLimit || width < bloom.downscaleLimit)
            {
                break;
            }

            int midId = toId - 1;
            
            _buffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
            _buffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
            
            Draw(fromId, midId, Pass.BloomHorizontal);
            Draw(midId, toId, Pass.BloomVertical);
            
            fromId = toId;
            toId += 2;
            width /= 2;
            height /= 2;
        }

        _buffer.ReleaseTemporaryRT(_bloomPrefilterId);

        // Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);

        _buffer.SetGlobalFloat(_bloomBicubicUpsamplingId, bloom.bicubicUpsampling ? 1f : 0f);
        _buffer.SetGlobalFloat(_bloomIntensityId, 1);
        
        if (i > 1)
        {
            _buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
        
            for (i -= 1; i > 0; i--)
            {
                _buffer.SetGlobalTexture(_fxSource2Id, toId + 1);
                Draw(fromId, toId, Pass.BloomCombine);
                _buffer.ReleaseTemporaryRT(fromId);
                _buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            } 
        }
        else
        {
            _buffer.ReleaseTemporaryRT(_bloomPyramidId);
        }
        
        _buffer.SetGlobalFloat(_bloomIntensityId, bloom.intensity);
        _buffer.SetGlobalTexture(_fxSource2Id, sourceId);
        Draw(fromId, BuiltinRenderTextureType.CameraTarget, Pass.BloomCombine);
        _buffer.ReleaseTemporaryRT(fromId);
        
        _buffer.EndSample("Bloom");
    }
}
