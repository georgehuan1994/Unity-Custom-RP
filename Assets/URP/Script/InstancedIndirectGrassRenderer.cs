using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Serialization;

[ExecuteAlways]
public class InstancedIndirectGrassRenderer : MonoBehaviour
{
    public static InstancedIndirectGrassRenderer Instance;
    
    [Header("Settings")]
    public float drawDistance = 150;            // 绘制距离，从摄像机的坐标开始
    public Material instanceMaterial;           // 材质实例
    
    [Header("Internal")]
    public ComputeShader cullingComputeShader;  // 剔除算法

    [NonSerialized]
    public List<Vector3> allGrassPos = new List<Vector3>(); // 每根草的位置

    public int boundSize = -1;                  // 边界尺寸
    
    private float _cellSizeX = 10;
    private float _cellSizeZ = 10;              // 分为 10 * 10 个绘制区域，单位：米
    
    private int _cellCountX = -1;
    private int _cellCountZ = -1;               // 每个绘制区域中草的数量 (自适应)
    
    private int _dispatchCount = -1;            // Compute Shader 内核线程数
    
    private int _cacheInstanceCount = -1;       // 当前实例数量
    private Mesh _cacheGrassMesh;               // 当前实例网格
    private int _cacheBoundSize = -1;           // 当前边界尺寸

    private ComputeBuffer _allInstancesPosWSBuffer;             // 储存每根草的位置
    private ComputeBuffer _visibleInstancesOnlyPosWSIDBuffer;   // 储存每根草的实例ID
    private ComputeBuffer _argsBuffer;                          // 存储每根草的其他参数

    private List<Vector3>[] _cellPosWSsList;    // 绘制区域列表，每个列表中储存的草的坐标
    private float _minX, _minZ, _maxX, _maxZ;   // 草在世界空间中的位置限制
    
    private List<int> _visibleCellIDList = new List<int>();     // 每个绘制区域的ID
    
    // 视锥平面: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
    private Plane[] _cameraFrustumPlanes = new Plane[6];

    private bool _shouldBatchDispatch = true;

    /// <summary>
    /// 创建材质网格
    /// </summary>
    /// <returns></returns>
    private Mesh GetGrassMeshCache()
    {
        if (!_cacheGrassMesh)
        {
            _cacheGrassMesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-0.25f, 0);
            verts[1] = new Vector3(+0.25f, 0);
            verts[2] = new Vector3(-0.00f, 1);

            int[] triangles = new int[3] {2, 1, 0};
            
            _cacheGrassMesh.SetVertices(verts);
            _cacheGrassMesh.SetTriangles(triangles, 0);
        }

