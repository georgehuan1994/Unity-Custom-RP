using UnityEngine;
using UnityEngine.UI;

public class SimpleCS : MonoBehaviour
{
    [SerializeField] private ComputeShader computeShader = null;
    
    private Image _image = null;
    private RenderTexture _rt = null;
    private int _kernelIndex = 0;

    private void InitShader()
    {
        _image = GetComponent<Image>();
        _kernelIndex = computeShader.FindKernel("CSMain");
        int width = 1024, height = 1024;
        _rt = new RenderTexture(width, height, 0) {enableRandomWrite = true};
        _rt.Create();
        
        _image.material.SetTexture("_MainTex", _rt);
        computeShader.SetTexture(_kernelIndex, "Result", _rt);
        computeShader.Dispatch(_kernelIndex, width / 8, height / 8, 1);
    }

    private void Start()
    {
        InitShader();
    }
}
