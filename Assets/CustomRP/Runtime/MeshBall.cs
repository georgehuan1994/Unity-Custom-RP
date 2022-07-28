using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class MeshBall : MonoBehaviour
{
    private static int _baseColorId = Shader.PropertyToID("_BaseColor");
    private static int _metallicId = Shader.PropertyToID("_Metallic");
    private static int _smoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] private Mesh mesh = default;
    [SerializeField] private Material material = default;
    [SerializeField] private Material cutOffMaterial = default;
    [SerializeField] private bool cutOff = false;
    [SerializeField] private LightProbeProxyVolume _lightProbeProxyVolume = null;

    private Matrix4x4[] _matrices = new Matrix4x4[1023];
    private Vector4[] _baseColors = new Vector4[1023];
    private float[] _metallics = new float[1023];
    private float[] _smoothness = new float[1023];

    private MaterialPropertyBlock _block;

    private void Awake()
    {
        if (cutOff)
        {
            material = cutOffMaterial;
        }
        
        for (int i = 0; i < _matrices.Length; i++)
        {
            if (cutOff)
            {
                // 创建一个变换矩阵：
                // 位置在半径为 10 的球体内随机，各轴随机旋转 0~360，随机缩放 0.5~1.5
                _matrices[i] = Matrix4x4.TRS(
                    Random.insideUnitSphere * 10f,
                    Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                    Vector3.one * Random.Range(0.5f, 1.5f));
                _baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f)); 
            }
            else
            {
                // 创建一个变换矩阵：
                // 位置在半径为 10 的球体内随机，不旋转，不缩放
                _matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, Quaternion.identity, Vector3.one);
                _baseColors[i] = new Vector4(Random.value, Random.value, 1f);
            }
            _metallics[i] = Random.value < 0.25f ? 1f : 0f;
            _smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    private void Update()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
            _block.SetVectorArray(_baseColorId, _baseColors);
            _block.SetFloatArray(_metallicId, _metallics);
            _block.SetFloatArray(_smoothnessId, _smoothness);

            if (!_lightProbeProxyVolume)
            {
                var position = new Vector3[1023];
                for (int i = 0; i < _matrices.Length; i++)
                {
                    position[i] = _matrices[i].GetColumn(3);
                }

                var lightProbes = new SphericalHarmonicsL2[1023];
                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(position, lightProbes, null);
                _block.CopySHCoefficientArraysFrom(lightProbes);  
            }
        }

        // Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block);
        Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block,
            ShadowCastingMode.On, true, 0, null, 
            _lightProbeProxyVolume ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided);
    }
}
