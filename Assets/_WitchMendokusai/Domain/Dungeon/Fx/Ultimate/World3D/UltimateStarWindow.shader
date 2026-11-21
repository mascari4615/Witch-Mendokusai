// 궁극기 3D 연출의 별밭 창. 메시 겉면이 아니라 그 너머 밤하늘이 보이는 재질 (밤의 날 칼날, 공중 틈)
// - 별은 시선 방향으로만 정함 (텍스처와 큐브맵 없음). 메시가 움직이거나 카메라가 흔들리면 안쪽 별이 겉면과 따로 움직여 깊이로 읽힘
// - 별 두 층 (작고 촘촘, 크고 드묾) + 성운 잡음. 바탕은 어두운 남보라
// - 테두리: 법선과 시선의 프레넬 (_RimAmount) 과 꼭짓점 알파 (_EdgeAmount, 틈 메시의 가장자리) 두 가지. 가장자리는 _EdgePower 로 좁힘 (넓으면 판 전체가 희게 뜸)
// - 꼭짓점 색 r 은 투명도 배율 (휘두름 자취의 꼬리 쪽이 옅어지게)
// - _White: 0 이면 별밭, 1 이면 흰 자체 발광 (별빛을 다 빨아들인 칼날)
// - 출력 상한 40: 반정밀도 넘침이 빛 번짐에 번지는 것 방지
Shader "WM/Fx/UltimateStarWindow"
{
	Properties
	{
		_SpaceColor ("Space Color", Color) = (0.03, 0.02, 0.09, 1)
		[HDR] _NebulaColor ("Nebula Color", Color) = (0.5, 0.25, 1.1, 1)
		[HDR] _StarColor ("Star Color", Color) = (2.4, 2.6, 3.4, 1)
		[HDR] _RimColor ("Rim Color", Color) = (1.6, 0.9, 3, 1)
		[HDR] _WhiteColor ("White Color", Color) = (3.2, 3.2, 3.8, 1)
		_StarScale ("Star Scale", Float) = 26
		_StarAmount ("Star Amount", Range(0, 1)) = 0.5
		_NebulaScale ("Nebula Scale", Float) = 3
		_NebulaAmount ("Nebula Amount", Range(0, 2)) = 0.8
		_RimPower ("Rim Power", Range(0.5, 8)) = 2.5
		_RimAmount ("Rim Amount", Range(0, 4)) = 1
		_EdgeAmount ("Edge Amount (vertex alpha)", Range(0, 4)) = 0
		_EdgePower ("Edge Power", Range(1, 12)) = 4
		_White ("White", Range(0, 1)) = 0
		_Alpha ("Alpha", Range(0, 1)) = 1
		_Brightness ("Brightness", Range(0, 4)) = 1
	}

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

		Pass
		{
			Name "ForwardUnlit"
			Tags { "LightMode" = "UniversalForward" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _SpaceColor;
				float4 _NebulaColor;
				float4 _StarColor;
				float4 _RimColor;
				float4 _WhiteColor;
				float _StarScale;
				float _StarAmount;
				float _NebulaScale;
				float _NebulaAmount;
				float _RimPower;
				float _RimAmount;
				float _EdgeAmount;
				float _EdgePower;
				float _White;
				float _Alpha;
				float _Brightness;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 normalWS : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float edge : TEXCOORD2;
				float fade : TEXCOORD3;
			};

			float Hash3(float3 cell)
			{
				cell = frac(cell * 0.3183099 + 0.1);
				cell *= 17.0;
				return frac(cell.x * cell.y * cell.z * (cell.x + cell.y + cell.z));
			}

			float ValueNoise(float3 position)
			{
				float3 cell = floor(position);
				float3 fraction = frac(position);
				fraction = fraction * fraction * (3.0 - 2.0 * fraction);
				float bottom = lerp(lerp(Hash3(cell), Hash3(cell + float3(1, 0, 0)), fraction.x), lerp(Hash3(cell + float3(0, 1, 0)), Hash3(cell + float3(1, 1, 0)), fraction.x), fraction.y);
				float top = lerp(lerp(Hash3(cell + float3(0, 0, 1)), Hash3(cell + float3(1, 0, 1)), fraction.x), lerp(Hash3(cell + float3(0, 1, 1)), Hash3(cell + float3(1, 1, 1)), fraction.x), fraction.y);
				return lerp(bottom, top, fraction.z);
			}

			// 시선 방향을 칸으로 나눠 칸마다 별 하나. amount 가 클수록 별이 있는 칸이 많음
			float StarLayer(float3 direction, float scale, float amount, float size)
			{
				float3 position = direction * scale;
				float3 cell = floor(position);
				float3 local = frac(position) - 0.5;
				float seed = Hash3(cell);
				float3 offset = float3(Hash3(cell + 11.3), Hash3(cell + 27.1), Hash3(cell + 43.7)) - 0.5;
				float apart = length(local - offset * 0.6);
				float present = step(1.0 - amount, seed);
				float twinkle = 0.7 + 0.3 * sin(_Time.y * (2.0 + seed * 5.0) + seed * 40.0);
				return present * smoothstep(size, 0.0, apart) * (0.4 + seed) * twinkle;
			}

			Varyings Vert(Attributes input)
			{
				Varyings output;
				VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
				output.positionCS = position.positionCS;
				output.positionWS = position.positionWS;
				output.normalWS = TransformObjectToWorldNormal(input.normalOS);
				output.edge = input.color.a;
				output.fade = input.color.r;
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
				float3 normal = normalize(input.normalWS);
				float3 look = -viewDirection;

				float stars = StarLayer(look, _StarScale, _StarAmount, 0.16) + StarLayer(look, _StarScale * 0.45, _StarAmount * 0.5, 0.22) * 1.6;
				float nebula = ValueNoise(look * _NebulaScale + 3.7) * ValueNoise(look * _NebulaScale * 2.3 + 9.1);
				float3 space = _SpaceColor.rgb + _NebulaColor.rgb * nebula * _NebulaAmount + _StarColor.rgb * stars;

				float fresnel = pow(1.0 - saturate(abs(dot(normal, viewDirection))), _RimPower);
				float rim = fresnel * _RimAmount + pow(saturate(input.edge), _EdgePower) * _EdgeAmount;
				float3 color = lerp(space, _WhiteColor.rgb, _White) * _Brightness + _RimColor.rgb * rim;
				return float4(min(color, 40.0), _Alpha * input.fade);
			}
			ENDHLSL
		}
	}
}
