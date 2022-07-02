using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class InstancedIndirectGrassPosDefine : MonoBehaviour
{
    [Range(1, 40000000)] public int instanceCount = 1000000;
    
    [Range(1, 40), Min(1)] public int boundSize = 4;
    
    public float drawDistance = 150;
    
    private int _cacheCount = -1;

    private int _cacheBoundSize = -1;

    private void Start()
    {
        UpdatePosAndSizeIfNeeded();
    }

    private void Update()
    {
        UpdatePosAndSizeIfNeeded();
    }

    private void UpdatePosAndSizeIfNeeded()
    {
        if (instanceCount == _cacheCount && _cacheBoundSize == boundSize)
        {
            return;
        }
        
        Debug.Log("Update Position...");
        
        UnityEngine.Random.InitState(123);

        float scale = Mathf.Sqrt((instanceCount / boundSize)) / 2f;
        transform.localScale = new Vector3(scale, transform.localScale.y, scale);

        List<Vector3> positions = new List<Vector3>(instanceCount);
        for (int i = 0; i < instanceCount; i++)
        {
            Vector3 pos = Vector3.zero;

            pos.x = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.x;
            pos.z = UnityEngine.Random.Range(-1f, 1f) * transform.lossyScale.z;

            pos += transform.position;
            
            positions.Add(new Vector3(pos.x, pos.y, pos.z));
        }

        InstancedIndirectGrassRenderer.Instance.allGrassPos = positions;
        InstancedIndirectGrassRenderer.Instance.boundSize = boundSize;
        
        _cacheCount = positions.Count;
        _cacheBoundSize = boundSize;
    }
}
