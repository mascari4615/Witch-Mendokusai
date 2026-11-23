Shader "WM/ArcSlash"
{
	// 초승달 베기 궤적. ArcSlashMesh 가 만든 호 메시 (u 호를 따라, v 안쪽 0 바깥 날 1) 에 _Progress (0..1) 로 그림
	// 박자 (레퍼런스 실측 memo wm/design/wand-attack-fx-20261123): 머리가 _GrowPortion 동안 펼쳐짐, _TailStart 부터 꼬리가 녹으며 가늘어짐
	Properties
	{
		[HDR] _EdgeColor ("Edge color", Color) = (2.2, 2.0, 1.5, 1)
		[HDR] _BodyColor ("Body color", Color) = (1.4, 0.95, 0.3, 1)
		_Progress ("Progress", Range(0, 1)) = 0
		_GrowPortion ("Grow portion", Range(0.01, 1)) = 0.22
		_TailStart ("Tail start", Range(0, 1)) = 0.35
		_TailSoftness ("Tail softness", Range(0.01, 0.5)) = 0.18
		_HeadSoftness ("Head softness", Range(0.005, 0.2)) = 0.03
		_CoreWidth ("Core width", Range(0.02, 0.6)) = 0.18
		_BodyPower ("Body power", Range(0.5, 6)) = 2.2
		_BodyAlpha ("Body alpha", Range(0, 1)) = 0.55
		_TrailDim ("Trail dim far from head", Range(0, 1)) = 0.45
		_ThinAmount ("Thin amount while fading", Range(0, 1)) = 0.7
		_FadeStart ("Fade start", Range(0, 1)) = 0.7
		_Intensity ("Intensity", Range(0, 4)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		Pass
		{
			Tags { "LightMode"="SRPDefaultUnlit" }
			Blend One OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			CBUFFER_START(UnityPerMaterial)
				float4 _EdgeColor;
				float4 _BodyColor;
				float _Progress;
				float _GrowPortion;
				float _TailStart;
				float _TailSoftness;
				float _HeadSoftness;
				float _CoreWidth;
				float _BodyPower;
				float _BodyAlpha;
				float _TrailDim;
				float _ThinAmount;
				float _FadeStart;
				float _Intensity;
			CBUFFER_END
			struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
			struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = input.uv;
				return output;
			}
			float4 Frag(Varyings input) : SV_Target
			{
				float along = input.uv.x;
				float across = input.uv.y;
				float head = saturate(_Progress / _GrowPortion);
				float tail = saturate((_Progress - _TailStart) / max(1.0 - _TailStart, 0.001));

				// 머리 앞은 아직 없음, 꼬리 뒤는 녹아 사라짐
				float headMask = 1.0 - smoothstep(head - _HeadSoftness, head, along);
				float tailMask = smoothstep(tail - _TailSoftness, tail, along);
				if (tail <= 0.0)
					tailMask = 1.0;

				// 녹는 동안 안쪽부터 깎여 날만 남음 (가늘어짐)
				float thinCut = tail * _ThinAmount;
				float thinMask = smoothstep(thinCut, thinCut + 0.12, across);

				// 바깥 날의 밝은 심과 안쪽으로 옅어지는 몸
				float core = smoothstep(1.0 - _CoreWidth, 1.0, across);
				float body = pow(across, _BodyPower) * _BodyAlpha;

				// 머리에서 멀수록 어두움 (휘두른 방향이 읽힘)
				float nearHead = saturate(1.0 - (head - along));
				float trail = lerp(1.0 - _TrailDim, 1.0, nearHead);

				float fade = 1.0 - smoothstep(_FadeStart, 1.0, _Progress);
				float alpha = saturate(core + body) * headMask * tailMask * thinMask * trail * fade;
				float3 color = lerp(_BodyColor.rgb, _EdgeColor.rgb, core);
				return float4(color * alpha * _Intensity, alpha);
			}
			ENDHLSL
		}
	}
}
