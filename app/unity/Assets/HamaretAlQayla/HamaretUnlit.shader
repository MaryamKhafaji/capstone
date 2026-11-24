Shader "MasAR/Hamaret Unlit"
{
    Properties
    {
        _Ownership("Territory ownership", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Mode ("Surface mode", Float) = 0
        _ClothMotion ("Fabric motion", Float) = 0
        _Weave ("Fabric grain", Float) = 0
        _Detail ("Surface detail", Float) = 0
        _Trim ("Embroidered hem", Float) = 0
        _Effect ("Effect settings", Vector) = (0,0,0,0)
        [Toggle] _WriteDepth ("Write depth", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_WriteDepth]
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Ownership);
            SAMPLER(sampler_Ownership);
            float4 _LightWake[12];
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Mode;
            half _ClothMotion;
            half _Weave;
            half _Detail;
            half _Trim;
            half _WriteDepth;
            float4 _Effect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 tint : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half4 tint : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position = input.positionOS.xyz;
                float drape = saturate((1.34 - position.y) * .78) * _ClothMotion;
                position.x += sin(_Time.y * 3.1 + position.y * 6 + input.uv.x * 19) * drape * .028;
                position.z += sin(_Time.y * 3.7 + position.y * 5 - input.uv.x * 15) * drape * .052;
                output.positionWS = TransformObjectToWorld(position);
                output.positionOS = position;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.tint = input.tint;
                return output;
            }

            float Hash(float2 p) { return frac(sin(dot(p,float2(41.13,289.2))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 cell=floor(p), t=frac(p);
                t=t*t*(3-2*t);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),t.x),
                    lerp(Hash(cell+float2(0,1)),Hash(cell+1),t.x),t.y);
            }

            float PuffNoise(float2 p)
            {
                return Noise(p)*.57+Noise(p*2.19+4.2)*.28+Noise(p*4.13-6.9)*.15;
            }

            float2 uvForSmoke(float2 uv,float seconds,float seed)
            {
                float2 p=uv*float2(3.1,3.7)+float2(seed-seconds*.31,seed*.67-seconds);
                float2 twist=float2(Noise(p*.77+3.4),Noise(p*.69-2.7))-.5;
                return p+twist*1.31;
            }

            half3 SurfaceDetail(Varyings input, half3 baseColor)
            {
                float2 uv=input.uv;
                if (_Detail>1.5 && _Detail<2.5)
                {
                    float2 p=(uv-float2(.505,.51))*float2(3.7,1.27);
                    float d=length(p);
                    half3 sclera=lerp(half3(.34,.255,.22),half3(.69,.60,.51),sin(uv.y*3.1416));
                    half3 iris=lerp(half3(.13,.087,.048),half3(.039,.027,.021),smoothstep(.19,.34,d));
                    half3 eye=lerp(iris,sclera,smoothstep(.32,.39,d));
                    eye=lerp(eye,half3(.019,.016,.014),1-smoothstep(.13,.17,d));
                    eye+=.47*(1-smoothstep(.035,.078,length(p-float2(-.10,.15))));
                    return eye*lerp(.36,1,smoothstep(0,.25,min(uv.y,1-uv.y)));
                }
                if (_Detail>.5 && _Detail<1.5 && input.positionOS.z>.036)
                {
                    float2 p=input.positionOS.xy;
                    float x=abs(p.x);
                    float lipWidth=saturate(1-x/.022);
                    float mouth=.110+.0013*cos(x*310);
                    float lips=(1-smoothstep(.002,.007,abs(p.y-mouth)/max(.17,lipWidth)))*step(x,.022);
                    float crease=(1-smoothstep(.0005,.0016,abs(p.y-mouth)))*lipWidth;
                    float browY=.212+.006*sin(saturate((x-.016)/.046)*3.14)-.05*(x-.036);
                    float brow=(1-smoothstep(.001,.004,abs(p.y-browY)))*smoothstep(.014,.021,x)*
                        (1-smoothstep(.054,.063,x));
                    float2 cheekOffset=(float2(x,p.y)-float2(.047,.148))/float2(.020,.029);
                    float2 nostrilOffset=(float2(x,p.y)-float2(.012,.143))/float2(.004,.0023);
                    float cheek=exp(-dot(cheekOffset,cheekOffset));
                    float nostril=exp(-dot(nostrilOffset,nostrilOffset));
                    baseColor=lerp(baseColor,baseColor*half3(1.05,.87,.83),cheek*.45);
                    baseColor=lerp(baseColor,half3(.35,.164,.133),lips*.76);
                    baseColor=lerp(baseColor,half3(.19,.089,.071),crease*.8+nostril*.5);
                    baseColor=lerp(baseColor,half3(.082,.053,.041),brow*.82);
                    return baseColor*(.975+Noise(uv*207)*.050);
                }
                if (_Detail>2.5)
                {
                    float strands=Noise(uv*float2(170,8)+Noise(uv*15)*.57);
                    float grain=Noise(uv*float2(64,30));
                    return baseColor*lerp(.68,1.34,strands*.70+grain*.30);
                }
                return baseColor;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color=_Color;
                if (_Mode < .5 && _WriteDepth > .5)
                {
                    half3 n=normalize(input.normalWS);
                    half3 eye=normalize(GetWorldSpaceViewDir(input.positionWS));
                    half3 light=normalize(half3(-.36,.78,.51));
                    half diffuse=saturate(dot(n,light));
                    half facing=saturate(dot(n,eye));
                    half rim=pow(1-saturate(dot(n,eye)),2.6);
                    half sheen=pow(saturate(dot(n,normalize(eye+light))),22);
                    half weave=1-.04*_Weave*(1-sin(input.uv.x*264)*sin(input.uv.y*291));
                    half3 albedo=SurfaceDetail(input,color.rgb);
                    if (_Trim>.5)
                    {
                        float hem=input.uv.y;
                        float thread=abs(sin(input.uv.x*113.10+hem*110)*sin(input.uv.x*113.10-hem*110));
                        float embroidery=(1-smoothstep(.038,.047,hem))*smoothstep(.010,.016,hem)*smoothstep(.55,.81,thread);
                        embroidery=max(embroidery,(1-smoothstep(.001,.0026,abs(hem-.006)))*.83);
                        albedo=lerp(albedo,half3(.44,.266,.107),embroidery);
                    }
                    color.rgb=albedo*(.63+diffuse*.49+facing*.28)*weave+
                        rim*half3(.080,.076,.069)+sheen*half3(.072,.063,.050)+
                        _Weave*half3(.032,.029,.025)*facing*(.34+diffuse*.66);
                    return color;
                }

                if (_Mode > 2.5 && _Mode < 3.5)
                {
                    float2 uv=input.uv;
                    float pattern=Noise(uv*float2(7,11));
                    float tracer=abs(sin(uv.y*27+sin(uv.x*7+uv.y*9)*1.6+pattern*4.1));
                    float silk=1-smoothstep(.015,.087,tracer);
                    float edges=smoothstep(0,.18,uv.x)*smoothstep(0,.18,1-uv.x)*
                        smoothstep(0,.08,uv.y)*smoothstep(0,.07,1-uv.y);
                    float healed=smoothstep(-.065,.02,_Effect.x-uv.y)*step(.001,_Effect.x);
                    float boost=smoothstep(-.025,.02,uv.y-_Effect.x)*
                        smoothstep(0,.04,_Effect.x+_Effect.z-uv.y)*step(.002,_Effect.z);
                    float wave=(1-smoothstep(.015,.064,abs(_Effect.x-uv.y)))*step(.001,_Effect.x);
                    color.rgb=half3(.98,.68,.334)+half3(.15,.11,.044)*wave;
                    float glow=1-smoothstep(.03,.34,tracer);
                    color.a=edges*_Effect.w*(healed*(glow*.091+silk*.26*pattern)+wave*silk*.14+boost*silk*.27);
                    return color;
                }

                float2 q=input.uv-.5;
                if (_Mode > 1.5 && _Mode < 2.5)
                {
                    float seconds=_Time.y*.39;
                    float2 flow=uvForSmoke(input.uv,seconds,_Effect.x);
                    float density=PuffNoise(flow);
                    float shell=PuffNoise(flow+float2(.41,-.23));
                    float y=input.uv.y;
                    float phase=_Effect.x+y*8.7-seconds*3.4;
                    float spine=sin(phase)*(.043+y*.113)+sin(phase*.61+2.1)*.051*y;
                    float curl=sin(phase+shell*4)*.095*y;
                    float width=.18+sin(y*3.1416)*.12;
                    float flank=abs(q.x-spine-curl+(density-.5)*.28);
                    float core=(1-smoothstep(width*.24,width,flank))*
                        smoothstep(0,.05,y)*(1-smoothstep(.57,.99,y));
                    float roll=smoothstep(.31,.66,density+sin(phase+q.x*8)*.13);
                    color.rgb+=half3(.105,.101,.093)*smoothstep(.40,.76,shell-density*.38);
                    // Suppress the razor-thin silhouette of a slice seen side-on.
                    // The perpendicular slice continues to show its curved plume.
                    float incidence=abs(dot(normalize(input.normalWS),normalize(GetWorldSpaceViewDir(input.positionWS))));
                    color.a*=core*roll*smoothstep(.14,.48,incidence);
                    return color;
                }


                if (_Mode > 6.5)
                {
                    float2 uv = input.uv;
                    float4 ownership = SAMPLE_TEXTURE2D(_Ownership, sampler_Ownership, uv);
                    float lit = smoothstep(.18, .76, ownership.r);
                    float dark = smoothstep(.18, .76, ownership.b);
                    float trail = smoothstep(.14, .73, ownership.g);
                    float rival = smoothstep(.14, .73, ownership.a);
                    color.rgb = lerp(half3(.029,.017,.048), half3(1,.94,.78), lit);
                    color.rgb = lerp(color.rgb, half3(.37,.19,.53), rival*.94);
                    color.rgb = lerp(color.rgb, half3(1,.68,.15), trail);
                    color.a = max(max(dark*.81, lit*.56), max(rival*.82, trail*.94));
                    return color;
                }
                if (_Mode > 5.5)
                    return color;
                float radius=length(q);
                half spread=1-smoothstep(.08,.48,radius);
                if (_Mode > 4.5)
                    color *= input.tint;
                color.a*=spread;
                return color;
            }
            ENDHLSL
        }
    }
}
