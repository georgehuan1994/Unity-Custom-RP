using UnityEngine;
using UnityEngine.Rendering;
using static PostFXSettings;

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
        ColorGradingFinal,
        ColorGradingNone,
        ColorGradingACES,
        ColorGradingNeutral,
        ColorGradingReinhard,
        BloomScatterFinal,
        BloomScatter,
        BloomPrefilterFireflies,
        BloomPrefilter,
        BloomAdd,
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
    private int _bloomResultId = Shader.PropertyToID("_BloomResult");
    private int _colorAdjustmentsId = Shader.PropertyToID("_ColorAdjustments");
    private int _colorFilterId = Shader.PropertyToID("_ColorFilter");
    private int _whiteBalanceId = Shader.PropertyToID("_WhiteBalance");
    private int _splitToningShadowsId = Shader.PropertyToID("_SplitToningShadows");
    private int _splitToningHighlightId = Shader.PropertyToID("_SplitToningHighlights");
    private int _channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
    private int _channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
    private int _channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");
    private int _smhShadowsId = Shader.PropertyToID("_SMHShadows");
    private int _smhMidtonesId = Shader.PropertyToID("_SMHMidtones");
    private int _smhHighlightId = Shader.PropertyToID("_SMHHighlights");
    private int _smhRangeId = Shader.PropertyToID("_SMHRange");
    private int _colorGradingLUTId = Shader.PropertyToID("_ColorGradingLUT");
    private int _ColorGradingLUTParametersId = Shader.PropertyToID("_ColorGradingLUTParameters");
    private int _colorGradingLUTInLogId = Shader.PropertyToID("_ColorGradingLUTInLogC");
    
    // 辉光纹理采样金字塔最大等级
    private const int MaxBloomPyramidLevels = 16;

    // 辉光采样纹理标识符
    private int _bloomPyramidId;
    
    // 当前 stack 是否处于活动状态
    public bool IsActive => _settings != null;

    private bool _useHDR;

    private int _colorLUTResolution;
    
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

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings, bool useHDR, int colorLUTResolution)
    {
        _context = context;
        _camera = camera;
        _settings = camera.cameraType != CameraType.SceneView ? settings : null;
        _useHDR = useHDR;
        _colorLUTResolution = colorLUTResolution;
        
#if UNITY_EDITOR
        ApplySceneViewState();
#endif
    }

    public void Render(int sourceId)
    {
        // _buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        // Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        
        // GaussianBlurring(sourceId);
        // DoBloom(sourceId);
        
        if (DoBloom(sourceId))
        {
            DoColorGradingAndToneMapping(_bloomResultId);
            _buffer.ReleaseTemporaryRT(_bloomResultId);
        }
        else
        {
            DoColorGradingAndToneMapping(sourceId);
        }
        
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
    
    
    private void DrawFinal(RenderTargetIdentifier from)
    {
        // 从标识符为 from 的 RenderTarget 中获取纹理，复制到标识符为 _PostFXSource 的纹理
        _buffer.SetGlobalTexture(_fxSourceId, from);
        // 将 Camera 的 RenderTarget 作为绘制目标
        _buffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
        // 校正视口参数
        _buffer.SetViewport(_camera.pixelRect);
        // 使用后处理材质在 RenderTarget 上绘制三角形
        _buffer.DrawProcedural(Matrix4x4.identity, _settings.Material, (int)Pass.ColorGradingFinal, MeshTopology.Triangles, 3);
    }

    private void GaussianBlurring(int sourceId)
    {
        _buffer.BeginSample("Gaussian Blur");
        PostFXSettings.BloomSettings bloom = _settings.Bloom;
        
        int width = _camera.pixelWidth / 2;
        int height = _camera.pixelHeight / 2;
        
        RenderTextureFormat format = RenderTextureFormat.Default;
        
        _buffer.GetTemporaryRT(_bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);
        Draw(sourceId, _bloomPrefilterId, Pass.BloomPrefilter);
        
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

    private bool DoBloom(int sourceId)
    {
        PostFXSettings.BloomSettings bloom = _settings.Bloom;
        
        int width = _camera.pixelWidth / 2;
        int height = _camera.pixelHeight / 2;

        int resScale = bloom.halfRes ? 2 : 1;

        if (bloom.maxIterations == 0 || bloom.intensity <= 0f ||
            height < bloom.downscaleLimit * resScale || width < bloom.downscaleLimit * resScale)
        {
            return false;
        }
        
        _buffer.BeginSample("Bloom");

        Vector4 threshold;
        threshold.x = Mathf.GammaToLinearSpace(bloom.threshold);
        threshold.y = threshold.x * bloom.thresholdKnee;
        threshold.z = 2f * threshold.y;
        threshold.w = .25f / (threshold.y + 0.00001f);
        threshold.y -= threshold.x;
        _buffer.SetGlobalVector(_bloomThresholdId, threshold);
        
        RenderTextureFormat format = _useHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        _buffer.GetTemporaryRT(_bloomPrefilterId, width, height, 0, FilterMode.Bilinear, format);

        Draw(sourceId, _bloomPrefilterId, 
            bloom.fadeFireflies ? Pass.BloomPrefilterFireflies : Pass.BloomPrefilter);
        
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

        Pass combinePass;
        Pass finalPass;
        float finalIntensity;
        if (bloom.mode == PostFXSettings.BloomSettings.Mode.Additive)
        {
            combinePass = finalPass = Pass.BloomAdd;
            _buffer.SetGlobalFloat(_bloomIntensityId, 1);
            finalIntensity = bloom.intensity;
        }
        else
        {
            combinePass = Pass.BloomScatter;
            finalPass = Pass.BloomScatterFinal;
            _buffer.SetGlobalFloat(_bloomIntensityId, bloom.scatter);
            finalIntensity = Mathf.Min(bloom.intensity, 0.95f);
        }
        
        
        if (i > 1)
        {
            _buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
        
            for (i -= 1; i > 0; i--)
            {
                _buffer.SetGlobalTexture(_fxSource2Id, toId + 1);
                Draw(fromId, toId, combinePass);
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
        
        _buffer.SetGlobalFloat(_bloomIntensityId, finalIntensity);
        _buffer.SetGlobalTexture(_fxSource2Id, sourceId);
        _buffer.GetTemporaryRT(_bloomResultId, _camera.pixelWidth, _camera.pixelHeight, 0, FilterMode.Bilinear, format);
        
        Draw(fromId, _bloomResultId, combinePass);
        _buffer.ReleaseTemporaryRT(fromId);
        
        _buffer.EndSample("Bloom");
        return true;
    }

    private void ConfigureColorAdjustments()
    {
        ColorAdjustmentsSettings colorAdjustments = _settings.ColorAdjustments;
        _buffer.SetGlobalVector(_colorAdjustmentsId, new Vector4(
            Mathf.Pow(2f, colorAdjustments.postExposure),
            colorAdjustments.contrast * 0.01f + 1f,
            colorAdjustments.hueShift * (1f / 360f),
            colorAdjustments.saturation * 0.01f + 1f
            ));
        _buffer.SetGlobalColor(_colorFilterId, colorAdjustments.colorFilter.linear);
    }

    private void ConfigureWhiteBalance()
    {
        WhiteBalanceSettings whiteBalance = _settings.WhiteBalance;
        _buffer.SetGlobalVector(_whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(
            whiteBalance.temperature,
            whiteBalance.tint));
    }

    private void ConfigureSplitToning()
    {
        SplitToningSettings splitToning = _settings.SplitToning;
        
        Color splitColor = splitToning.shadows;
        splitColor.a = splitToning.balance * 0.01f;
        
        _buffer.SetGlobalColor(_splitToningShadowsId, splitColor);
        _buffer.SetGlobalColor(_splitToningHighlightId, splitToning.highlights);
    }

    private void ConfigureChannelMixer()
    {
        ChannelMixerSettings channelMixer = _settings.ChannelMixer;
        _buffer.SetGlobalVector(_channelMixerRedId, channelMixer.red);
        _buffer.SetGlobalVector(_channelMixerGreenId, channelMixer.green);
        _buffer.SetGlobalVector(_channelMixerBlueId, channelMixer.blue);
    }

    private void ConfigureShadowsMidtonesHighlights()
    {
        ShadowsMidtonesHigtlightsSettings smh = _settings.shadowsMidtonesHigtlights;
        _buffer.SetGlobalColor(_smhShadowsId, smh.shadows.linear);
        _buffer.SetGlobalColor(_smhMidtonesId, smh.midtones.linear);
        _buffer.SetGlobalColor(_smhHighlightId, smh.highlights.linear);
        _buffer.SetGlobalVector(_smhRangeId, new Vector4(
            smh.shadowsStart, smh.shadowsEnd, smh.highlightStart, smh.highLightsEnd));
    }

    private void DoColorGradingAndToneMapping(int sourceId)
    {
        ConfigureColorAdjustments();
        ConfigureWhiteBalance();
        ConfigureSplitToning();
        ConfigureChannelMixer();
        ConfigureShadowsMidtonesHighlights();

        int lutHeight = _colorLUTResolution;
        int lutWidth = lutHeight * lutHeight;
        _buffer.GetTemporaryRT(_colorGradingLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
        
        // ToneMappingSettings.Mode mode = _settings.ToneMapping.mode;
        // // Pass pass = mode < 0 ? Pass.Copy : Pass.ToneMappingACES + (int)mode;
        // Pass pass = Pass.ColorGradingNone + (int)mode;
        // Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
        
        _buffer.SetGlobalVector(_ColorGradingLUTParametersId, new Vector4(
            lutHeight, 0.5f / lutWidth, 0.5f / lutHeight, lutHeight / (lutHeight - 1)));
        
        ToneMappingSettings.Mode mode = _settings.ToneMapping.mode;
        Pass pass = Pass.ColorGradingNone + (int)mode;
        _buffer.SetGlobalFloat(_colorGradingLUTInLogId, _useHDR && pass != Pass.ColorGradingNone ? 1f : 0f);
        Draw(sourceId, _colorGradingLUTId, pass);
        _buffer.SetGlobalVector(_ColorGradingLUTParametersId, new Vector4(
            1f / lutWidth, 1f / lutHeight, lutHeight -1));
        // Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.ColorGradingFinal);
        DrawFinal(sourceId);
        _buffer.ReleaseTemporaryRT(_colorGradingLUTId);
    }
}
