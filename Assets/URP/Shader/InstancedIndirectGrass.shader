Shader "Grass/InstancedIndirectGrass"
{
    Properties
    {
        [MainColor]
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _BaseColorTexture("Base Color Texture", 2D) = "white" {}
        _GroundColor("Ground Color", Color) = (0.5,0.5,0.5)
        
        [Header(Grass Shape)]
        _GrassWidth("Grass Width", Float) = 1.0
        _GrassHeight("Grass Height", Float) = 1.0
        
        [Header(Wind)]
        _WindAIntensity("WindA Intensity", Float) = 1.77
        _WindAFrequency("WindA Frequency", Float) = 4
        _WindATiling("WindA Tilting", Vector) = (0.1,0.1,0)
        _WindAWrap("WindA Wrap", Vector) = (0.5,0.5,0)
        
        _WindBIntensity("_WindB Intensity", Float) = 0.25
        _WindBFrequency("_WindB Frequency", Float) = 7.7
        _WindBTiling("_WindB Tiling", Vector) = (.37,3,0)
        _WindBWrap("_WindB Wrap", Vector) = (0.5,0.5,0)
        
        _WindCIntensity("_WindC Intensity", Float) = 0.125
        _WindCFrequency("_WindC Frequency", Float) = 11.7
        _WindCTiling("_WindC Tiling", Vector) = (0.77,3,0)
        _WindCWrap("_WindC Wrap", Vector) = (0.5,0.5,0)
        
        [Header(Lighting)]
        _RandomNormal("RandomNormal", Float) = 0.15
        
        [Header(SRP Batching)]
        _PivotPosWS("PivotPosWS", Vector) = (0,0,0,0)   // GameObject Position
        _BoundSize("BoundSize", Vector) = (1,1,0)       // GameObject LocalScale X,Y
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalRenderPipeline" }

        Pass
        {
            Cull Back
            Ztest Less
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            
            #pragma vertex GrassPassVertex
            #pragma fragment GrassPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;   //
                half3 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial) // Define GPU Instance CBUFFER
            float3 _PivotPosWS;
            float2 _BoundSize;

            float _GrassWidth;
            float _GrassHeight;

            float _WindAIntensity;
            float _WindAFrequency;
            float2 _WindATiling;
            float2 _WindAWrap;

            float _WindBIntensity;
            float _WindBFrequency;
            float2 _WindBTiling;
            float2 _WindBWrap;

            float _WindCIntensity;
            float _WindCFrequency;
            float2 _WindCTiling;
            float2 _WindCWrap;

            half3 _BaseColor;
            float4 _BaseColorTexture_ST;
            half3 _GroundColor;

            half _RandomNormal;

            StructuredBuffer<float3> _AllInstancesTransformBuffer;
            StructuredBuffer<uint> _VisibleInstanceOnlyTransformIDBuffer;
            CBUFFER_END
            
            sampler2D _GrassBendingRT;
            sampler2D _BaseColorTexture;

            half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half positionOSY)
            {
                half3 H = normalize(light.direction + V);

                half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;   // 平行光漫射模型: 半兰伯特
                float directSpecular = saturate(dot(N,H));  // 平行光镜反

                // pow(directSpecular,8)
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;

                directSpecular *= 0.1 * positionOSY;    // 只在草的顶部应用高光，模拟环境光遮蔽

                half3 lightColor = light.color * (light.shadowAttenuation * light.distanceAttenuation);
                half3 result = (albedo * directDiffuse + directSpecular) * lightColor;
                return result;
            }

            Varyings GrassPassVertex(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                // we pre-transform to posWS in C# now
                float3 perGrassPivotPosWS = _AllInstancesTransformBuffer[_VisibleInstanceOnlyTransformIDBuffer[instanceID]];

                // 草高度
                float perGrassHeight = lerp(2, 5, (sin(perGrassPivotPosWS.x * 23.4643 + perGrassPivotPosWS.z) * 0.45 + 0.55)) * _GrassHeight;

                // 用包围盒在贴图上取 UV
                float2 grassBendingUV = ((perGrassPivotPosWS.xz - _PivotPosWS.xz) / _BoundSize) * 0.5 + 0.5;
                float stepped = tex2Dlod(_GrassBendingRT, float4(grassBendingUV, 0, 0)).x;

                // 让草旋转至面向相机，billboard 效果
                float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;      // 世界空间下，相机右单位向量
                float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;         // 世界空间下，相机上单位向量
                float3 cameraTransformForwardWS = UNITY_MATRIX_V[2].xyz;    // 世界空间下，相机前单位向量

                // 使用 posXZ 获取随机宽度，最小 0.1
                float3 positionOS = IN.positionOS.x * cameraTransformRightWS * _GrassWidth * (sin(perGrassPivotPosWS.x * 95.4643 + perGrassPivotPosWS.z) * 0.45 + 0.55);

                // billboard 高度
                positionOS += IN.positionOS.y * cameraTransformUpWS;

                // bending by RT
                float3 bendDir = cameraTransformForwardWS;
                bendDir.xz *= 0.5;  // 让草在 bending 的时候便短一些，效果更好
                bendDir.y = min(-0.5, bendDir.y);   // 防止相机视野变换时，草变得过长
                positionOS = lerp(positionOS.xyz + bendDir * positionOS.y / -bendDir.y, positionOS.xyz, stepped * 0.95 + 0.05); // 防止完全倒下产生的 ZFighting

                positionOS.y *= perGrassHeight;

                float3 viewWS = _WorldSpaceCameraPos - perGrassPivotPosWS;
                float ViewWSLength = length(viewWS);
                positionOS += cameraTransformForwardWS * IN.positionOS.x * max(0, ViewWSLength * 0.0225);

                // 从模型空间变换到世界空间，已经面向相机了，不需要矩阵变换
                float3 positionWS = positionOS + perGrassPivotPosWS;

                float wind = 0;
                wind += (sin(_Time.y * _WindAFrequency + perGrassPivotPosWS.x * _WindATiling.x + perGrassPivotPosWS.z * _WindATiling.y) * _WindAWrap.x + _WindATiling.x + _WindAWrap.y) * _WindAIntensity;
                wind += (sin(_Time.y * _WindBFrequency + perGrassPivotPosWS.x * _WindBTiling.x + perGrassPivotPosWS.z * _WindBTiling.y) * _WindBWrap.x + _WindBTiling.x + _WindBWrap.y) * _WindBIntensity;
                wind += (sin(_Time.y * _WindCFrequency + perGrassPivotPosWS.x * _WindCTiling.x + perGrassPivotPosWS.z * _WindCTiling.y) * _WindCWrap.x + _WindCTiling.x + _WindCWrap.y) * _WindCIntensity;

                // 将顶点转换到齐次裁剪空间
                OUT.positionCS = TransformWorldToHClip(positionWS);
                
                /////////////////////////////////////////////////////////////////////
                // lighting & color
                /////////////////////////////////////////////////////////////////////

                Light mainLight;
#if _MAIN_LIGHT_SHADOWS
                mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
#else
                mainLight = GetMainLight();
#endif

                // 随机法向增量
                half3 randomAddToN = (_RandomNormal * sin(perGrassPivotPosWS.x * 82.32523 + perGrassPivotPosWS.z) + wind * -0.25) * cameraTransformRightWS;

                // 在世界空间下，草的顶点默认是朝上的，这是一个简单却很有用的小技巧：
                // - 偏转法线让光照不要太统一
                // - 因为要实现 billboard 效果，所以要让 cameraTransformForwardWS 参与到计算中
                half3 N = normalize(half3(0,1,0) + randomAddToN - cameraTransformForwardWS * 0.5);

                half3 V = viewWS / ViewWSLength;

                half3 baseColor = tex2Dlod(_BaseColorTexture, float4(TRANSFORM_TEX(positionWS.xz, _BaseColorTexture),0,0)) * _BaseColor;
                half3 albedo = lerp(_GroundColor, baseColor, IN.positionOS.y);

                half3 lightingResult = SampleSH(0) * albedo;

                lightingResult += ApplySingleDirectLight(mainLight, N, V, albedo, positionOS.y);

#if _ADDITIONAL_LIGHTS
                int additionalLightsCount = GetAdditionalLightsCount(); // 返回每个物体受到的光源
                for (int i = 0; i < additionalLightsCount; ++i) // 循环附加光源
                {
                    // 如果 _ADDITIONAL_LIGHT_SHADOWS 被定义，它也会计算阴影
                    Light light = GetAdditionalLight(i, positionWS);
                    lightingResult += ApplySingleDirectLight(light, N, V, albedo, positionOS.y);
                }
#endif

                // Fog
                float fogFactor = ComputeFogFactor(OUT.positionCS.z);

                OUT.color = MixFog(lightingResult, fogFactor);

                return OUT;
            }

            half4 GrassPassFragment(Varyings IN):SV_Target
            {
                return half4(IN.color, 1);
            }
            
            ENDHLSL
        }
        
        // copy pass, change LightMode to ShadowCaster will make grass cast shadow
        // copy pass, change LightMode to DepthOnly will make grass render into _CameraDepthTexture
    }
}
