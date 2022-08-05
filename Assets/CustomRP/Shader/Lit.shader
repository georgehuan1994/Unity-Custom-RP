Shader "Custom RP/Lit"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.5,0.5,0.5,1)
        _BaseMap("Texture", 2D) = "white" {}
    	
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
    	
        [Toggle(_PREMULTIPLY_ALPHA)] _Premultiply_Alpha("PreMultiply Alpha", Float) = 0
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    	
    	[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission Color", Color) = (0.0, 0.0, 0.0, 0.0)
    	
        [Enum(UnityEngine.Rendering.BlendMode)]_SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
    	
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows("Shadows", Float) = 0
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
    	
    	[HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "LitInput.hlsl"
		ENDHLSL
        
        Pass
        {
            Tags { "LightMode" = "CustomLit" }  // 自定义光照标签
            
            Blend [_SrcBlend] [_DstBlend]       // 源颜色 (该片元产生的颜色) * SrcFactor + 目标颜色 (已经存在于颜色缓存的颜色) * DstFactor
            ZWrite [_ZWrite]                    // 深度写入开关
            
            HLSLPROGRAM
            #pragma target 3.5                  // 不支持 WebGL 1.0 和 OpenGL ES 2.0
            #pragma shader_feature _CLIPPING    // 是否使用 Alpha Clip，不能在材质中同时使用透明度混合和 Alpha 剔除，前者不写入深度，后者写入
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma shader_feature _PREMULTIPLY_ALPHA   // 是否使用预乘 Alpha，开启可以有效的模拟玻璃效果
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _SHADOW_MASK_DISTANCE
            #pragma multi_compile _ LIGHTMAP_ON // 是否使用光照贴图
            #pragma multi_compile_instancing    // GPU Instancing 指令：生成两个变体：一个支持 GPU 实例化，一个不支持
            #pragma vertex LitPassVertex        // Lit Pass 顶点着色器
            #pragma fragment LitPassFragment    // Lit Pass 片元着色器
            #include "LitPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            
            ColorMask 0     // 禁用写颜色数据，只需要写深度
            
            HLSLPROGRAM
            #pragma target 3.5
            // #pragma shader_feature _CLIPPING
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex        // ShadowCaster Pass 顶点着色器
            #pragma fragment ShadowCasterPassFragment    // ShadowCaster Pass 片元着色器
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
        
        Pass {
			Tags { "LightMode" = "Meta" }

			Cull Off

			HLSLPROGRAM
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "MetaPass.hlsl"					// 不能放在前面
			ENDHLSL
		}
        
    }
    
    CustomEditor "CustomShaderGUI"
}
