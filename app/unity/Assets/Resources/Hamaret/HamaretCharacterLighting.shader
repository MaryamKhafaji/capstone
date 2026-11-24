Shader "MasAR/Hamaret Character"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Weave("Fabric detail", Float) = 0
        _Gait("Walking blend", Float) = 0
        _SkinDetail("Skin shading", Float) = 0
        _Gloss("Specular", Float) = 0.1
        _MaskAlpha("Use texture alpha", Float) = 0
        _Eye("Procedural iris", Float) = 0
        _Vanish("Dissolve progress and floor", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Weave;
                half _Gait;
                half _SkinDetail;
                half _Gloss;
                half _MaskAlpha;
                half _Eye;
                float4 _Vanish;
            CBUFFER_END

            struct Vertex
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Pixel
            {
                float4 position : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Pixel Vert(Vertex input)
            {
                Pixel output = (Pixel)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position = input.position.xyz;
                float hem = saturate((.95 - position.y) * 1.1);
                float cloth = step(.94, _Weave) * hem * (.18 + .82 * _Gait);
                position.x += sin(position.y * 7.9 + input.uv.x * 18.1 + _Time.y * 4.1) * .021 * cloth;
                position.z += sin(position.y * 9.0 - input.uv.x * 10.0 + _Time.y * 3.2) * .016 * cloth;
                output.positionWS = TransformObjectToWorld(position);
                output.position = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normal);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Pixel input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half3 side = SafeNormalize(cross(half3(0, 1, 0), view));
                half3 key = SafeNormalize(view + half3(0, 0.72, 0) - side * 0.48);
                half3 fill = SafeNormalize(view * 0.25 + side * 0.85 + half3(0, 0.35, 0));
                half diffuse = saturate(dot(normal, key));
                half bounce = saturate(dot(normal, fill));
                half facing = saturate(dot(normal, view));
                half rim = (1 - facing) * (1 - facing) * saturate(normal.y + 0.42);
                half specular = pow(saturate(dot(normal, SafeNormalize(view + key))), 38) * _Gloss;
                half4 surface = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                if (_Eye > .5)
                {
                    float2 irisPoint = input.uv - float2(.50, .50);
                    float radial = length(irisPoint);
                    float angle = atan2(irisPoint.y, irisPoint.x);
                    float fibres = .5 + .5 * sin(angle * 29 + radial * 28 + sin(angle * 17) * .65);
                    half3 iris = lerp(half3(.060,.028,.014), half3(.145,.079,.033), fibres * .66);
                    iris = lerp(iris, half3(.021,.014,.011), smoothstep(.143,.190,radial));
                    iris *= lerp(.12, 1, smoothstep(.062,.076,radial));
                    surface.rgb = lerp(iris, half3(.58,.47,.42), smoothstep(.183,.208,radial));
                }
                clip(lerp(1, surface.a, _MaskAlpha) - 0.4);
                float dissolveEdge = 0;
                if (_Vanish.x > 0)
                {
                    float3 grain = floor(input.positionWS * 79);
                    float offset = frac(sin(dot(grain, float3(27.13,71.87,85.31))) * 43758.54);
                    float height = (input.positionWS.y - _Vanish.y) / 1.9;
                    float dissolveMask = height + offset * .31 - _Vanish.x * 1.43;
                    clip(dissolveMask);
                    dissolveEdge = 1 - smoothstep(0,.15,dissolveMask);
                }
                half3 color = lerp(surface.rgb * _BaseColor.rgb, half3(.48,.31,.19), dissolveEdge);
                float2 weaveUV = input.uv * float2(330, 380);
                half weave = sin(weaveUV.x) * sin(weaveUV.y) * saturate(1 - length(fwidth(weaveUV)) * 0.42);
                color *= 1 + weave * _Weave * 0.125;
                float2 poreUV = input.uv * float2(1130,1430);
                float pore = cos(poreUV.x + .7 * sin(poreUV.y)) * sin(poreUV.y);
                color *= 1 + pore * _SkinDetail * .034 * saturate(1-length(fwidth(poreUV))*.39);
                diffuse = lerp(diffuse, .64 * diffuse + .36 * saturate((dot(normal,key)+.28)/1.28), _SkinDetail);
                color += _SkinDetail * rim * half3(.026,.009,.006);
                half3 light = half3(0.30, 0.28, 0.27) + diffuse * half3(0.65, 0.58, 0.52)
                    + bounce * half3(0.14, 0.16, 0.19);
                return half4(color * light + rim * _Weave * half3(0.024, 0.020, 0.015)
                    + specular * half3(1, 0.87, 0.76), 1);
            }
            ENDHLSL
        }
    }
}
