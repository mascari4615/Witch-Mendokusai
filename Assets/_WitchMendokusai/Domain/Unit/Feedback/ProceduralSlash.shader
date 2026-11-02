Shader "WM/ProceduralSlash"
{
	Properties
	{
		_CoreColor ("Blade light", Color) = (1, 0.97, 0.86, 1)
		_EdgeColor ("Blade shade", Color) = (0.58, 0.68, 0.70, 1)
		_OutlineColor ("Contour", Color) = (0.12, 0.16, 0.19, 1)
		_Width ("Ribbon width", Range(0.01, 0.3)) = 0.11
		_Arc ("Ribbon length radians", Range(1, 4.5)) = 2.5
		_SweepTime ("Sweep phase", Range(0.1, 0.6)) = 0.42
		_Trail ("Fine trailing lines", Range(0, 1)) = 0.3
		_Tear ("Trailing edge separation", Range(0, 1)) = 0.55
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
				float4 _EdgeColor;
				float4 _OutlineColor;
				float _Width;
				float _Arc;
				float _SweepTime;
				float _Trail;
				float _Tear;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 phase : TEXCOORD1; };
			struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				output.phase = input.phase.x;
				return output;
			}
			half4 Frag(Varyings input) : SV_Target
			{
				float phase = saturate(input.phase);
				float2 p = input.uv * 2 - 1;
				float angle = atan2(p.y, p.x);
				float radius = length(p);
				float sweep = saturate(phase / _SweepTime);
				float travel = 4.5 * (1 - pow(1 - sweep, 3));
				float head = -2.35 + travel;
				// 먼저 지난 꼬리부터 회수, 앞선 뒤로 짧게 남는 곡면
				float release = smoothstep(_SweepTime * 0.7, 1, phase);
				float arc = min(_Arc, travel) * (1 - release);
				float behind = fmod(head - angle + 12.56637, 6.283185);
				float along = saturate(behind / max(arc, 0.001));
				float taper = pow(saturate(sin(along * 3.14159265)), 0.65) * (1 - along * 0.6);
				float arcMask = step(behind, arc) * smoothstep(0, 0.035, phase);
				float outerRadius = 0.79 - along * along * 0.17 + release * 0.035;
				float width = _Width * taper * (1 - release * 0.55);
				float depth = outerRadius - radius;
				float edge = max(fwidth(radius) * 0.65, 0.001);
				float body = smoothstep(-edge, edge, depth) * (1 - smoothstep(width - edge, width + edge, depth));
				float contour = smoothstep(-edge, edge, depth + 0.003) * (1 - smoothstep(width + 0.003 - edge, width + 0.003 + edge, depth));
				float across = saturate(depth / max(width, 0.001));
				// 길이 방향의 절개로 잔상 분리, 화면 격자와 입자 노이즈 제외
				float splitOne = abs(across - (0.48 + along * 0.14));
				float splitTwo = abs(across - (0.77 - along * 0.09));
				float tearWidth = _Tear * 0.13;
				float firstCut = (1 - smoothstep(tearWidth, tearWidth + 0.03, splitOne)) * smoothstep(0.35, 0.65, along + release * 0.2);
				float secondCut = (1 - smoothstep(tearWidth * 0.6, tearWidth + 0.03, splitTwo)) * smoothstep(0.58, 0.85, along + release * 0.15);
				float strands = 1 - max(firstCut, secondCut);
				body *= strands;
				contour *= strands;
				float light = 1 - smoothstep(0.14, 0.23, across);
				float3 bladeColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, light);
				float3 color = lerp(_OutlineColor.rgb, bladeColor, body);
				float fade = 1 - smoothstep(0.75, 1, phase);
				float alpha = contour * arcMask * fade;
				float lineRadius = outerRadius + 0.045 + along * 0.04;
				float lineDistance = abs(radius - lineRadius);
				float lineWidth = 0.003 * taper;
				float trail = (1 - smoothstep(lineWidth, lineWidth + edge, lineDistance));
				trail *= smoothstep(0.12, 0.25, along) * (1 - smoothstep(0.65, 0.9, along)) * arcMask * _Trail * fade;
				color = lerp(color, _EdgeColor.rgb, saturate(trail / max(alpha, 0.001)));
				alpha = max(alpha, trail);
				clip(alpha - 0.015);
				return half4(color, alpha * _CoreColor.a);
			}
			ENDHLSL
		}
	}
}
