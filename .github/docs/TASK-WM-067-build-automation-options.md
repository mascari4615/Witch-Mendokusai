# TASK-WM-067: Build Automation Options Analysis

## Overview

WM-066 (Release flow) 구현 완료 → tag push 시 자동 CHANGELOG + bundleVersion 업데이트. 
**본 문서**: 빌드 자동화 옵션 분석 및 선택 가이드.

## 현재 상태

- **trigger**: `git tag v0.0.X && git push origin v0.0.X`
- **자동 실행**: CHANGELOG 생성, bundleVersion 업데이트, GitHub Release 생성
- **누락**: Linux/Win/Mac 빌드 자동화 (현재는 사용자 수동 또는 local PC build)

---

## Option 1: game-ci/unity-builder (Third-party GitHub Action)

### 개요
- 가장 흔한 CI 패턴
- GitHub-hosted runner (ubuntu, windows 등 선택 가능)
- 자동 Unity license activation + build 아웃풋 처리
- https://game.ci

### 장점
- ✅ GitHub Actions 생태계 기본. 많은 사례·documentation
- ✅ Linux runner 무료 (월 2000분 quota, Personal)
- ✅ Build output → GitHub Release 자동 attach 쉬움 (action 내장)
- ✅ `release.yml` 에 1개 job 추가로 구현 가능

### 단점
- ❌ Third-party action → signature/검증 비용 (memo `feedback_no_third_party_without_consent.md` 적용)
- ❌ Windows runner 시간 비쌈 (기본 quota 초과 시 과금)
- ❌ 빌드 시간 길음 (15~30분, runner 스펙 따라)
- ❌ Personal license 한도: 월 시간 제한 있음

### 구현 흐름
1. Unity Personal License activation file → secret 등록
2. `release.yml` 에 build job 추가
   ```yaml
   build:
     strategy:
       matrix:
         targetPlatform:
           - StandaloneLinux64
           - StandaloneWindows64
           - StandaloneOSX
     runs-on: ubuntu-latest
     steps:
       - uses: game-ci/unity-builder@v4
         with:
           unityVersion: ...
           targetPlatform: ${{ matrix.targetPlatform }}
   ```
3. Build output → Release attach

### 요구사항
- Unity Personal 라이선스 activation file (`.lic`)
- GitHub secret: `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`
- Self-signed 또는 공개 cert 필요할 수 있음

---

## Option 2: Unity Cloud Build (Unity 공식)

### 개요
- Unity 공식 클라우드 빌드 서비스
- Cloud 기반 build farm
- GitHub repo poll → 자동 빌드
- Personal: 월 무료 hour 한도 (일정량 초과 시 유료)

### 장점
- ✅ Unity 공식 → 신뢰 비용 ↑ (third-party action 없음)
- ✅ Mac/iOS/Android 등 다양한 플랫폼 지원
- ✅ Cloud 관리 → runner 유지보수 X
- ✅ 빌드 결과 page + 히스토리 관리

### 단점
- ❌ GitHub Release auto-attach 안 됨. webhook + script 별도 필요
- ❌ Personal 한도 초과 시 유료 (출시 후 결정 필요)
- ❌ GitHub 과 별도 플랫폼 (Unity 계정 연동 필수)
- ❌ 초기 setup 복잡 (webhook, API token, 스크립트)

### 구현 흐름
1. Unity Cloud Build 프로젝트 생성
2. GitHub 연동 + webhook 설정
3. Post-build script: UCB result → GitHub Release upload
   ```bash
   # UCB API → build result download
   # GitHub API → Release attach
   ```

### 요구사항
- Unity Plus/Pro 계정 (또는 Personal 한도 내)
- UCB API token
- GitHub API token + release script 작성

---

## ★ Option 3: Local Build + Manual Attach (Default 추천)

### 개요
- **자동화 0**. 사용자가 local PC에서 Unity build → `.zip` → GitHub Release 수동 업로드
- 기존 WM-066 (release intent 자동) 위에 사람이 마지막 1km
- 게임 0.0.5 초기 단계에 충분

### 장점
- ✅ 자동화 비용 0
- ✅ 신뢰성 ↑ (사용자가 직접 검증)
- ✅ 라이선스·러너·비용 문제 없음
- ✅ 첫 release 빠르게 진행 가능
- ✅ 추후 O1/O2로 업그레이드 가능

### 단점
- ❌ 완전 수동 (사용자 책임)
- ❌ 일관성 (빌드 환경 편차 가능)
- ❌ 테스터 확대 시 배포 속도 병목

### 구현 흐름
1. TASK 문서 / wiki 에 가이드 작성:
   - `export BUNDLEVERSION=$(grep bundleVersion ProjectSettings/ProjectSettings.asset | awk '{print $2}')`
   - Unity Editor > File > Build
   - GitHub Release UI > Attach files
2. Conventional 정하기: `Builds/WitchMendokusai-v0.0.X-<platform>.zip`

### 요구사항
- 배포 가이드 문서 (한 번만)
- 사용자 시간 (빌드당 ~5분)

---

## 결정 기준

| 상황 | 추천 |
|------|------|
| **지금** (0.0.5, 베타 테스터 1명) | O3 (Local + 수동) |
| **추후** (테스터 5+ 명, 주간 build) | O1 (game-ci) 또는 O2 (UCB) |
| **기업/팀 빌드** (24/7 CI/CD) | O1 (game-ci) + license 정리 |

---

## Next Steps (사용자 선택 대기)

**A. 옵션 선택 필수**

- **O1 선택** → B: game-ci action + secret setup + build job 추가
- **O2 선택** → C: UCB setup + webhook + GitHub Release script
- **O3 선택** → D: 배포 가이드 문서 작성

이 PR 은 **draft** 상태 → 선택 후 진행.

---

## References

- game-ci docs: https://game.ci/docs
- Unity Cloud Build: https://unity.com/products/cloud-build
- WM-066 (Release flow): `.github/workflows/release.yml`
- CLAUDE.md: `memo/CLAUDE.md § 자동화 우선` (시스템 도입 패턴)
