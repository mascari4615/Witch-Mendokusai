# KarmoEditor 씬 툴바 설계 (Scene Toolbar Design)

Summary: 유니티 상단 툴바에 씬 전환 드롭다운 기능을 통합하기 위한 데이터 구조 및 UI 구현 설계 명세.

## 1. 개요

Unity 6의 새로운 Toolbar API를 사용하여 에디터 상단에 직접 씬 전환 메뉴를 노출.
모든 씬을 보여주는 대신, 프로젝트의 ScriptableObject 설정을 통해 원하는 씬과 폴더만을 선택적으로 노출하여 작업 효율을 높음.

## 2. 주요 기능

1. **Custom Main Toolbar Dropdown**:
   - Unity 6의 **Main Toolbar** 영역에 드롭다운 배치.
   - 현재 활성화된 씬 이름을 표시.
   - 클릭 시 설정된 씬 목록을 출력하고, 선택 시 해당 씬으로 즉시 이동.

2. **ToolbarSceneConfig (ScriptableObject)**:
   - 드롭다운에 표시할 `SceneAsset` 목록을 리스트로 관리.
   - 특정 폴더(`DefaultAsset` 혹은 경로 지정) 내의 모든 씬을 자동으로 포함하는 기능.
   - 설정 파일은 `Assets/KarmoEditor/Settings/` 폴더 내에 생성 및 관리.

3. **씬 전환 로직**:
   - `EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()`를 호출하여 변경사항 저장 여부 확인.
   - `EditorSceneManager.OpenScene()`을 통해 씬 로드.

## 3. 데이터 구조

### ToolbarSceneConfig

- `List<SceneAsset> FavoriteScenes`: 직접 등록한 씬 목록.
- `List<DefaultAsset> TargetFolders`: 씬을 자동으로 긁어올 폴더 목록.
- `bool ShowOnlyBuildSettingsScenes`: 빌드 설정 포함 씬만 필터링 여부 (추가 제안).

## 4. 구현 계획

### 1단계: ScriptableObject 생성

- `KarmoTools.Editor.Toolbar` 네임스페이스에 `ToolbarSceneConfig` 클래스 구현.

### 2단계: Toolbar UI 구현

- `MainToolbarElement` 속성을 **정적 팩토리 메서드**에 사용하여 메인 툴바에 등록.
- `EditorToolbarDropdown`을 상속받아 드롭다운 UI 구현.

### 3단계: 씬 검색 및 필터링

- 설정된 폴더 내의 `.unity` 파일을 검색하여 드롭다운 아이템 생성.
