/* Shadows.cs */

using UnityEngine;
using UnityEngine.Rendering;

public class Shadows  // 在 Lighting 实例化并持有
{
    private const int MAXShadowedDirectionalLightCount = 1; // 可投射阴影的平行光数量
    
    private const string BufferName = "Shadows";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName
    };

    private ScriptableRenderContext _context;   // 持有 context，方便在此 ExecuteCommandBuffer

    private CullingResults _cullingResults;

    private ShadowSettings _settings;
    
    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
    }

    private ShadowedDirectionalLight[] _shadowedDirectionalLights =
        new ShadowedDirectionalLight[MAXShadowedDirectionalLightCount];

    public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
    {
        _context = context;
        _cullingResults = cullingResults;
        _settings = settings;
    }

    private void ExecuteBuffer()
    {
        _context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
    }
}
