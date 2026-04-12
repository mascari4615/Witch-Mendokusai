# KarmoEditor 히스토리 (History)

Summary: 유니티 에디터 확장 도구(KarmoEditor)의 주요 기능 업데이트, 툴바 API 통합 및 패키지 구조화 기록.

## 2026-01-21 (KST)

- **KarmoEditor 설정 리팩토링 및 빌드 시스템 고도화**:
  - **설정 네이밍 명확화**: `KarmoSettings`를 `KarmoEditorSettings`로 변경하고, `ApplicationMutexNames`, `ReflectionFieldResets` 등 필드명을 더 직관적으로 리팩토링함.
  - **빌드 윈도우(Build Helper) UI 통합**: 개별 버튼(`Build Only`, `Build & Run`, `Build & Deploy`)을 단일 `Build` 버튼과 체크박스 옵션(`Run after Build`, `Deploy after Build`)으로 통합하여 사용성 개선.
  - **워크플로우 개선**: `persona.md` 지침 강화를 통해 모든 작업 시 아티팩트 한글화 및 프로젝트별 자동 문서화 체계 구축.

## 2026-01-18 (KST)

- **KarmoEditor Professional Upgrade (Massive Feature Update)**:
  - **Project Settings 통합**: 모든 패키지 설정을 유니티 설정창(`Edit > Project Settings > KarmoLab`)으로 이전하여 중앙 관리 체계 구축.
  - **Quick Search (Ctrl+K) 연동**: `kl:` 필터를 통해 빌드 헬퍼, 뮤텍스 킬 등 패키지 주요 기능을 즉시 검색 및 실행 가능.
  - **Welcome Wizard (초기 설정 마법사)**: 패키지 설치 시 자동 실행되는 온보딩 시스템 구현. 설정 에셋 자동 생성 및 경로 가이드 제공.
  - **Localization (KR/EN)**: 패키지 내 UI와 메시지에 대한 한국어/영어 다국어 대응 시스템 구축.
  - **고급 UX 개선**:
    - `ReorderableList`를 적용한 커스텀 인스펙터로 씬/뮤텍스 목록 편집 조작감 향상.
    - 주요 기능 전용 단축키 바인딩 (빌드: `Ctrl+Alt+B`, 뮤텍스: `Ctrl+Alt+M`, 설정: `Ctrl+Alt+K`).
  - **표준 가이드라인 준수**: 상상용 패키지 규격에 맞춰 `README.md`, `CHANGELOG.md` 작성 및 에셋 저장 경로(`Assets/Settings/KarmoLab/KarmoEditor`) 정규화.
  - **기술 가이드 자산화**: 유니티 패키지 제작 노하우를 집대성한 `UnityPackage_CreationGuide.md` 기술 노트 작성.

## 2026-01-17 (KST)

- **KarmoEditor 툴바 기능 강화**:
  - **Custom Scene Selector**: Unity 6.3의 새로운 `MainToolbar` API를 활용하여 에디터 상단에 씬 전환 드롭다운 메뉴 추가.
  - **공식 API 패턴 적용**: `IEnumerable<MainToolbarElement>`를 반환하는 정적 메서드와 `MainToolbarDropdown`을 사용하는 최신 아키텍처 채택.
  - **데이터 기반 구성 (ScriptableObject)**: `ToolbarSceneConfig` 에셋을 통해 표시할 씬과 폴더를 자유롭게 설정 가능.
  - **자동 씬 검색**: 지정된 폴더 내의 모든 `.unity` 파일을 자동으로 스캔하여 메뉴에 포함.
  - **편의성 개선**: `KarmoTools/Create Toolbar Config` 메뉴로 원클릭 설정 생성 지원 및 실시간 활성 씬 이름 표시.

- **Unity Package 배포 및 모노레포 아키텍처 수립**:
  - **UPM(Unity Package Manager) 지원**: `com.mascari4615.karmo-editor` 패키지 구성을 위한 `package.json` 및 `.asmdef` 구축.
  - **Unity 6 최적화**: 최신 Unity 6 (6000.3) 버전을 최소 사양으로 지정하고 `karmo-editor`로 네이밍 간소화.
  - **모노레포 구조화 (Local Packages)**: 패키지들을 `Assets`가 아닌 `LocalPackages/` 폴더로 격리하여 관리하는 전문적인 아키텍처 도입. `manifest.json`을 통한 로컬 경로 참조 방식 채택.
  - **자동 배포 파이프라인 (CI)**: GitHub Actions를 활용하여 `main` 푸시 및 버전 태그(`패키지명/v*`) 생성 시 자동으로 배포용(`upm/*`) 브랜치를 갱신하는 워크플로우(`upm-publish.yml`) 구축.
  - **기술 가이드 제공**: 배포 전략, 프로젝트 구조, IDE 트러블슈팅 등을 통합한 `upm-package-distribution-guide.md` 작성.

- **명칭 및 네임스페이스 리팩토링 (Final Refinement)**:
  - **네임스페이스 통일**: 모든 코드의 네임스페이스를 `KarmoLab.KarmoEditor`로 변경하고, `MenuItem` 경로를 `KarmoLab`으로 단일화.
  - **ID/브랜드 일치**: `com.mascari4615.karmo-editor`, `KarmoDDrine` 등 유저 요청 명칭을 프로젝트 전반(package.json, docs, code)에 전수 적용.
  - **가이드 구축**: 프로젝트 통합 네이밍 규칙(`Naming_Convention_Guide.md`) 및 시멘틱 버저닝 가이드(`semantic-versioning-guide.md`) 작성.
  - **문서 정리**: `Doc/KarmoEditor` 폴더 구조를 인간 중심의 PascalCase로 정리하여 가독성 향상.

## 2026-01-13 (KST)

- **KarmoTools**:
  - **Build Helper**: Unity 에디터 전용 빌드 및 배포 도구 (`Assets/KarmoTools/Editor`).
  - **Smart Build**: `Prefix_Time_Memo` 자동 네이밍 및 경로 관리.
  - **Patch Mode**: 빌드 후 Live 경로로 즉시 덮어쓰기 기능 지원.
  - **Auto Cleanup**: 빌드 시 `DoNotShip` 등 불필요한 디버그 폴더 자동 삭제 옵션.
