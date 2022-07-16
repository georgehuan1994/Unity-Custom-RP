using System.Runtime.InteropServices;
using UnityEngine;

public class ParticlesManager : MonoBehaviour {
    
    /// <summary>
    /// 粒子数据
    /// </summary>
    public struct Particle
    {
        public Vector2 position;    // 粒子位置
        public Vector2 velocity;    // 粒子速度
    }
    
    private int _kernelIndex;                   // kernel 索引

    public ComputeShader computeShader;
    public Material material;                   // 材质
    public int particlesCount = 1024000;        // 粒子数量
    
    private ComputeBuffer _computeBuffer;       // Compute Buffer
    private int _particleBufferSize;            // 单个 Particle 结构体的占用大小
    private Particle[] _particleBufferData;     // 初始 Particle 数据
    
    private const int WarpSize = 1024;          // 单个线程组中的线程数
    private int _warpCount;                     // 所需线程组的数量
    
    void Start () 
    {
        _kernelIndex = computeShader.FindKernel("Update");
        
        _particleBufferSize = Marshal.SizeOf(typeof(Particle));

        _computeBuffer = new ComputeBuffer(particlesCount, _particleBufferSize, ComputeBufferType.Default);

        _particleBufferData = new Particle[particlesCount];
        
        for (int i = 0; i < particlesCount; i++)
        {
            _particleBufferData[i] = new Particle
            {
                position = Random.insideUnitCircle * 10f,   // 位置：在半径为 10 的圆内随机
                velocity = Vector2.zero
            };
        }

        _computeBuffer.SetData(_particleBufferData);
        
        // 传递 Compute Buffer 到 Compute Shader 当中
        computeShader.SetBuffer(_kernelIndex, "Particles", _computeBuffer);

        // 传递 Compute Buffer 到 Shader 当中
        material.SetBuffer("Particles", _computeBuffer);
        
        // 应当总是将线程组的数量设置为 warp size 的整数倍
        _warpCount = Mathf.CeilToInt((float)particlesCount / WarpSize);
    }
    
	void Update () 
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            _computeBuffer.SetData(_particleBufferData);
        }

        computeShader.SetInt("shouldMove", Input.GetMouseButton(0) ? 1 : 0);
        var mousePosition = GetMousePosition();
        computeShader.SetFloats("mousePosition", mousePosition);
        computeShader.SetFloat("deltaTime", Time.deltaTime);
        
        computeShader.Dispatch(_kernelIndex, _warpCount, 1, 1);
	}
    
    void OnRenderObject()
    {
        // 激活给定的 Pass 以进行渲染
        material.SetPass(0);
        
        // 在 GPU 上执行 Draw Call，第一个参数是拓扑结构，第二个参数数顶点数，第三个参数是实例数
        Graphics.DrawProceduralNow(MeshTopology.Points, 1, particlesCount);
    }

    void OnDestroy()
    {
        if (_computeBuffer != null)
            _computeBuffer.Release();
    }
    
    /// <summary>
    /// 获取鼠标的屏幕坐标
    /// </summary>
    /// <returns></returns>
    float[] GetMousePosition()
    {
        var mp = Input.mousePosition;
        var v = Camera.main.ScreenToWorldPoint(mp);
        return new float[] { v.x, v.y };
    }
    
    /*private void OnDrawGizmos()
    {
        var p = new Particle[size];
        particles.GetData(p);
        for (int i = 0; i < size; i++)
        {
            Gizmos.DrawSphere(p[i].position, 0.1f);
        }
    }*/
}
