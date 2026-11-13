// Meadow ground for the survival stage. Three layers (grass, grass with bare earth, dirt) mixed by world space noise,
// height blend between them, and three anti tiling tools: hex tiling (random offset and rotation per hex cell,
// contrast keeping blend), macro brightness and hue variation, and a second larger scale far away.
// Research and approval: memo wm/design/art/stage-frames-20261113.md (v19 to forest4).
Shader "WM/MeadowGround"
{
    Properties
    {
        _Grass ("Grass (rgb, a = height)", 2D) = "white" {}
        _Earth ("Grass with earth (rgb, a = height)", 2D) = "white" {}
        _Dirt ("Dirt (rgb, a = height)", 2D) = "white" {}
        _Noise ("Noise (r low, g detail)", 2D) = "gray" {}
        _Tiles ("Tile size m (grass, earth, dirt)", Vector) = (2.6, 5, 3, 0)
        _EarthAmount ("Earth patch amount", Range(0, 1)) = 0.35
        _DirtAmount ("Dirt patch amount", Range(0, 1)) = 0.08
        _PatchScale ("Patch noise scale", Float) = 0.03
        _HeightBlend ("Height blend depth", Float) = 0.45
        _Linear ("Share of plain blend", Range(0, 1)) = 0.5
        _MacroStrength ("Macro variation", Range(0, 1)) = 1
        _FarStart ("Far blend start m", Float) = 40
        _FarRange ("Far blend range m", Float) = 80
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

            TEXTURE2D(_Grass); TEXTURE2D(_Earth); TEXTURE2D(_Dirt); SAMPLER(sampler_Grass);
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            CBUFFER_START(UnityPerMaterial)
            float4 _Tiles; float _EarthAmount; float _DirtAmount; float _PatchScale; float _HeightBlend; float _Linear;
            float _MacroStrength; float _FarStart; float _FarRange;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fogFactor : TEXCOORD2; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // Hex tiling after Mikkelsen 2022: triangle grid on skewed uv, each vertex is one hex cell with its own offset and rotation.
            float4 SampleHex(Texture2D tex, float2 uv)
            {
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                const float2x2 skew = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
                float2 skewed = mul(skew, uv * 2.3);
                float2 cell = floor(skewed);
                float3 fraction = float3(frac(skewed), 0);
                fraction.z = 1.0 - fraction.x - fraction.y;
                float2 vertices[3];
                float3 weights;
                if (fraction.z > 0)
                {
                    weights = float3(fraction.z, fraction.y, fraction.x);
                    vertices[0] = cell; vertices[1] = cell + float2(0, 1); vertices[2] = cell + float2(1, 0);
                }
                else
                {
                    weights = float3(-fraction.z, 1.0 - fraction.y, 1.0 - fraction.x);
                    vertices[0] = cell + 1; vertices[1] = cell + float2(1, 0); vertices[2] = cell + float2(0, 1);
                }
                float4 samples[3];
                [unroll] for (int k = 0; k < 3; k++)
                {
                    float2 random = Hash2(vertices[k]);
                    float angle = random.x * 6.2831853;
                    float cosine = cos(angle);
                    float sine = sin(angle);
                    float2x2 rotation = float2x2(cosine, -sine, sine, cosine);
                    samples[k] = SAMPLE_TEXTURE2D_GRAD(tex, sampler_Grass, mul(rotation, uv) + random * 17.0, mul(rotation, dx), mul(rotation, dy));
                }
                float3 luminance = float3(dot(samples[0].rgb, 0.333), dot(samples[1].rgb, 0.333), dot(samples[2].rgb, 0.333));
                weights = pow(weights, 7.0) * (1.0 + 2.4 * luminance);
                weights /= dot(weights, 1);
                return samples[0] * weights.x + samples[1] * weights.y + samples[2] * weights.z;
            }

            float4 Layer(Texture2D tex, float2 positionXZ, float tile, float far)
            {
                float2 uv = positionXZ / tile;
                float4 nearColor = SampleHex(tex, uv);
                float4 farColor = SampleHex(tex, uv * 0.23 + 0.37);
                return lerp(nearColor, lerp(nearColor, farColor, 0.35), far);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 positionXZ = input.positionWS.xz;
                float distanceToCamera = distance(GetCameraPositionWS(), input.positionWS);
                float far = saturate((distanceToCamera - _FarStart) / max(_FarRange, 1));

                // Patch weights from world noise (no path in the survival field)
                float patchNoise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, positionXZ * _PatchScale).r;
                float dirtNoise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, positionXZ * _PatchScale * 0.7 + 0.53).g;
                float earth = smoothstep(1.0 - _EarthAmount - 0.15, 1.0 - _EarthAmount + 0.15, patchNoise);
                float dirt = smoothstep(1.0 - _DirtAmount - 0.06, 1.0 - _DirtAmount + 0.06, dirtNoise);
                float4 control = float4(saturate(1 - earth - dirt), earth * (1 - dirt), dirt, 0);
                control /= max(dot(control, 1), 1e-4);

                float4 grass = Layer(_Grass, positionXZ, _Tiles.x, far);
                float4 earthColor = Layer(_Earth, positionXZ, _Tiles.y, far);
                float4 dirtColor = Layer(_Dirt, positionXZ, _Tiles.z, far);

                float3 heighted = control.xyz + float3(grass.a, earthColor.a, dirtColor.a) * 0.5;
                float top = max(max(heighted.x, heighted.y), heighted.z) - _HeightBlend;
                float3 blend = max(heighted - top, 0) * step(0.001, control.xyz);
                blend /= max(dot(blend, 1), 1e-4);
                blend = lerp(blend, control.xyz, _Linear);
                float3 albedo = grass.rgb * blend.x + earthColor.rgb * blend.y + dirtColor.rgb * blend.z;

                float macroLow = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, positionXZ * 0.011).r;
                float macroDetail = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, positionXZ * 0.047 + 0.31).g;
                float3 varied = albedo * lerp(0.84, 1.14, macroLow) * lerp(0.93, 1.06, macroDetail);
                varied = lerp(varied, varied * float3(1.06, 1.02, 0.86), saturate(macroDetail * 1.6 - 0.5) * 0.6);
                albedo = lerp(albedo, varied, _MacroStrength);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse = saturate(dot(normalWS, mainLight.direction));
                float3 lit = albedo * (mainLight.color * diffuse * mainLight.shadowAttenuation + SampleSH(normalWS));
                return half4(MixFog(lit, input.fogFactor), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
