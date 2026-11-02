Shader "WM/ProceduralSlash"
{
	Properties
	{
		_CoreColor ("Blade light", Color) = (1, 0.97, 0.86, 1)
		_EdgeColor ("Blade shade", Color) = (0.58, 0.68, 0.70, 1)
		_OutlineColor ("Contour", Color) = (0.12, 0.16, 0.19, 1)
		_Width ("Blade thickness", Range(0.01, 0.18)) = 0.028
		_Arc ("Arc radians", Range(1, 4.5)) = 3.8
		_SweepTime ("Sweep phase", Range(0.1, 0.6)) = 0.3
		_Trail ("Afterimage", Range(0, 1)) = 0.3
		_Impact ("Impact flare", Range(0, 1)) = 0
		_PixelSize ("Screen pixel block", Range(1, 8)) = 4
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
				float _Impact;
				float _PixelSize;
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
			float Grain(float2 p)
			{
				return frac(sin(dot(floor(p * 73), float2(127.1, 311.7))) * 43758.5453);
			}
			half4 Frag(Varyings input) : SV_Target
			{
				float phase = saturate(input.phase);
				// 화면 격자 중심에서만 모양 계산, 기울어진 참격도 정사각 도트 유지
				float pixelSize = max(1, round(_PixelSize));
				float2 cell = floor(input.positionCS.xy / pixelSize);
				float2 offset = (cell + 0.5) * pixelSize - input.positionCS.xy;
				float2 uvDx = ddx(input.uv);
				float2 uvDy = ddy(input.uv);
				float2 p = (input.uv + uvDx * offset.x + uvDy * offset.y) * 2 - 1;
				float radius = length(p);
				float2 radialDirection = p / max(radius, 0.001);
				float radialPixel = length(float2(dot(radialDirection, uvDx), dot(radialDirection, uvDy))) * 2 * pixelSize;
				float angle = atan2(p.y, p.x);
				float sweep = saturate(phase / _SweepTime);
				float head = lerp(-2.35, 2.25, 1 - (1 - sweep) * (1 - sweep));
				float arc = _Arc * smoothstep(0, 0.14, phase) * (1 - 0.5 * smoothstep(0.45, 1, phase));
				float behind = fmod(head - angle + 12.56637, 6.283185);
				float along = saturate(behind / max(arc, 0.001));
				float taper = pow(saturate(sin(along * 3.14159265)), 0.7);
				float arcMask = step(behind, arc);
				float bladeRadius = lerp(0.58, 0.77, sweep) + phase * 0.035;
				float width = max(_Width * (1 - phase * 0.65), radialPixel * 0.55) * taper;
				float distance = abs(radius - bladeRadius);
				float body = step(distance, width) * arcMask;
				float contour = step(distance, width + radialPixel * 0.4) * arcMask;
				float fade = 1 - smoothstep(0.38, 1, phase);
				float erosion = step(Grain(p) * 0.75, fade);
				float core = floor(saturate(1 - distance / max(width, 0.001)) * 2 + 0.5) * 0.5;
				float3 bladeColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
				float3 color = lerp(_OutlineColor.rgb, bladeColor, body);
				float alpha = contour * fade * erosion;
				float trailDistance = abs(radius - (bladeRadius - 0.10 - phase * 0.05));
				float trail = step(trailDistance, radialPixel * 0.45) * arcMask * taper * _Trail * fade * fade;
				color = lerp(color, _EdgeColor.rgb, saturate(trail / max(alpha, 0.001)));
				alpha = max(alpha, trail);
				float2 tip = float2(cos(head), sin(head)) * bladeRadius;
				float2 spark = p - tip;
				float flare = saturate(1 - abs(spark.x) / 0.17 - abs(spark.y) / 0.045);
				flare = max(flare, saturate(1 - abs(spark.x) / 0.035 - abs(spark.y) / 0.14));
				flare *= _Impact * (1 - smoothstep(0.16, 0.38, phase)) * smoothstep(0.04, 0.12, phase);
				color = lerp(color, _CoreColor.rgb, flare);
				alpha = max(alpha, flare);
				// 반투명 흐림 대신 2x2 도트 순서로 잔상 소멸
				float2 parity = fmod(cell, 2);
				float dither = (fmod(parity.x + parity.y, 2) * 2 + parity.y + 0.5) * 0.25;
				clip(alpha * _CoreColor.a - dither);
				return half4(floor(saturate(color) * 15 + 0.5) / 15, 1);
			}
			ENDHLSL
		}
	}
}
