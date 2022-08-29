using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack
{
    private ScriptableRenderContext _context;
    
    private Camera _camera;

    private PostFXSettings _settings;
    
    private const string BufferName = "Post FX";

    private CommandBuffer _buffer = new CommandBuffer
    {
        name = BufferName,
    };
    
    // 当前 stack 是否处于活动状态
    public bool IsActive => _settings != null;

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings)
    {
        _context = context;
        _camera = camera;
        _settings = settings;
    }

    public void Render(int sourceId)
    {
        _buffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
        _context.ExecuteCommandBuffer(_buffer);
        _buffer.Clear();
        
    }
}
