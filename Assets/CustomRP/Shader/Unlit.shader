Shader "Custom RP/Unlit"
{
    Properties
    {
        [HDR] _BaseColor("Color", Color) = (1,1,1,1)
        _BaseMap("Texture", 2D) = "white" {}
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "UnlitInput.hlsl"
		ENDHLSL
        
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]   // 源颜色 (该片元产生的颜色) * SrcFactor + 目标颜色 (已经存在于颜色缓存的颜色) * DstFactor
            ZWrite [_ZWrite]                // 深度写入开关
            
            HLSLPROGRAM
            
            #include "UnlitPass.hlsl"

            #pragma shader_feature _CLIPPING    // 是否使用 Alpha Clip，不能在材质中同时使用透明度混合和 Alpha 剔除，前者不写入深度，后者写入
            #pragma multi_compile_instancing    // GPU Instancing 指令：生成两个变体：一个支持 GPU 实例化，一个不支持
            #pragma vertex UnlitPassVertex      // Unlit Pass 顶点着色器
            #pragma fragment UnlitPassFragment  // Unlit Pass 片元着色器
            
            ENDHLSL
        }
        
        Pass {
			Tags { "LightMode" = "Meta" }

			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "MetaPass.hlsl"
			ENDHLSL
		}
    }
    
    CustomEditor "CustomShaderGUI"
}
