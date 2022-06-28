using UnityEngine;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    private static int _baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private Mesh mesh = default;
    [SerializeField] private Material material = default;

    private Matrix4x4[] _matrices = new Matrix4x4[1023];
    private Vector4[] _baseColors = new Vector4[1023];

    private MaterialPropertyBlock _block;

    private void Awake()
    {
        for (int i = 0; i < _matrices.Length; i++)
        {
            // 创建一个变换矩阵：
            // 位置在半径为 10 的球体内随机，不旋转，不缩放
            _matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one);
            _baseColors[i] = new Vector4(Random.value, Random.value, 1f);
            
            // 创建一个变换矩阵：
            // 位置在半径为 10 的球体内随机，各轴随机旋转 0~360，随机缩放 0.5~1.5
            // _matrices[i] = Matrix4x4.TRS(
            //     Random.insideUnitSphere * 10f,
            //     Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
            //     Vector3.one * Random.Range(0.5f, 1.5f));
            // _baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
        }
    }

    private void Update()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
            _block.SetVectorArray(_baseColorId, _baseColors);
        }

        Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block);
    }
}
