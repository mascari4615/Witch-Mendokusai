Shader "WM/ProceduralSlash"
{
	Properties
	{
		_CoreColor ("Crest", Color) = (1, 0.96, 0.82, 1)
		_BodyColor ("Sheet", Color) = (0.47, 0.56, 0.58, 1)
		_Width ("Sheet depth", Range(0.2, 0.95)) = 0.78
		_Curl ("Sheet curl", Range(0, 0.8)) = 0.4
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _CoreColor;
				float4 _BodyColor;
				float _Width;
				float _Curl;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 phase : TEXCOORD1; };
			struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 phase : TEXCOORD1; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				float phase = input.phase.x;
				float along = input.uv.x;
				float across = input.uv.y;
				float spread = smoothstep(0, 0.18, phase);
				float angle = lerp(-2.4, 1.15, along) + (1 - spread) * 0.65;
				float flare = pow(saturate(sin(along * 3.14159265)), 0.55);
				float depth = _Width * flare * (0.65 + along * 0.35);
				float radius = 0.97 - (1 - across) * depth;
				radius *= 0.85 + spread * 0.15 + phase * 0.14;
				float3 position = float3(cos(angle) * radius, sin(angle) * radius, 0);
				position.z = _Curl * (1 - across) * (1 - across) * flare;
				position.z += 0.12 * sin(along * 5) * across;
				output.positionCS = TransformObjectToHClip(position);
				output.uv = input.uv;
				output.phase = input.phase;
				return output;
			}
			float Hash(float2 position)
			{
				return frac(sin(dot(position, float2(127.1, 311.7))) * 43758.5453);
			}
			float Noise(float2 position)
			{
				float2 cell = floor(position);
				float2 fraction = frac(position);
				fraction = fraction * fraction * (3 - 2 * fraction);
				return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), fraction.x),
					lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), fraction.x), fraction.y);
			}
			half4 Frag(Varyings input) : SV_Target
			{
				float phase = input.phase.x;
				float along = input.uv.x;
				float across = input.uv.y;
				// 곡면 안쪽으로 흐르는 결, 외곽선과 독립된 면의 명암
				float flow = along * 3.2 - phase * 4;
				float bend = sin(along * 4.5) * 0.18;
				float broad = Noise(float2(flow, (across + bend) * 10));
				float grain = Noise(float2(flow * 2.1, (across + bend) * 29));
				float streak = broad * 0.72 + grain * 0.28;
				float crest = smoothstep(0.93, 0.99, across);
				float vein = smoothstep(0.48, 0.76, streak) * smoothstep(0.1, 0.75, across);
				float body = smoothstep(0.08, 0.38, across + broad * 0.3);
				float head = smoothstep(0, 0.18, phase);
				float draw = 1 - smoothstep(head - 0.07, head, along);
				float dissolve = smoothstep(0.3, 1, phase);
				float survival = across * 0.48 + broad * 0.4 + grain * 0.12;
				float erosion = smoothstep(dissolve - 0.035, dissolve + 0.035, survival);
				float tip = smoothstep(0, 0.035, along) * (1 - smoothstep(0.94, 1, along));
				float alpha = body * draw * tip * erosion * smoothstep(0, 0.025, phase);
				alpha *= saturate(across * 0.12 + crest * 0.85 + vein * 0.88);
				float3 color = lerp(_BodyColor.rgb, _CoreColor.rgb, saturate(crest + vein * 0.85));
				clip(alpha - 0.025);
				return half4(color, alpha * _CoreColor.a);
			}
			ENDHLSL
		}
	}
}
