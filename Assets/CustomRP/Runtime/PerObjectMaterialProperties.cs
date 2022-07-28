using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int _baseColorId = Shader.PropertyToID("_BaseColor");
    private static int _cutoffId = Shader.PropertyToID("_Cutoff");
    private static int _metallicId = Shader.PropertyToID("_Metallic");
    private static int _smoothnessId = Shader.PropertyToID("_Smoothness");
    private static int _emissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float cutoff = 0.5f; 
    [SerializeField, Range(0f, 1f)] private float metallic = 0; 
    [SerializeField, Range(0f, 1f)] private float smoothness = 0.5f; 
    [SerializeField, ColorUsage(false, true)] private Color emissionColor = Color.black; 

    private static MaterialPropertyBlock _block;

    private void OnValidate()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
        
        _block.SetColor(_baseColorId, baseColor);
        _block.SetFloat(_cutoffId, cutoff);
        _block.SetFloat(_metallicId, metallic);
        _block.SetFloat(_smoothnessId, smoothness);
        _block.SetColor(_emissionColorId, emissionColor);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    private void Awake()
    {
        OnValidate();
    }
}
