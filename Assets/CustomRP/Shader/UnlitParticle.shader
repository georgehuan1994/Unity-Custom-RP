Shader "Custom RP/Particles/Unlit"
{
    Properties
    {
        [HDR] _BaseColor("Color", Color) = (1,1,1,1)
    	
    	// Particles
    	[Toggle(_VERTEX_COLORS)] _VertexColors ("Vextex Colors", Float) = 0
    	[Toggle(_FLIPBOOK_BLENDING)] _FlipbookBlending ("Flipbook Blending", Float) = 0
    	[Toggle(_NEAR_FADE)] _NearFade ("Near Fade", Float) = 0
    	_NearFadeDistance ("Near Fade Distance", Range(0.0, 10.0)) = 1
    	_NearFadeRange ("Near Fade Range", Range(0.01, 10.0)) = 1
    	[Toggle(_SOFT_PARTICLES)] _SoftParticles ("Soft Particles", Float) = 0
    	_SoftParticlesDistance ("Soft Particles Distance", Range(0.0, 10.0)) = 0
    	_SoftParticlesRange ("Soft Particles Range", Range(0.01, 10.0)) = 1
    	[Toggle(_DISTORTION)] _Distortion ("Distortion", Float) = 0
    	[NoScaleOffset] _DistortionMap ("Distortion Vector", 2D) = "bump"
    	_DistortionStrength ("Distortion Strength", Range(0.0, 0.2)) = 0.1
    	
    	// Base
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
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha   // 源颜色 (该片元产生的颜色) * SrcFactor + 目标颜色 (已经存在于颜色缓存的颜色) * DstFactor
            ZWrite [_ZWrite]                // 深度写入开关
            
            HLSLPROGRAM
            #pragma shader_feature _VERTEX_COLORS		// 是否使用顶点色
            #pragma shader_feature _FLIPBOOK_BLENDING	// 是否使用 Flipbook 混合
            #pragma shader_feature _NEAR_FADE			// 是否使用近平面淡出
            #pragma shader_feature _SOFT_PARTICLES		// 是否使用软粒子
            #pragma shader_feature _DISTORTION			// 是否使用失真纹理
            #pragma shader_feature _CLIPPING    // 是否使用 Alpha Clip，不能在材质中同时使用透明度混合和 Alpha 剔除，前者不写入深度，后者写入
            #pragma multi_compile_instancing    // GPU Instancing 指令：生成两个变体：一个支持 GPU 实例化，一个不支持
            #pragma vertex UnlitPassVertex      // Unlit Pass 顶点着色器
            #pragma fragment UnlitPassFragment  // Unlit Pass 片元着色器
            #include "UnlitPass.hlsl"
            ENDHLSL
        }
    	
    	Pass {
			Tags { "LightMode" = "ShadowCaster" }

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma multi_compile_instancing
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#include "ShadowCasterPass.hlsl"
			ENDHLSL
		}
    }
    
    CustomEditor "CustomShaderGUI"
}
