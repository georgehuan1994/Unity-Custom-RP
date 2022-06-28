using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
    private static int baseColorId = Shader.PropertyToID("_BaseColor");
    private static int cutoffId = Shader.PropertyToID("_Cutoff");

    [SerializeField] private Color baseColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float cutoff = 0.5f; 

    private static MaterialPropertyBlock _block;

    private void OnValidate()
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
        
        _block.SetColor(baseColorId, baseColor);
        _block.SetFloat(cutoffId, cutoff);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    private void Awake()
    {
        OnValidate();
    }
}
