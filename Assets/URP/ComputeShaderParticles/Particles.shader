// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Subway/Particles"
{

    Properties
    {
        _ColorLow("Color Slow Speed", Color) = (0, 0, 0.5, 1)
        _ColorHigh("Color High Speed", Color) = (1, 0.215, 0, 1)
        _HighSpeedValue("High speed Value", Range(0, 50)) = 25
    }

    SubShader
    {
        Pass
        {
            Blend SrcAlpha one

            CGPROGRAM
            #pragma target 5.0

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct Particle
            {
                float2 position;
                float2 velocity;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
            };

            // 接收 Compute Buffer 共享的粒子数据
            StructuredBuffer<Particle> Particles;

            uniform float4 _ColorLow;
            uniform float4 _ColorHigh;
            uniform float _HighSpeedValue;

            Varyings vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                Varyings OUT = (Varyings)0;

                // 获取粒子速度，插值获得粒子颜色
                float speed = length(Particles[instance_id].velocity);
                float lerpValue = clamp(speed / _HighSpeedValue, 0.0f, 1.0f);
                OUT.color = lerp(_ColorLow, _ColorHigh, lerpValue);
                
                // 位置
                OUT.position = UnityObjectToClipPos(float4(Particles[instance_id].position, 0.0f, 1.0f));

                return OUT;
            }


            float4 frag(Varyings IN) : COLOR
            {
                return IN.color;
            }

            ENDCG
        }
    }

    Fallback Off
}
