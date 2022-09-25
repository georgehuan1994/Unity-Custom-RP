Shader "Hidden/Custom RP/Post FX Stack"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/UnityInput.hlsl"
        #include "PostFXStackPasses.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "FXAA"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #include "FXAAPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "FXAA Whit Luma"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FXAAPassFragment
            #define FXAA_ALPHA_CONTAINS_LUMA
            #pragma multi_compile _ FXAA_QUALITY_MEDIUM FXAA_QUALITY_LOW
            #include "FXAAPass.hlsl"
            ENDHLSL
        }
        
        Pass
        {
            Name "Final Rescale"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment FinalPassFragmentRescale
            ENDHLSL
        }
        
        Pass
        {
            Name "Apply ColorGrading"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ApplyColorGradingPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Apply ColorGrading With Luma"
            
            Blend [_FinalSrcBlend] [_FinalDstBlend]
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ApplyColorGradingWithLumaPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ColorGrading None"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradeNonePassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ColorGrading ACES"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradeACESPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ColorGrading Neutral"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradeNeutralPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "ColorGrading Reinhard"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment ColorGradeReinhardPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom ScatterFinal"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterFinalPassFragment
            ENDHLSL
        }
        
        Pass
        {
            Name "Bloom Scatter"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomScatterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom PrefilterFireflies"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterFirefliesPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomPrefilterPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Combine"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomCombinePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Vertical"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomVerticalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Horizontal"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment BloomHorizontalPassFragment
            ENDHLSL
        }

        Pass
        {
            Name "Copy"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DefaultPassVertex
            #pragma fragment CopyPassFragment
            ENDHLSL
        }
    }
}