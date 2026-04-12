# KarmoEditor 툴바 확장 아이디어 (Toolbar Ideas)

Summary: Unity 6.3의 MainToolbar API를 활용하여 작업 생산성을 극대화할 수 있는 추가 기능 제안 목록.

## 1. 빌드 및 배포 도구 (Build & Deploy)

- **명칭**: Quick Build
- **기능**: `BuildToolWindow`의 기능을 툴바에서 즉시 실행.
- **UI**:
  - 최근 빌드 타겟 표시 드롭다운.
  - 빌드/배포 즉시 실행 버튼.

## 2. 프로젝트 통계 (Project Stats)

- **명칭**: Stats Monitor
- **기능**: 런타임 성능 지표 실시간 확인.
- **UI**:
  - FPS, 현재 메모리 사용량 텍스트 레이블.

## 3. 환경설정 토글 (Environment Context)

- **명칭**: Environment
- **기능**: 개발/스테이징/라이브 환경에 따른 Script Define symbols 또는 전역 설정을 원클릭 전환.
- **UI**: 환경 이름 표시 드롭다운.

## 4. 스크린샷 및 녹화 (Media Capture)

- **명칭**: Snap
- **기능**: UI를 제외한 순수 Game View 스크린샷 저장 또는 비디오 녹화 시작/정지.
- **UI**: 카메라 아이콘(스냅샷), 녹화 버튼.

**기술적 구현 제언**:

- 각 기능은 `MainToolbarElement` 속성을 가진 개별 정적 메서드로 구현하여 모듈화할 수 있음.
- 환경설정(`ScriptableObject`)을 통해 사용자가 원하는 툴바 항목만 활성화하도록 관리하는 것이 좋음.
