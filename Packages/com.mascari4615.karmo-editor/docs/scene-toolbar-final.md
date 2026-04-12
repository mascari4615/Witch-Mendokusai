# KarmoEditor 씬 툴바 결과 보고서 (Scene Toolbar Final)

Summary: Unity 6.3 MainToolbar API를 활용한 씬 선택기 구현 결과 및 기술적 세부 사항 보고.

## 1. 개발 개요

에디터 상단 툴바에서 씬 탐색 및 전환 드롭다운 메뉴 추가로 작업 효율성 개선.

## 2. 주요 기능

- **메인 툴바 통합**: Unity 6.3 정적 `MainToolbar` API를 사용하여 에디터 최상단에 UI 배치.
- **씬 자동 스캔**: 설정된 폴더 내의 `.unity` 파일을 자동으로 찾아 리스트화.
- **즐겨찾기 지원**: 자주 사용하는 씬을 별도로 등록하여 상단에 노출.
- **실시간 UI 갱신**: 씬 전환 시 `MainToolbar.Refresh`를 사용하여 드롭다운 타이틀에 현재 씬 이름 즉시 반영.

## 3. 기술적 구현 세부 사항

### 3.1 사용 API (Unity 6.3+)

- **`UnityEditor.Toolbars.MainToolbarElementAttribute`**: 정적 메서드를 툴바 요소로 등록.
- **`UnityEditor.Toolbars.MainToolbarDropdown`**: 툴바 전용 드롭다운 UI 클래스.
- **`UnityEditor.Toolbars.MainToolbarContent`**: 텍스트, 툴팁, 이미지를 포함하는 컨텐츠 클래스 (아이콘 설정 시 `.image` 속성 사용 필수).

### 3.2 핵심 코드 구조 (Reference)

#### ToolbarSceneConfig (ScriptableObject)

씬 목록과 폴더 경로를 관리하는 데이터 에셋.

#### KarmoToolbar (Static Factory Method Pattern)

공식 샘플에서 권장하는 정적 팩토리 메서드 방식을 사용.

```csharp
public static class KarmoToolbar
{
    public const string ID = "KarmoTools/SceneSelector";

    [MainToolbarElement(ID, defaultDockPosition = MainToolbarDockPosition.Middle)]
    static IEnumerable<MainToolbarElement> CreateSceneSelector()
    {
        var content = new MainToolbarContent(activeSceneName, "툴팁");
        content.image = sceneIcon;
        yield return new MainToolbarDropdown(content, ShowSceneMenu);
    }

    private static void ShowSceneMenu(Rect worldBound) { /* GenericMenu 구성 및 표시 */ }
}
```

## 4. 사용 및 활성화 방법

1. **설정 생성**: `KarmoTools/Create Toolbar Config` 메뉴를 통해 설정 에셋 생성.
2. **씬 등록**: 에셋 인스펙터에서 `Favorite Scenes` 또는 `Target Folders` 설정.
3. **툴바 활성화**: 유니티 메인 툴바 우클릭 -> **"Customize Toolbar"** -> **"Karmo Scene Selector"** 드래그하여 추가.

## 5. 향후 확장 아이디어 (Roadmap)

- **빌드/배포 퀵 버튼**: 툴바에서 타겟 플랫폼 선택 후 즉시 빌드 실행.
- **Time Scale 제어**: 플레이 모드 속도 조절 슬라이더.
- **환경 설정 토글**: 개발/라이브 서버 환경을 원클릭으로 전환.
- **선택 히스토리**: 이전에 선택한 객체로 돌아가는 뒤로가기 버튼.

> **관리자**: Alisa (PM)
