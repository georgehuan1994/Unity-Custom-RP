Shader "Unlit/GrassLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseColorTexture("Base Color Texture", 2D) = "white" {}
        _GroundColor("Ground Color", Color) = (0.5,0.5,0.5)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalRenderPipeline"}

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM

            half3 _BaseColor;
            float4 _BaseColorTexture_ST;
            half3 _GroundColor;

            sampler2D _BaseColorTexture;

            #pragma vertex GrassLitVertex
            #pragma fragment GrassLitFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attrubutes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color       : COLOR;
            };

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
            
            Varyings GrassLitVertex(Attrubutes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS);

                // float3 positionWS = TransformObjectToWorld(IN.positionOS);

                // Billboard
                // =========================================
                float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;      // 观察矩阵第1行：世界空间下，相机右方向的单位向量
                float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;         // 观察矩阵第2行：世界空间下，相机上方向的单位向量
                float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;   // 观察矩阵第3行：世界空间下，相机前方向的单位向量

                // 模型空间下，每根草顶点坐标：
                // 左：verts[0] = new Vector3(-0.25f, 0);
                // 右：verts[1] = new Vector3(+0.25f, 0);
                // 上：verts[2] = new Vector3(-0.00f, 1);

                // 让每根草旋转，面向相机，实现 billboard 效果
                // 处理左右两个顶点的同时，做了一个宽度变化
                float3 positionOS = 4 * IN.positionOS.x * cameraTransformRightWS;
                positionOS += IN.positionOS.y * cameraTransformUpWS;
                // =========================================
                
                
                // Lighting & Color
                // =========================================
                Light mainLight;
#if _MAIN_LIGHT_SHADOWS
                mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
#else
                mainLight = GetMainLight();
#endif


                half3 N = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // 视野方向
                half3 V = GetWorldSpaceViewDir(positionWS);

                // half3 baseColor = tex2Dlod(_BaseColorTexture, float4(TRANSFORM_TEX(positionWS.xz, _BaseColorTexture),0,0)) * _BaseColor;
                // half3 albedo = lerp(_GroundColor, baseColor, IN.positionOS.y);

                half3 baseColor = _BaseColor;
                half3 albedo = lerp(_GroundColor, baseColor, IN.positionOS.y);

                half3 lightingResult = SampleSH(0) * albedo;

                lightingResult += ApplySingleDirectLight(mainLight, N, V, albedo, IN.positionOS.y);

#if _ADDITIONAL_LIGHTS
                int additionalLightsCount = GetAdditionalLightsCount(); // 返回每个物体受到的光源
                for (int i = 0; i < additionalLightsCount; ++i) // 循环附加光源
                {
                    // 如果 _ADDITIONAL_LIGHT_SHADOWS 被定义，它也会计算阴影
                    Light light = GetAdditionalLight(i, positionWS);
                    lightingResult += ApplySingleDirectLight(light, N, V, albedo, IN.positionOS.y);
                }
#endif
                // =========================================
                
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.color = lightingResult;
                return  OUT;
            }

            half4 GrassLitFragment(Varyings IN):SV_Target
            {
                return float4(IN.color,1);
            }
            
            ENDHLSL
            
        }
    }
}
