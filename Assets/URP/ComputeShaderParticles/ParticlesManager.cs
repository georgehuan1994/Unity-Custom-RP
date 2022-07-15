using System.Runtime.InteropServices;
using UnityEngine;

public class ParticlesManager : MonoBehaviour {
    
    /// <summary>
    /// 粒子数据
    /// </summary>
    public struct Particle
    {
        public Vector2 position;
        public Vector2 velocity;
    }

    public ComputeShader computeShader;         // 与着色器共用的 Compute Buffer
    public Material material;                   // 材质
    public int particlesCount = 1024000;        // 粒子数量
    
    private ComputeBuffer _particlesBuffer;
    private int _particleBufferSize;            // Particle 数据大小
    private Particle[] _particleBufferData;     // 要传递给 Compute Buffer 的数据
    
    private const int WARP_SIZE = 1024;         // 单个线程组中的线程数
    private int _warpCount;                     // 所需线程组数

    private int _kernelIndex;                   // kernel 索引
    
    /*private void OnDrawGizmos()
    {
        var p = new Particle[size];
        particles.GetData(p);
        for (int i = 0; i < size; i++)
        {
            Gizmos.DrawSphere(p[i].position, 0.1f);
        }
    }*/
    
    void Start () {

        _warpCount = Mathf.CeilToInt((float)particlesCount / WARP_SIZE);

        _particleBufferSize = Marshal.SizeOf(typeof(Particle));
        _particlesBuffer = new ComputeBuffer(particlesCount, _particleBufferSize);

        _particleBufferData = new Particle[particlesCount];

        for (int i = 0; i < particlesCount; i++)
        {
            _particleBufferData[i] = new Particle();
            _particleBufferData[i].position = Random.insideUnitCircle * 10f;
            _particleBufferData[i].velocity = Vector2.zero;
        }

        _particlesBuffer.SetData(_particleBufferData);

        _kernelIndex = computeShader.FindKernel("Update");
        
        computeShader.SetBuffer(_kernelIndex, "Particles", _particlesBuffer);

        material.SetBuffer("Particles", _particlesBuffer);
    }
    
	void Update () {

        if (Input.GetKeyDown(KeyCode.R))
        {
            _particlesBuffer.SetData(_particleBufferData);
        }

        computeShader.SetInt("shouldMove", Input.GetMouseButton(0) ? 1 : 0);
        var mousePosition = GetMousePosition();
        computeShader.SetFloats("mousePosition", mousePosition);
        computeShader.SetFloat("dt", Time.deltaTime);
        computeShader.Dispatch(_kernelIndex, _warpCount, 1, 1);
	}

    float[] GetMousePosition()
    {
        var mp = Input.mousePosition;
        var v = Camera.main.ScreenToWorldPoint(mp);
        return new float[] { v.x, v.y };
    }

    void OnRenderObject()
    {
        material.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Points, 1, particlesCount);
    }

    void OnDestroy()
    {
        if (_particlesBuffer != null)
            _particlesBuffer.Release();
    }
}
