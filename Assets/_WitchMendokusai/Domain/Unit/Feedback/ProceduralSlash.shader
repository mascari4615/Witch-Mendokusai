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
		_Steps ("Contour resolution", Range(24, 256)) = 96
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
				float _Steps;
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
				float2 original = input.uv * 2 - 1;
				float aa = max(length(fwidth(original)), 0.004);
				float2 p = floor(original * _Steps + 0.5) / _Steps;
				float radius = length(p);
				float angle = atan2(p.y, p.x);
				float sweep = saturate(phase / _SweepTime);
				float head = lerp(-2.35, 2.25, 1 - (1 - sweep) * (1 - sweep));
				float arc = _Arc * smoothstep(0, 0.14, phase) * (1 - 0.5 * smoothstep(0.45, 1, phase));
				float behind = fmod(head - angle + 12.56637, 6.283185);
				float along = saturate(behind / max(arc, 0.001));
				float taper = pow(saturate(sin(along * 3.14159265)), 0.7);
				float arcMask = 1 - smoothstep(arc - 0.035, arc + 0.035, behind);
				float bladeRadius = lerp(0.58, 0.77, sweep) + phase * 0.035;
				float width = _Width * taper * (1 - phase * 0.65);
				float distance = abs(radius - bladeRadius);
				float body = (1 - smoothstep(width - aa, width + aa, distance)) * arcMask;
				float contour = (1 - smoothstep(width + 0.009 - aa, width + 0.009 + aa, distance)) * arcMask;
				float fade = 1 - smoothstep(0.38, 1, phase);
				float erosion = step(Grain(p) * 0.75, fade);
				float core = 1 - smoothstep(width * 0.2, max(width * 0.85, aa), distance);
				float3 bladeColor = lerp(_EdgeColor.rgb, _CoreColor.rgb, core);
				float3 color = lerp(_OutlineColor.rgb, bladeColor, body);
				float alpha = contour * fade * erosion;
				float trailDistance = abs(radius - (bladeRadius - 0.10 - phase * 0.05));
				float trail = (1 - smoothstep(0.007, 0.007 + aa, trailDistance)) * arcMask * taper * _Trail * fade * fade;
				color = lerp(color, _EdgeColor.rgb, saturate(trail / max(alpha, 0.001)));
				alpha = max(alpha, trail);
				float2 tip = float2(cos(head), sin(head)) * bladeRadius;
				float2 spark = p - tip;
				float flare = saturate(1 - abs(spark.x) / 0.17 - abs(spark.y) / 0.045);
				flare = max(flare, saturate(1 - abs(spark.x) / 0.035 - abs(spark.y) / 0.14));
				flare *= _Impact * (1 - smoothstep(0.16, 0.38, phase)) * smoothstep(0.04, 0.12, phase);
				color = lerp(color, _CoreColor.rgb, flare);
				alpha = max(alpha, flare);
				clip(alpha - 0.015);
				return half4(color, alpha * _CoreColor.a);
			}
			ENDHLSL
		}
	}
}
