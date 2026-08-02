// 사거리·보급 반경처럼 「어디까지 닿는가」를 그리는 선 — *무엇에도 가리지 않는다*.
//
// ★ 왜 전용 셰이더인가 (사용자 실증 2회: "벽에 가린다니까? 더 높은 레이어로"):
//   기성 Unlit 셰이더에 렌더 순서·깊이 옵션을 코드로 꽂아 봤지만, 셰이더가 그 속성을 안 갖고 있으면
//   *조용히 무시*된다. 그래서 「깊이 검사 없음」을 셰이더 자체에 못으로 박는다.
//   ZTest Always = 앞에 뭐가 있든 그린다 / ZWrite Off = 다른 것의 깊이를 망치지 않는다 /
//   Overlay 큐 = 마지막에 그린다.
Shader "WM/TowerDefenseOverlayLine"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                // LineRenderer 는 정점 색으로 시작/끝 색을 넘긴다 — 그걸 그대로 살린다.
                output.color = input.color * _BaseColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
