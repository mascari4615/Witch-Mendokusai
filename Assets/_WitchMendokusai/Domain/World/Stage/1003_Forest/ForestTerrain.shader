// 숲 스테이지 지형. 제어 텍스처 네 층 (풀, 흙 섞인 풀, 흙길, 바위) 을 육각 타일 표집과 높이 섞기로 그림
// 좌표는 오브젝트 공간. 스테이지가 포털 자리에 맞춰 옮겨져도 무늬와 길이 지형에 붙어 있음
// 원형: 숲 시안 forestz21 연구 셰이더 (memo wm/design/style-pass-20261116)
Shader "WM/ForestTerrain"
{
    Properties
    {
        _Control ("Control", 2D) = "red" {}
        _L0 ("L0 (rgb, a=height)", 2D) = "white" {}
        _L1 ("L1", 2D) = "white" {}
        _L2 ("L2", 2D) = "white" {}
        _L3 ("L3", 2D) = "white" {}
        _Noise ("Noise", 2D) = "gray" {}
        _Wall ("Cliff wall (rgb, a=height)", 2D) = "gray" {}
        _WallTile ("Wall tile (m)", Float) = 3
        _WallOn ("Wall on steep faces", Float) = 0
        _Tiles ("Tile size per layer (m)", Vector) = (3, 5, 3, 6)
        _WorldSize ("World origin xz, size xz", Vector) = (0, 0, 100, 100)
        _Hex ("Hex tiling on", Float) = 1
        _Macro ("Macro variation", Float) = 1
        _Far ("Distance blend", Float) = 1
        _HeightBlend ("Height blend depth", Float) = 0.2
        _Linear ("Share of plain (soft) blend", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_L0); TEXTURE2D(_L1); TEXTURE2D(_L2); TEXTURE2D(_L3); SAMPLER(sampler_L0);
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            TEXTURE2D(_Wall);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tiles; float4 _WorldSize; float _Hex; float _Macro; float _Far; float _HeightBlend; float _Linear; float _WallTile; float _WallOn;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 cs : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float fog : TEXCOORD2; float3 os : TEXCOORD3; };

            V vert(A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.pos.xyz);
                o.cs = p.positionCS; o.ws = p.positionWS; o.os = i.pos.xyz; o.n = TransformObjectToWorldNormal(i.n);
                o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }

            float2 Hash2(float2 p) { p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3))); return frac(sin(p) * 43758.5453); }

            // 육각 타일 (Mikkelsen 2022): 기운 uv 위 삼각 격자, 꼭짓점마다 칸 하나에 고유 이동과 회전
            float4 SampleHex(Texture2D t, float2 uv)
            {
                float2 dx = ddx(uv), dy = ddy(uv);
                const float2x2 skew = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
                float2 s = mul(skew, uv * 2.3);
                float2 b = floor(s); float3 f = float3(frac(s), 0); f.z = 1.0 - f.x - f.y;
                float2 v1, v2, v3; float3 w;
                if (f.z > 0) { w = float3(f.z, f.y, f.x); v1 = b; v2 = b + float2(0, 1); v3 = b + float2(1, 0); }
                else { w = float3(-f.z, 1.0 - f.y, 1.0 - f.x); v1 = b + 1; v2 = b + float2(1, 0); v3 = b + float2(0, 1); }
                float3 lum; float4 cs[3]; float2 vs[3] = { v1, v2, v3 };
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float2 h = Hash2(vs[k]);
                    float a = h.x * 6.2831853; float ca = cos(a), sa = sin(a);
                    float2x2 r = float2x2(ca, -sa, sa, ca);
                    float2 u = mul(r, uv) + h * 17.0;
                    cs[k] = SAMPLE_TEXTURE2D_GRAD(t, sampler_L0, u, mul(r, dx), mul(r, dy));
                }
                // 대비 유지 가중치: 표본 밝기로 날카롭게 해 섞이는 곳이 흐려지지 않게
                lum = float3(dot(cs[0].rgb, 0.333), dot(cs[1].rgb, 0.333), dot(cs[2].rgb, 0.333));
                w = pow(w, 7.0) * (1.0 + 0.6 * lum * 4.0);
                w /= dot(w, 1);
                return cs[0] * w.x + cs[1] * w.y + cs[2] * w.z;
            }

            float4 Layer(Texture2D t, float2 xz, float tile, float far)
            {
                float2 uv = xz / tile;
                float4 nearC = _Hex > 0.5 ? SampleHex(t, uv) : SAMPLE_TEXTURE2D(t, sampler_L0, uv);
                if (_Far < 0.5) return nearC;
                float4 farC = _Hex > 0.5 ? SampleHex(t, uv * 0.23 + 0.37) : SAMPLE_TEXTURE2D(t, sampler_L0, uv * 0.23 + 0.37);
                return lerp(nearC, lerp(nearC, farC, 0.35), far);
            }

            half4 frag(V i) : SV_Target
            {
                float2 xz = i.os.xz;
                float2 cuv = (xz - _WorldSize.xy) / _WorldSize.zw;
                float4 ctl = SAMPLE_TEXTURE2D(_Control, sampler_Control, cuv);
                float dist = distance(GetCameraPositionWS(), i.ws);
                float far = saturate((dist - 40.0) / 80.0);
                float4 c0 = Layer(_L0, xz, _Tiles.x, far);
                float4 c1 = Layer(_L1, xz, _Tiles.y, far);
                float4 c2 = Layer(_L2, xz, _Tiles.z, far);
                float4 c3 = Layer(_L3, xz, _Tiles.w, far);
                // 높이 섞기
                float4 hw = ctl + float4(c0.a, c1.a, c2.a, c3.a) * 0.5;
                float m = max(max(hw.x, hw.y), max(hw.z, hw.w)) - _HeightBlend;
                float4 w = max(hw - m, 0) * step(0.001, ctl);
                w /= max(dot(w, 1), 1e-4);
                w = lerp(w, ctl / max(dot(ctl, 1), 1e-4), _Linear);
                float3 alb = c0.rgb * w.x + c1.rgb * w.y + c2.rgb * w.z + c3.rgb * w.w;
                if (_Macro > 0.5)
                {
                    float n1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, xz * 0.011).r;
                    float n2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, xz * 0.047 + 0.31).g;
                    alb *= lerp(0.84, 1.14, n1) * lerp(0.93, 1.06, n2);
                    alb = lerp(alb, alb * float3(1.06, 1.02, 0.86), saturate(n2 * 1.6 - 0.5) * 0.6);
                }
                float3 n = normalize(i.n);
                if (_WallOn > 0.5)
                {
                    // 절벽 면: 가파른 곳에 옆 투영 벽 텍스처, 윗 가장자리는 부드럽게
                    float3 an = abs(n); float2 wx = float2(i.os.z, i.os.y), wz = float2(i.os.x, i.os.y);
                    float4 cwx = SampleHex(_Wall, wx / _WallTile), cwz = SampleHex(_Wall, wz / _WallTile);
                    float4 cw = lerp(cwz, cwx, saturate((an.x - an.z) * 2 + 0.5));
                    float wall = smoothstep(0.82, 0.62, n.y + (cw.a - 0.5) * 0.2);
                    float3 wcol = cw.rgb * float3(0.98, 0.95, 0.86);
                    // 벽 텍스처로 교체
                    alb = lerp(alb, wcol, wall);
                }
                Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                float nl = saturate(dot(n, L.direction));
                float3 lit = alb * (L.color * nl * L.shadowAttenuation + SampleSH(n));
                lit = MixFog(lit, i.fog);
                return half4(lit, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 v(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half f() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
