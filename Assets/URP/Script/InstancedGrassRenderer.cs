using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class InstancedGrassRenderer : MonoBehaviour
{
    [SerializeField] private Mesh mesh = null;
    [SerializeField] private Material material = null;
    
    private Mesh _cachedGrassMesh;
    private Matrix4x4[] _matrices = new Matrix4x4[1023];
    
    /// <summary>
    /// 获取草的网格
    /// </summary>
    /// <returns></returns>
    private Mesh GetGrassMeshCache()
    {
        if (!_cachedGrassMesh)
        {
            // 创建 3 个顶点的 mesh
            _cachedGrassMesh = new Mesh();

            // single grass (vertices)
            Vector3[] verts = new Vector3[3];
            verts[0] = new Vector3(-0.25f, 0);
            verts[1] = new Vector3(+0.25f, 0);
            verts[2] = new Vector3(-0.0f, 1);
            
            // single grass (Triangle index)
            int[] triangles = new int[3] { 2, 1, 0, }; // order to fit Cull Back in grass shader

            _cachedGrassMesh.SetVertices(verts);
            _cachedGrassMesh.SetTriangles(triangles, 0);
        }

        return _cachedGrassMesh;
    }

    private void Awake()
    {
        if (mesh == null)
        {
            mesh = GetGrassMeshCache();
        }
        
        if (material == null)
        {
            material = default;
        }

        for (int i = 0; i < _matrices.Length; i++)
        {
            _matrices[i] = Matrix4x4.TRS(new Vector3(Random.value,0,Random.value) * 10f, Quaternion.identity, Vector3.one); 
        }
    }

    private void LateUpdate()
    {
        Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023);
    }
}
