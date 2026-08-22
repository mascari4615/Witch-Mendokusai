# 마녀: 귀찮아

> **AI 와 함께 만듭니다.** 코드·문서 상당 부분을 AI 에이전트(Claude Code)와 함께 씁니다.

## Release

새 버전 박는 절차 — tag 만 push 하면 끝:

```bash
git tag v0.0.5
git push origin v0.0.5
```

[`.github/workflows/release.yml`](.github/workflows/release.yml) 가 자동으로 [`CHANGELOG.md`](CHANGELOG.md) 갱신 + `ProjectSettings/ProjectSettings.asset` 의 `bundleVersion` sync + [GitHub Release](https://github.com/mascari4615/Witch-Mendokusai/releases) 생성. 자세한 흐름·결정 배경·revert 는 [`CLAUDE.md` § Git Workflow → Release flow](CLAUDE.md).

## 사용한 리소스/에셋

- [Galmuri (Font)](https://galmuri.quiple.dev/), [(Github)](https://github.com/quiple/galmuri)
- [Game-icons.net (Icon)](https://game-icons.net/), [(Github)](https://github.com/game-icons/icons)
- [Newtonsoft.JSON](https://github.com/applejag/Newtonsoft.Json-for-Unity)
- [Unified-Universal-Blur](https://github.com/lukakldiashvili/Unified-Universal-Blur)
- [DOTween](https://dotween.demigiant.com/index.php), [(Github)](https://github.com/Demigiant/dotween)
- [PrefabLightmapping](https://github.com/Ayfel/PrefabLightmapping)
- [Rito15 - Behavior Tree](https://rito15.github.io/posts/behavior-tree/), [(Github)](https://github.com/rito15/UnityStudy2/tree/master/Rito/2.%20Study/2021_0105_Behavior%20Tree/Scripts)
- [Rito15 - RPG Inventory System](https://rito15.github.io/posts/unity-study-rpg-inventory/), [(Github)](https://github.com/rito15/Unity-RPG-Inventory)
- [FMOD](https://www.fmod.com/unity), [(Github)](https://github.com/fmod/fmod-for-unity)
- [PROTODOME's Album](https://protodome.com/), [(Bandcamp)](https://protodome.bandcamp.com/)
  - [BLUENOISE](https://protodome.bandcamp.com/album/bluenoise) [(CC BY-NC-ND 3.0)](https://creativecommons.org/licenses/by-nc-nd/3.0/)
- [Nathan Gibson's SFX](https://nathangibson.myportfolio.com/)
  - [Universal UI Soundpack](https://ellr.itch.io/universal-ui-soundpack) [(CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/)
- [ArtisticDude's SFX](https://opengameart.org/users/artisticdude)
  - [RPG Sound Pack](https://opengameart.org/content/rpg-sound-pack) [(CC0)](https://creativecommons.org/publicdomain/zero/1.0/)
