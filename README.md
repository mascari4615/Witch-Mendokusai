# 마녀: 귀찮아

> **AI 와 함께 만듭니다.** 코드·문서 상당 부분을 AI 에이전트(Claude Code)와 함께 씁니다.

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
