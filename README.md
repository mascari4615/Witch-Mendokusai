# 마녀: 귀찮아

> **AI 와 함께 만듭니다.** 코드·문서 상당 부분을 AI 에이전트(Claude Code)와 함께 씁니다.

마녀 욘이 숲속 연구소에서 출발해 던전에서 버티는 생존 액션 데모입니다. 기본 던전 한 판은 10분입니다. 무기는 자동으로 공격하고, 레벨업마다 카드 세 장 중 하나를 골라 한 판의 빌드를 만듭니다. Unity 로 만든 Windows PC 게임입니다.

- 상태: 데모 개발 중. 스크린샷과 플레이 영상은 화면 정리가 끝나면 이 자리에 붙습니다
- 받기: [GitHub Releases](https://github.com/mascari4615/Witch-Mendokusai/releases) 의 Windows zip. 압축을 풀고 `WitchMendokusai.exe` 실행
- 이 README 의 조작과 코드 설명은 `dev` 가지 기준입니다. 최신 공개판 v0.0.9 (2026-11-12) 는 그 전 판이라 키 재설정, 화면 설정, 세이브 안전, 포커스 일시정지, 업적 창, 큰 포화가 없고 개발용 키가 일부 켜져 있습니다. 다음 공개판에 들어갑니다

## 한 판

1. 시작 화면에서 시작하면 연구소에 도착
2. 던전 안내인 티메토에게 다가가 Z 로 말을 걸고, 던전과 위협 단계를 골라 입장
3. 몰려오는 몬스터를 피하며 경험치를 모아 레벨업, 카드로 무기와 능력을 키움. 정예와 보스가 중간중간 등장
4. 결과 화면에서 보상을 받고 연구소로 귀환. 보상으로 상점과 영구 강화를 이용

## 조작 (키보드와 마우스, 기본값)

| 행동 | 키 |
| --- | --- |
| 이동 | W A S D |
| 스킬 | Left Shift |
| 기본 공격, 보조 공격 | 마우스 왼쪽, 오른쪽 |
| 큰 포화 | R |
| 말 걸기, UI 확인 | Z (또는 Enter) |
| UI 취소 | X |
| 대화 건너뛰기, 자동 진행, 기록 | P, O, L |
| 인벤토리, 스탯, 퀘스트, 인형, 마도서, 도감, 업적 | I, V, J, K, M, B, H |
| 시점 조작 모드 | Tab |
| 열린 창 닫기, 메뉴 (설정, 종료) | ESC |
| 단축키 안내 | F1 |

- 키는 설정창의 키 탭에서 바꿀 수 있습니다. 이미 쓰는 키를 고르면 맞바꿀지 묻습니다
- 해상도, 전체 화면, 수직 동기, FPS 상한, 그래픽 품질 (낮음, 중간, 높음) 도 설정창에 있습니다
- 게임패드는 일부만 묶여 있습니다 (이동, 시점, UI 탐색, 대화 조작)

## 코드에서 볼 곳

| 주제 | 무엇을 했나 | 입구 |
| --- | --- | --- |
| 엔진 없는 도메인 | 전투 규칙, 아이템, 퀘스트 같은 게임 규칙을 Unity 참조 없는 어셈블리 38개로 나눔 (`noEngineReferences`). 엔진 쪽은 `IXxxBridge` 를 구현해 등록하는 방식으로 연결. 이 어셈블리들은 CI 에서 Unity 없이 .NET 으로 경고를 오류로 빌드 | [`DomainSDK/`](DomainSDK), [`DomainSDK/Combat/WaveDensityRules.cs`](DomainSDK/Combat/WaveDensityRules.cs) |
| 생존 던전 흐름 | 입장, 웨이브, 레벨업 카드, 보스, 결과, 귀환 | [`DungeonManager.cs`](Assets/_WitchMendokusai/Domain/Dungeon/Dungeon/DungeonManager.cs), [`CardManager.cs`](Assets/_WitchMendokusai/Domain/Dungeon/Card/Core/CardManager.cs) |
| 세이브 안전 | 임시 파일에 쓰고 바꿔 끼우는 원자적 저장, 앞 판 `.bak` 복구, 깨진 파일 격리, 세이브 버전, 두 번 실행 시 저장 주인 잠금 (Windows) | [`SaveTool.cs`](Assets/_WitchMendokusai/Domain/Save/SaveTool.cs), [`SaveManager.cs`](Assets/_WitchMendokusai/Domain/Save/SaveManager.cs), [`SaveOwnerLock.cs`](Assets/_WitchMendokusai/Core/Utility/SaveOwnerLock.cs) |
| 입력 | New Input System 한 곳 (`InputManager`) 에서 모든 입력을 등록. 릴리스에서 안 쓰는 입력은 표시 하나로 거름. 키 재설정과 충돌 맞바꿈 | [`InputManager.cs`](Assets/_WitchMendokusai/Core/Input/InputManager.cs), [`KeybindRegistry.cs`](Assets/_WitchMendokusai/Core/Input/Keybind/KeybindRegistry.cs) |
| 설정 | 화면 설정 저장과 부팅 적용, URP 에셋 단계별 품질 | [`DisplaySettings.cs`](Assets/_WitchMendokusai/Core/Display/DisplaySettings.cs), [`QualityPreset.cs`](Assets/_WitchMendokusai/Core/Display/QualityPreset.cs) |
| 포커스 일시정지 | 창이 포커스를 잃으면 시간과 소리를 멈춤. 무인 시험과 배치 실행은 면제 | [`FocusPause.cs`](Assets/_WitchMendokusai/Core/Time/FocusPause.cs) |
| 품질 게이트 | push 마다 파일 길이, 초기화 순서 감사 (자체 시험 포함), 죽은 코드, 중복 멤버, asmdef 금지 참조. 수동 빌드 뒤 실행 파일 스모크 | [`.github/workflows/`](.github/workflows) |

- `DomainSDK/` 와 `Assets/` 에는 생존 데모 밖의 실험 놀이 (농사, 타워 디펜스, 1대1 대결 등) 모듈도 있습니다. 데모에서 쓰는 것은 위 표의 경로입니다

## Release

새 버전 박는 절차 — tag 만 push 하면 끝:

```bash
git tag v0.0.5
git push origin v0.0.5
```

[`.github/workflows/release.yml`](.github/workflows/release.yml) 가 자동으로 [`CHANGELOG.md`](CHANGELOG.md) 갱신 + `ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` sync + [GitHub Release](https://github.com/mascari4615/Witch-Mendokusai/releases) 생성. 자세한 흐름·결정 배경·revert 는 [`CLAUDE.md` § Git Workflow → Release flow](CLAUDE.md).

## Content authoring

Unity의 **WM > Authoring > Scene workspace**에서 독립 장면과 동작을 제작합니다. 자세, 표정, 카메라, 조명과 여러 캐릭터를 편집하고 같은 데이터를 게임에서 재생합니다. [사용법과 현재 범위](Documentation/ContentAuthoring.md).

## FMOD

Unity에서 열기 전에 [FMOD 공식 다운로드](https://www.fmod.com/download#fmodforunity)에서 FMOD for Unity 2.04.00을 받아 아래 명령을 실행합니다. Windows 빌드 러너는 같은 파일을 도구 캐시에서 가져옵니다. Desktop 뱅크는 FMOD Studio 2.04.00에서 생성합니다.

```powershell
.\Tools\Prepare-Fmod.ps1 -Archive "$env:USERPROFILE\Downloads\fmodstudio20400.tgz"
```

## 사용한 리소스/에셋

- [Galmuri (Font)](https://galmuri.quiple.dev/), [(Github)](https://github.com/quiple/galmuri)
- [Game-icons.net (Icon)](https://game-icons.net/), [(Github)](https://github.com/game-icons/icons)
  - 생존 용어 아이콘 (`Domain/UI/Survival/Art/TermIcons`, CC BY 3.0). Icons made by Lorc: cut-diamond, laurel-crown, horned-skull, big-wave. Delapouite: rolling-dice-cup, chest, upgrade, upgrade-right (upgrade 90도 회전본). Sbed: cancel
- [Newtonsoft.JSON](https://github.com/applejag/Newtonsoft.Json-for-Unity)
- [Unified-Universal-Blur](https://github.com/lukakldiashvili/Unified-Universal-Blur)
- [DOTween](https://dotween.demigiant.com/index.php), [(Github)](https://github.com/Demigiant/dotween)
- [PrefabLightmapping](https://github.com/Ayfel/PrefabLightmapping)
- [Rito15 - Behavior Tree](https://rito15.github.io/posts/behavior-tree/), [(Github)](https://github.com/rito15/UnityStudy2/tree/master/Rito/2.%20Study/2021_0105_Behavior%20Tree/Scripts)
- [Rito15 - RPG Inventory System](https://rito15.github.io/posts/unity-study-rpg-inventory/), [(Github)](https://github.com/rito15/Unity-RPG-Inventory)
- [FMOD](https://www.fmod.com/unity), [(Github)](https://github.com/fmod/fmod-for-unity)
- [Juhani Junkala (SubspaceAudio)'s Music](https://opengameart.org/users/subspaceaudio)
  - [Chiptune Adventures](https://opengameart.org/content/4-chiptunes-adventure) [(CC0)](https://creativecommons.org/publicdomain/zero/1.0/)
  - [Retro Game Music Pack](https://opengameart.org/content/5-chiptunes-action) [(CC0)](https://creativecommons.org/publicdomain/zero/1.0/)
- [Nathan Gibson's SFX](https://nathangibson.myportfolio.com/)
  - [Universal UI Soundpack](https://ellr.itch.io/universal-ui-soundpack) [(CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/)
- [ArtisticDude's SFX](https://opengameart.org/users/artisticdude)
  - [RPG Sound Pack](https://opengameart.org/content/rpg-sound-pack) [(CC0)](https://creativecommons.org/publicdomain/zero/1.0/)