        return _cacheGrassMesh;
    }
    
    /// <summary>
    /// 更新所有实例的 Transform 缓存
    /// </summary>
    private void UpdateAllInstanceTransformBufferIfNeeded()
    {
        // 设置材质属性
        instanceMaterial.SetVector("_PivotPosWS", transform.position);
        instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));

        // 如果实例已绘制完毕，则无需更新
        if (_cacheBoundSize == boundSize &&
            _cacheInstanceCount == allGrassPos.Count &&
            _argsBuffer != null &&
            _allInstancesPosWSBuffer != null &&
            _visibleInstancesOnlyPosWSIDBuffer != null)
        {
            return;
        }
        
        Debug.Log("正在更新草地实例 (Slow)...");
        
        _allInstancesPosWSBuffer?.Release();
        _allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3);

        _visibleInstancesOnlyPosWSIDBuffer?.Release();
        _visibleInstancesOnlyPosWSIDBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(uint), ComputeBufferType.Append);

        _minX = float.MaxValue;
        _minZ = float.MaxValue;
        _maxX = float.MinValue;
        _maxZ = float.MinValue;

        // 获取草的位置范围，allGrassPos 在 InstancedIndirectGrassPosDefine.UpdatePosAndSizeIfNeeded() 中赋值
        for (int i = 0; i < allGrassPos.Count; i++)
        {
            Vector3 target = allGrassPos[i];
            _minX = Mathf.Min(target.x, _minX);
            _minZ = Mathf.Min(target.z, _minZ);
            _maxX = Mathf.Max(target.x, _maxX);
            _maxZ = Mathf.Max(target.z, _maxZ);
        }

        // 获取每个绘制区域的尺寸
        _cellCountX = Mathf.CeilToInt((_maxX - _minX) / _cellSizeX);
        _cellCountZ = Mathf.CeilToInt((_maxZ - _minZ) / _cellSizeZ);

        // 划分区域
        _cellPosWSsList = new List<Vector3>[_cellCountX * _cellCountZ];
        for (int i = 0; i < _cellPosWSsList.Length; i++)
        {
            _cellPosWSsList[i] = new List<Vector3>();
        }

        // 遍历每根草，将草的坐标填充到各个绘制区域中
        for (int i = 0; i < allGrassPos.Count; i++)
        {
            Vector3 pos = allGrassPos[i];

            // 反插值，[0 ~ (cellCount-1)]，转换为列表的索引
            int xID = Mathf.Min(_cellCountX - 1, Mathf.FloorToInt(Mathf.InverseLerp(_minX, _maxX, pos.x) * _cellCountX));
            int zID = Mathf.Min(_cellCountZ - 1, Mathf.FloorToInt(Mathf.InverseLerp(_minZ, _maxZ, pos.z) * _cellCountZ));
            
            _cellPosWSsList[xID + zID * _cellCountX].Add(pos);
        }

        // 按区域，将每根草的坐标填充到一个数组中
        int offset = 0;
        Vector3[] allGrassPosWSSortedByCell = new Vector3[allGrassPos.Count];
        for (int i = 0; i < _cellPosWSsList.Length; i++)
        {
            for (int j = 0; j < _cellPosWSsList[i].Count; j++)
            {
                allGrassPosWSSortedByCell[offset] = _cellPosWSsList[i][j];
                offset++;
            }
        }
        
        _allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);
        instanceMaterial.SetBuffer("_AllInstancesTransformBuffer", _allInstancesPosWSBuffer);
        instanceMaterial.SetBuffer("_VisibleInstanceOnlyTransformIDBuffer", _visibleInstancesOnlyPosWSIDBuffer);

        if (_argsBuffer != null)
        {
            _argsBuffer.Release();
        }

        uint[] args = new uint[5] {0, 0, 0, 0, 0};
        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        args[0] = (uint) GetGrassMeshCache().GetIndexCount(0);  // 参数0：获取某个 submesh 的索引计数
        args[1] = (uint) allGrassPos.Count; // 参数1：草的数量
        args[2] = (uint) GetGrassMeshCache().GetIndexStart(0);  // 参数2：获取某个 submesh 索引缓冲区中的起始索引位置
        args[3] = (uint) GetGrassMeshCache().GetBaseVertex(0);  // 参数3：获取某个 submesh 的顶点索引的偏移
        args[4] = 0;
        
        _argsBuffer.SetData(args);
        
        // Update Cache
        _cacheInstanceCount = allGrassPos.Count;
        _cacheBoundSize = boundSize;
        
        // Set Buffer
        cullingComputeShader.SetBuffer(0, "_AllInstancesPosWSBuffer", _allInstancesPosWSBuffer);
        cullingComputeShader.SetBuffer(0, "_VisibleInstancesOnlyPosWSIDBuffer", _visibleInstancesOnlyPosWSIDBuffer);
    }

    private void LateUpdate()
    {
        UpdateAllInstanceTransformBufferIfNeeded();
        
        // CPU 初步剔除不可见的绘制区域
        _visibleCellIDList.Clear();
        Camera cam = Camera.main;

        float cameraOriginalFarPlane = cam.farClipPlane;
        cam.farClipPlane = drawDistance;
        GeometryUtility.CalculateFrustumPlanes(cam, _cameraFrustumPlanes);
        cam.farClipPlane = cameraOriginalFarPlane;
        
        Profiler.BeginSample("CPU cell frustum culling (heavy)");
        for (int i = 0; i < _cellPosWSsList.Length; i++)
        {
            // 创建每个绘制区域的包围盒
            Vector3 centerPosWS = new Vector3(i % _cellCountX + 0.5f, 0, i / _cellCountX + 0.5f);
            centerPosWS.x = Mathf.Lerp(_minX, _maxX, centerPosWS.x / _cellCountX);
            centerPosWS.z = Mathf.Lerp(_minZ, _maxZ, centerPosWS.z / _cellCountZ);
            Vector3 sizeWS = new Vector3(Mathf.Abs(_maxX - _minX) / _cellCountX, 0, Mathf.Abs(_maxX - _minX) / _cellCountX);
            Bounds cellBound = new Bounds(centerPosWS, sizeWS);

            // 检测绘制区域是否在视锥平面内
            // if (GeometryUtility.TestPlanesAABB(_cameraFrustumPlanes, cellBound))
                _visibleCellIDList.Add(i);
        }
        Profiler.EndSample();
        
        // 
        Matrix4x4 v = cam.worldToCameraMatrix;
        Matrix4x4 p = cam.projectionMatrix;
        Matrix4x4 vp = p * v;
        
        _visibleInstancesOnlyPosWSIDBuffer.SetCounterValue(0);
        
        cullingComputeShader.SetMatrix("_VPMatrix", vp);
        cullingComputeShader.SetFloat("_MaxDrawDistance", drawDistance);

        // 处理每个绘制区域
        _dispatchCount = 0;
        for (int i = 0; i < _visibleCellIDList.Count; i++)
        {
            int targetCellFlattenID = _visibleCellIDList[i];
            int memoryOffset = 0;
            for (int j = 0; j < targetCellFlattenID; j++)
            {
                memoryOffset += _cellPosWSsList[j].Count;
            }
            cullingComputeShader.SetInt("_StartOffset", memoryOffset);
            int jobLength = _cellPosWSsList[targetCellFlattenID].Count;

            if (_shouldBatchDispatch)
            {
                while ((i < _visibleCellIDList.Count - 1) &&
                       (_visibleCellIDList[i + 1] == _visibleCellIDList[i] + 1))
                {
                    jobLength += _cellPosWSsList[_visibleCellIDList[i + 1]].Count;
                    i++;
                }
            }
            
            // disaptch.X division number must match numthreads.x in compute shader (e.g. 64)
            cullingComputeShader.Dispatch(0, Mathf.CeilToInt(jobLength / 64f), 1, 1);
            _dispatchCount++;
        }
        
        ComputeBuffer.CopyCount(_visibleInstancesOnlyPosWSIDBuffer, _argsBuffer, 4);

        Bounds renderBound = new Bounds();
        renderBound.SetMinMax(new Vector3(_minX, 0, _minZ), new Vector3(_maxX, 0, _maxZ));
        Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), 0, instanceMaterial, renderBound, _argsBuffer);
    }
    
    private void OnEnable()
    {
        Instance = this;
    }

    private void OnDisable()
    {
        _allInstancesPosWSBuffer?.Release();
        _allInstancesPosWSBuffer = null;
        
        _visibleInstancesOnlyPosWSIDBuffer?.Release();
        _visibleInstancesOnlyPosWSIDBuffer = null;
        
        _argsBuffer?.Release();
        _argsBuffer = null;

        Instance = null;
    }
}
