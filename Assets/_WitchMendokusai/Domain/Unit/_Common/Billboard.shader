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
        // 상태이상 물들임 (알파가 세기), 가장자리 테두리 (알파 0이면 없음), 테두리 두께 화소
        _StatusTint("Status Tint", Color) = (0, 0, 0, 0)
        _StatusRim("Status Rim", Color) = (0, 0, 0, 0)
        _StatusRimWidth("Status Rim Width", Float) = 2.5
        // 피격 좌우 흔들림. x 시작 시각 (_Time.y), y 폭, z 시간, w 초당 왕복
        _HitShake("Hit Shake", Vector) = (0, 0, 0, 0)
        // 장면 빛 반응 배율. 0이면 빛 무시, 1이면 하늘 설정 (SpriteLightingSettings) 그대로
        _LightResponse("Light Response", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True"
        }

        Pass
        {
            // 장면 빛 (해, 환경광, 추가 광원) 을 받으려면 UniversalForward. 물체당 추가 광원 목록이 이 패스에만 온다
            Name "SpriteForward"
            Tags
            {
                "LightMode" = "UniversalForward"
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
            // 추가 광원은 정점에서 계산. 판 그림은 정점이 적어 화소마다 반복하지 않음
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // 장면 안개. 생존 던전의 전투 시야 경계 밖에서 생긴 적을 안개가 가림 (CameraManager.CombatView)
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                half3 light : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _Cutoff;
                float4 _EmissionColor;
                float _Emission;
                float3 _RotateVector;
                float _BillboardMode;
                float4 _StatusTint;
                float4 _StatusRim;
                float _StatusRimWidth;
                float4 _HitShake;
                float _LightResponse;
            CBUFFER_END

            // 하늘 설정이 넣는 전역값 (SpriteLighting.Apply). 안 넣은 장면은 0이라 빛 반응 없음
            float _WMSpriteLightResponse;
            float _WMSpriteLightReference;
            float _WMSpriteSunWeight;
            float _WMSpriteLightFloor;
            float _WMSpritePointGain;
            float _WMSpriteLightCap;
            float _WMSpriteLightMaxCount;

            // 그림에 곱할 빛 배율. 낮은 1에 막혀 지금과 같고, 밤은 바닥값까지만 어두워짐, 추가 광원은 그 위에 더함
            half3 SpriteLight(float3 positionWS, float3 normalWS)
            {
                half response = saturate(_LightResponse * _WMSpriteLightResponse);
                if (response <= 0)
                    return half3(1, 1, 1);

                Light mainLight = GetMainLight();
                half3 scene = SampleSH(normalWS) + mainLight.color * _WMSpriteSunWeight;
                scene = min(scene / max(_WMSpriteLightReference, 0.001), 1);
                half sceneLuminance = dot(scene, half3(0.2126, 0.7152, 0.0722));
                scene = sceneLuminance > 0.0001 ? scene * max(1, _WMSpriteLightFloor / sceneLuminance) : _WMSpriteLightFloor.xxx;

                half3 points = 0;
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                uint lightCount = min(GetAdditionalLightsCount(), (uint)_WMSpriteLightMaxCount);
                for (uint lightIndex = 0; lightIndex < lightCount; lightIndex++)
                {
                    Light light = GetAdditionalLight(lightIndex, positionWS);
                    points += light.color * light.distanceAttenuation;
                }
                #endif

                half3 lit = min(scene + points * _WMSpritePointGain, _WMSpriteLightCap);
                return lerp(half3(1, 1, 1), lit, response);
            }

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
                    float4 pivotPosWorldSpace = mul(UNITY_MATRIX_M, float4(0, 0, 0, 1)); // pivotPosObjectSpace 0, 0, 0
                    float3 cameraLookDir = normalize(pivotPosWorldSpace.xyz - _WorldSpaceCameraPos); // 카메라 -> 오브젝트 방향
                    if (isOnlyYaw == 1.0f) cameraLookDir.y = 0.0f; // Yaw축 빌보드

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

                // 피격 흔들림: 카메라 기준 좌우, 감쇠 사인. 위치와 충돌체는 그대로, 그림만
                float shakeTime = _Time.y - _HitShake.x;
                if (_HitShake.y > 0 && shakeTime >= 0 && shakeTime < _HitShake.z)
                {
                    float shakeDecay = 1 - shakeTime / _HitShake.z;
                    float shakeOffset = _HitShake.y * shakeDecay * shakeDecay * sin(shakeTime * _HitShake.w * 6.2831853);
                    positionWS.xyz += UNITY_MATRIX_V[0].xyz * shakeOffset;
                }
                float4 positionVS = mul(UNITY_MATRIX_V, positionWS);
                float4 positionCS = mul(UNITY_MATRIX_P, positionVS);
                
                o.positionCS = positionCS;
                o.uv = v.uv;
                o.color = v.color;
                // 그림은 늘 카메라를 보므로 법선은 카메라 쪽. 방향 음영 없음
                o.light = SpriteLight(positionWS.xyz, normalize(_WorldSpaceCameraPos - positionWS.xyz));
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 mainTexUV = i.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV);
                float textureAlpha = col.a;

                col *= _Color;
                col *= i.color;
                
                clip(col.a - _Cutoff  * _Emission);

                // 장면 빛. 상태 물들임, 테두리, 피격 점멸은 빛 뒤라 밤에도 그대로 읽힘
                col.rgb *= i.light;

                // 상태이상 물들임. 밝기를 남겨 형태 유지
                float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, _StatusTint.rgb * (0.45 + 0.75 * luminance), _StatusTint.a);

                // 서리 테두리. 이웃 화소가 비어 있으면 가장자리
                if (_StatusRim.a > 0)
                {
                    float2 rimStep = _MainTex_TexelSize.xy * _StatusRimWidth;
                    float neighborAlpha = min(
                        min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV + float2(rimStep.x, 0)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV - float2(rimStep.x, 0)).a),
                        min(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV + float2(0, rimStep.y)).a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainTexUV - float2(0, rimStep.y)).a));
                    float rimEdge = step(0.5, textureAlpha) * (1 - step(0.5, neighborAlpha));
                    col.rgb = lerp(col.rgb, _StatusRim.rgb, rimEdge * _StatusRim.a);
                }

                // 피격 점멸: 더하기 대신 섞기. 1이면 기존과 같은 완전 흰색, 0.5면 형태가 남음
                col.rgb = lerp(col.rgb, _EmissionColor.rgb, saturate(_Emission));
                // 안개는 마지막. 먼 적은 점멸과 상태 표시까지 같이 가려짐
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}
