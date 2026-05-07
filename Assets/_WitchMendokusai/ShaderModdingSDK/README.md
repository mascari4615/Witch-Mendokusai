# Witch-Mendokusai 셰이더팩 모딩 SDK

마인크래프트/OptiFine 풍 외부 셰이더팩 모딩 — WM 본체 안에 통합된 SDK 폴더.

설계 정본: `memo/wm/design/systems/shader-modding-architecture.md`
TASK: `memo/wm/tasks/TASK-WM-055-쉐이더-모딩-커스텀.md`

## 빠르게 시작 (모더용)

1. WM 프로젝트 clone (현재 MVP — 본체 전체. follow-up 으로 SDK zip 분리 예정)
2. Unity Editor 로 프로젝트 열기 (자동 reimport 끝까지 대기)
3. `Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night/` 가 자동 생성됨 (Domain Reload 시 `ShaderModdingSDKBootstrap` 작동)
4. cozy-night 폴더 복사해서 자기 셰이더팩 만들기 (예: `my-pack/`)
   - `CozyNightVolumeProfile.asset` 편집 — `ColorAdjustments` / `Vignette` / `Bloom` 등 표준 VolumeOverride 추가/조정
   - `manifest.json` 편집 — `name` / `author` / `version` / `slots[].assetName` (= VolumeProfile.asset 이름) 갱신
5. Unity 메뉴 `WM > ShaderModdingSDK > Build cozy-night Sample` 클릭 (또는 자기 pack 용 메뉴 추가)
6. 빌드 결과 = `{repo_root}/Shaderpacks_Output/{packId}/` — `manifest.json` + `{packId}.shaderbundle`
7. 그 폴더를 `{persistentDataPath}/shaderpacks/{packId}/` 로 복사
   - Windows: `%USERPROFILE%/AppData/LocalLow/KarmoDDrine/WitchMendokusai/shaderpacks/{packId}/`
8. WM 게임 실행 → ESC → 환경설정 → 쉐이더팩 탭 → 재스캔 → 적용

## 슬롯 (현재 P1)

| ID | 자산 타입 | 합성 |
| --- | --- | --- |
| `postprocess` | `VolumeProfile.asset` | 별도 Runtime Volume + URP priority blend (base 시간대 색감 위에 합성) |

미래 슬롯 (P2~P5): Skybox / Water / Lit override / Terrain / 확장. 각 슬롯의 `assetName` 의미는 슬롯별 정의.

## Manifest 스키마

```json
{
  "schemaVersion": 1,
  "name": "Cozy Night",
  "author": "Mascari4615",
  "version": "0.1.0",
  "description": "Warm color grade for night",
  "bundleFile": "cozy-night.shaderbundle",
  "slots": [
    {
      "id": "postprocess",
      "assetName": "CozyNightVolumeProfile",
      "blendMode": "overlay",
      "priority": 9999
    }
  ]
}
```

- `bundleFile` — 같은 폴더 안의 `.shaderbundle` 파일명
- `slots[].id` — 슬롯 ID (현재 `postprocess` 만)
- `slots[].assetName` — AssetBundle 안의 자산 이름 (확장자 제외)
- `slots[].priority` — URP Volume priority (모더 = 9999 권장, base 시간 시스템 = 0)
- `slots[].blendMode` — P1 메타 only. P5 확장 자리

## base+overlay 합성 모델

모더 셰이더팩은 본체 `DefaultVolumeProfile` 을 *수정하지 않고* 별도 Runtime Volume 에 합성된다. 즉 시간 시스템 (TASK-WM-054 SkyDirector) 의 시간대 색감 위에 모더 효과가 *얹어진다*. 마크 OptiFine 의 vanilla 색감 합성 모델과 정합.

자세한 디자인: `memo/wm/design/systems/shader-modding-architecture.md`

## 알려진 한계 (P1)

- Editor SDK 는 WM 본체에 포함 — 모더가 큰 프로젝트 받음. follow-up: GitHub Release 에 SDK zip 자동 업로드 (CI Actions)
- `blendMode` 메타는 *priority 기반 표준 합성* 만. Multiply/Add 같은 깊은 합성은 P5
- Slot 종류 = PostProcess 만. Skybox/Water/Lit/Terrain 은 P2~P4
- USS 스타일 (쉐이더팩 탭 UI) 은 raw 톤 — `wm-setting-shaderpack-*` class 정의 follow-up
