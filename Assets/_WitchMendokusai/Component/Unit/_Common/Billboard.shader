Shader "WitchMendokusai/BillboardShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Cutoff", Float) = 0.5
        [HDR] _EmissionColor("EmissionColor", Color) = (0,0,0)
        _Emission("Emission", float) = 0 
        _RotateVector("RotateVector", Vector) = (0,0,0,0)
        _BillboardMode("Billboard Mode", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True"
        }

        Pass
        {
            Name "URPUnlit"
            Tags
            {
                "LightMode" = "SRPDefaultUnlit"
            }
            Cull Off
            
            // https://darkcatgame.tistory.com/137

            // Sprite의 투명한 부분이 불투명하게 보이는 문제
            //https://forum.unity.com/threads/shader-on-sprite-makes-transparent-part-visible.546508/
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
                
            // 기존에 사용하던 Alpha Cutoff는 위 상황에서 의도한대로 작동하지 않음
            // 하지만 Cutoff 부분을 제거하면,
            // 피격 효과용으로 주던 Emission이 Color에 더해지는 거라 불투명한 부분이 그대로 노출되므로,
            // Cutoff는 Emission이 활성화되었을 때 기능하도록 수정  
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define _BILLBOARD_MODE_OFF 0
            #define _BILLBOARD_MODE_POS 1
            #define _BILLBOARD_MODE_ROT 2

            #define _BILLBOARD_ONLYYAW 0

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
				// SpriteRenderer의 Color 값
                float4 color : COLOR;
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                float4 _Color;
            CBUFFER_END

            float _Cutoff;
            float4 _EmissionColor;
            float _Emission;
            float3 _RotateVector;
            float _BillboardMode;

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                float4 positionOS = v.positionOS;

                // 월드 공간 크기 행렬 계산
                float4x4 scaleMatrix;
                float4 sx = float4(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20, 0);
                float4 sy = float4(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21, 0);
                float4 sz = float4(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22, 0);
                float scaleX = length(sx);
                float scaleY = length(sy);
                float scaleZ = length(sz);
                scaleMatrix[0] = float4(scaleX, 0, 0, 0);
                scaleMatrix[1] = float4(0, scaleY, 0, 0);
                scaleMatrix[2] = float4(0, 0, scaleZ, 0);
                scaleMatrix[3] = float4(0, 0, 0, 1);

                // 월드 공간 회전 행렬 계산
                float4x4 rotationMatrix;
                float isOnlyYaw = _BILLBOARD_ONLYYAW;

                // Model Matrix에서 회전 행렬 추출
                if (_BillboardMode == _BILLBOARD_MODE_OFF) 
                {
                    rotationMatrix[0] = float4(UNITY_MATRIX_M._m00 / scaleX, UNITY_MATRIX_M._m01 / scaleY, UNITY_MATRIX_M._m02 / scaleZ, 0);
                    rotationMatrix[1] = float4(UNITY_MATRIX_M._m10 / scaleX, UNITY_MATRIX_M._m11 / scaleY, UNITY_MATRIX_M._m12 / scaleZ, 0);
                    rotationMatrix[2] = float4(UNITY_MATRIX_M._m20 / scaleX, UNITY_MATRIX_M._m21 / scaleY, UNITY_MATRIX_M._m22 / scaleZ, 0);
                    rotationMatrix[3] = float4(0, 0, 0, 1);
                }
                // 카메라 위치기준 빌보드
                else if (_BillboardMode == _BILLBOARD_MODE_POS) 
                {
                    // 월드 포지션 기준 카메라 방향 벡터 계산
                    float3 pivotPosOS = float3(0, 0, 0);
                    float4 pivotPosWS = mul(UNITY_MATRIX_M, float4(pivotPosOS.xyz, 1));
                    float4 cameraPosWS = float4(_WorldSpaceCameraPos.xyz, 1);
                    float3 cameraLookDir = normalize(pivotPosWS.xyz - cameraPosWS.xyz);
                    if (isOnlyYaw ==1.0f) cameraLookDir.y = 0.0f; // Yaw축 빌보드

                    // 카메라 방향 벡터를 회전 행렬로 계산
                    float3 upVector = float3(0, 1, 0);
                    float3 rotM2 = cameraLookDir;
                    float3 rotM0 = normalize(cross(upVector, rotM2));
                    float3 rotM1 = cross(rotM2, rotM0);

                    float4x4 cameraRotMatrix;
                    cameraRotMatrix[0] = float4(rotM0.x, rotM1.x, rotM2.x, 0);
                    cameraRotMatrix[1] = float4(rotM0.y, rotM1.y, rotM2.y, 0);
                    cameraRotMatrix[2] = float4(rotM0.z, rotM1.z, rotM2.z, 0);
                    cameraRotMatrix[3] = float4(0, 0, 0, 1);
                    rotationMatrix = cameraRotMatrix;
                }
                // 카메라 방향(Rotation) 기준 빌보드
                else if (_BillboardMode == _BILLBOARD_MODE_ROT) 
                {
                    float4x4 cameraRotMatrix;
                    // View 메트릭스(카메라 회전 행렬)를 회전 행렬로 사용
                    cameraRotMatrix[0] = float4(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02, 0);
                    cameraRotMatrix[1] = float4(UNITY_MATRIX_V._m10, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m12, 0);
                    cameraRotMatrix[2] = float4(UNITY_MATRIX_V._m20, UNITY_MATRIX_V._m21, UNITY_MATRIX_V._m22, 0);
                    cameraRotMatrix[3] = float4(0, 0, 0, 1);
                    if (isOnlyYaw == 1.0f) cameraRotMatrix[1] = float4(0, 1, 0, 0); // Yaw축 빌보드
                    rotationMatrix = transpose(cameraRotMatrix); // View 공간을 월드 공간 기준으로 계산하기 위해 전치행렬로 변환하여 역행렬 계산하게 세팅.
                }

                float4x4 moveMatrix;
                // 월드 공간 위치 행렬 계산
                moveMatrix[0] = float4(1, 0, 0, UNITY_MATRIX_M._m03);
                moveMatrix[1] = float4(0, 1, 0, UNITY_MATRIX_M._m13);
                moveMatrix[2] = float4(0, 0, 1, UNITY_MATRIX_M._m23);
                moveMatrix[3] = float4(0, 0, 0, UNITY_MATRIX_M._m33);

                float4x4 modelMatrix = mul(mul(moveMatrix, rotationMatrix), scaleMatrix);
                float4 positionWS = mul(modelMatrix, float4(positionOS.xyz, 1));
                float4 positionVS = mul(UNITY_MATRIX_V, positionWS);
                float4 positionCS = mul(UNITY_MATRIX_P, positionVS);
                
                o.positionCS = positionCS;
                o.uv = v.uv;
				o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 mainTexUV = i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV);

                col *= _Color;
                col *= i.color;
                
                clip(col.a - _Cutoff  * _Emission);
                col += _EmissionColor * _Emission;
                return col;
            }
            ENDHLSL
        }
    }
}