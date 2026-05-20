# WitchMendokusai Build & Release Manual Guide

> **Scope**: Option 3 (TASK-WM-067) — Local build + GitHub Release manual attach

## Prerequisites

- Unity Editor (same version as project)
- All platforms target build support (Linux, Windows, macOS)
- GitHub CLI (`gh`) installed and authenticated

## Release Process (Step-by-step)

### 1. Create Release Tag (Automated by release.yml)

```bash
# main branch, all changes committed
git checkout main
git pull origin main

# Create release tag
git tag v0.0.5  # or v0.1.0 etc.
git push origin v0.0.5
```

**Result**: GitHub Actions auto-runs:
- ✅ CHANGELOG.md updated (Conventional Commits)
- ✅ ProjectSettings.asset bundleVersion bumped
- ✅ GitHub Release created (empty, awaiting build attachments)

**Verify**:
```bash
gh release view v0.0.5
# Should show: Draft (if no files attached yet)
```

### 2. Local Build (All Platforms)

#### 2.1 Get Version from release.yml

```bash
RELEASE_VERSION=$(gh release view --json tagName -q '.tagName' | sed 's/v//')
# or manual: RELEASE_VERSION="0.0.5"
echo "Building version: $RELEASE_VERSION"
```

#### 2.2 Build via Unity Batch Mode (Recommended)

Create `build-all-platforms.sh`:

```bash
#!/bin/bash
set -euo pipefail

RELEASE_VERSION="${1:-0.0.5}"
PROJECT_PATH="$(pwd)"
BUILD_OUTPUT_DIR="Builds/Release-${RELEASE_VERSION}"

mkdir -p "$BUILD_OUTPUT_DIR"

echo "=== Building WitchMendokusai v${RELEASE_VERSION} ==="

build_platform() {
  local PLATFORM="$1"
  local OUTPUT="$2"
  local SCENES="Assets/Scenes/Intro.unity"
  
  echo "Building $PLATFORM..."
  
  unity -projectPath "$PROJECT_PATH" \
    -executeMethod BuildScript.BuildPlayer \
    -logFile - \
    -batchmode \
    -nographics \
    -quit \
    -buildTarget "$PLATFORM" \
    BUILD_OUTPUT="$OUTPUT" \
    BUNDLEVERSION="$RELEASE_VERSION"
  
  if [ -f "$OUTPUT" ]; then
    echo "✓ $PLATFORM complete: $OUTPUT"
  else
    echo "✗ $PLATFORM FAILED"
    exit 1
  fi
}

# macOS (if running on macOS)
# build_platform StandaloneOSX "$BUILD_OUTPUT_DIR/WitchMendokusai-${RELEASE_VERSION}-macOS.zip"

# Windows
build_platform StandaloneWindows64 "$BUILD_OUTPUT_DIR/WitchMendokusai-${RELEASE_VERSION}-Windows.zip"

# Linux
build_platform StandaloneLinux64 "$BUILD_OUTPUT_DIR/WitchMendokusai-${RELEASE_VERSION}-Linux.zip"

echo "=== All builds complete ==="
ls -lh "$BUILD_OUTPUT_DIR"
```

#### 2.3 Manual Build (via Unity Editor)

If batch mode fails or unavailable:

1. **File > Build Settings**
2. Select **Target Platform**: StandaloneWindows64 (or Linux64 / macOS)
3. Verify scenes in build list
4. **Player Settings**:
   - Product Name: `WitchMendokusai`
   - Version: `0.0.5` (match release tag)
   - Bundle Version Code: `5` (for Android if future)
5. **Build**
   - Output: `Builds/WitchMendokusai-0.0.5-Windows.zip`
   - Compression: Enabled
6. Repeat for each platform (Linux64, macOS)

**Tips**:
- Build once per platform on that OS (Windows build on Windows, macOS on macOS)
- Cross-platform builds possible but slower + potential runtime issues

### 3. Attach Builds to GitHub Release

#### 3.1 Create `.zip` files

```bash
# If build scripts created dirs, zip them:
cd Builds/Release-0.0.5

zip -r WitchMendokusai-0.0.5-Windows.zip WitchMendokusai-Windows/
zip -r WitchMendokusai-0.0.5-Linux.zip WitchMendokusai-Linux/
zip -r WitchMendokusai-0.0.5-macOS.zip WitchMendokusai-macOS/
```

#### 3.2 Upload via GitHub CLI

```bash
gh release upload v0.0.5 \
  Builds/Release-0.0.5/WitchMendokusai-0.0.5-Windows.zip \
  Builds/Release-0.0.5/WitchMendokusai-0.0.5-Linux.zip \
  Builds/Release-0.0.5/WitchMendokusai-0.0.5-macOS.zip
```

Or via GitHub Web UI:
1. https://github.com/karmoddrine/WitchMendokusai/releases/tag/v0.0.5
2. **Edit** draft release
3. Drag & drop `.zip` files
4. **Publish**

### 4. Verify Release

```bash
gh release view v0.0.5 --json assets -q '.assets[].name'
# Should list:
# WitchMendokusai-0.0.5-Windows.zip
# WitchMendokusai-0.0.5-Linux.zip
# WitchMendokusai-0.0.5-macOS.zip
```

---

## Automation Path (Future: Option 1 or 2)

If manual build becomes bottleneck (test team > 3 people, weekly builds):

### Option 1: game-ci/unity-builder (GitHub Actions)
- Add build job to `.github/workflows/release.yml`
- Automatic: tag push → build all platforms → attach to release
- Cost: Personal quota or paid minutes

### Option 2: Unity Cloud Build
- Setup webhook: UCB → GitHub Release API
- Automatic: GitHub repo poll → build → attach
- Cost: Personal hour quota (scalable if needed)

---

## Troubleshooting

### Build fails: "Unity not found"

**Solution**: Use full path or ensure Unity in `PATH`:
```bash
/Applications/Unity/Hub/Editors/2023.2.X/Unity.app/Contents/MacOS/Unity  # macOS
"C:\Program Files\Unity\Hub\Editors\2023.2.X\Editor\Unity.exe"          # Windows
```

### ZIP creation fails

```bash
# Install zip tool if missing (macOS/Linux)
brew install zip      # macOS
sudo apt install zip  # Ubuntu/Debian

# Or use 7-zip / tar
tar -czf output.tar.gz folder/
```

### Release not found

```bash
git fetch origin  # Ensure local tag sync
gh release list --limit 5
```

---

## Checklist (Before Publish)

- [ ] All builds tested locally
- [ ] No console errors in Editor.log
- [ ] Game launches and loads intro scene
- [ ] Version string matches tag (v0.0.5)
- [ ] GitHub Release contains all 3 platform zips
- [ ] Release notes auto-generated from CHANGELOG.md

---

## File Naming Convention

Stick to:
```
WitchMendokusai-<version>-<platform>.zip
  v0.0.5-Windows.zip
  v0.0.5-Linux.zip
  v0.0.5-macOS.zip
```

Rationale: Consistent, sortable, clear platform.
