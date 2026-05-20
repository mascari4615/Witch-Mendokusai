# Unity Cloud Build Integration (Option 2) — Setup Guide

> **NOTE**: Reference guide for TASK-WM-067 Option 2 adoption. Read before implementing.

## Overview

Unity Cloud Build (UCB) is Unity's official cloud build service.
- **Trigger**: GitHub repo poll (configurable)
- **Output**: Build results + artifact URLs
- **Integration**: Webhook → custom script → GitHub Release

## Prerequisites

- Unity Pro or Plus subscription (or Personal with free hour quota)
- Unity account linked to GitHub
- GitHub repo public or private (both supported)
- GitHub personal access token (for webhook script)

---

## Step 1: Set Up Unity Cloud Build Project

### 1.1 Create UCB Project

1. Go to https://build.cloud.unity.com
2. **New Project** → link GitHub repo (WitchMendokusai)
3. **Project Settings**:
   - Name: `WitchMendokusai` (or any ID)
   - GitHub branch: `main`
   - Poll frequency: On every push (or hourly)

### 1.2 Configure Build Targets

For each platform (Linux, Windows, macOS):

1. **Add Build Target**
   - Platform: StandaloneLinux64 / StandaloneWindows64 / StandaloneOSX
   - Build method: Auto-detect or custom
   - Compression: Enabled
   - Output format: ZIP

2. **Build Settings** per target:
   - Scene list: Assets/Scenes/Intro.unity
   - Scripting backend: Mono (or IL2CPP if preferred)
   - Strip engine code: Yes (reduce size)

3. **Advanced**:
   - Timeout: 60 minutes
   - Post-build: (leave empty, we'll use webhook instead)

### 1.3 Get API Credentials

In UCB project settings:

- **API Token**: Generate in account settings (https://build.cloud.unity.com/account)
- **Organization ID** + **Project ID**: Found in URL or project settings

Save these in secure location.

---

## Step 2: GitHub Webhook Integration

### 2.1 Create Webhook Secret

In GitHub repo settings:

1. **Settings** → **Webhooks** → **Add webhook**
2. Payload URL: `https://your-endpoint.com/webhook/ucb-release`
   (Or use GitHub Actions to receive webhook)
3. Content type: `application/json`
4. Secret: Generate strong random string (save it)

### 2.2 Create GitHub Actions Webhook Handler

File: `.github/workflows/ucb-webhook-handler.yml`

```yaml
name: UCB Webhook → Release Attach

on:
  # Option A: Incoming webhook (requires external endpoint)
  # Use: https://github.com/actions/github-script or
  # https://github.com/gitpod-io/workspace-full/blob/main/.github/workflows/notify.yml

  # Option B: Scheduled poll (simpler for self-hosted)
  schedule:
    - cron: '*/15 * * * *'  # Every 15 minutes

  # Option C: Manual dispatch
  workflow_dispatch:
    inputs:
      ucb_build_id:
        description: 'UCB Build ID (optional)'
        required: false

jobs:
  check-ucb-builds:
    runs-on: ubuntu-latest
    steps:
      - name: Fetch latest UCB build
        env:
          UCB_API_TOKEN: ${{ secrets.UCB_API_TOKEN }}
          UCB_ORG_ID: ${{ secrets.UCB_ORG_ID }}
          UCB_PROJECT_ID: ${{ secrets.UCB_PROJECT_ID }}
        run: |
          set -euo pipefail

          # Query UCB API for latest builds
          BUILDS=$(curl -s \
            -H "Authorization: Bearer ${UCB_API_TOKEN}" \
            "https://build-api.cloud.unity.com/api/v1/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/builds" \
            | jq -r '.builds[] | select(.status=="success") | .id' | head -5)

          echo "Latest successful builds:"
          echo "$BUILDS"

      - name: Download build artifacts + attach to GitHub Release
        env:
          UCB_API_TOKEN: ${{ secrets.UCB_API_TOKEN }}
          UCB_ORG_ID: ${{ secrets.UCB_ORG_ID }}
          UCB_PROJECT_ID: ${{ secrets.UCB_PROJECT_ID }}
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          set -euo pipefail

          BUILD_ID="${{ github.event.inputs.ucb_build_id }}"

          # If empty, get latest success build
          if [ -z "$BUILD_ID" ]; then
            BUILD_ID=$(curl -s \
              -H "Authorization: Bearer ${UCB_API_TOKEN}" \
              "https://build-api.cloud.unity.com/api/v1/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/builds?status=success" \
              | jq -r '.builds[0].id')
          fi

          if [ -z "$BUILD_ID" ]; then
            echo "No successful builds found"
            exit 0
          fi

          echo "Processing UCB Build: $BUILD_ID"

          # Get build metadata
          BUILD_META=$(curl -s \
            -H "Authorization: Bearer ${UCB_API_TOKEN}" \
            "https://build-api.cloud.unity.com/api/v1/orgs/${UCB_ORG_ID}/projects/${UCB_PROJECT_ID}/builds/${BUILD_ID}")

          BUILD_TARGET=$(echo "$BUILD_META" | jq -r '.buildTargetName')
          BUILD_STATUS=$(echo "$BUILD_META" | jq -r '.status')

          echo "Build Target: $BUILD_TARGET, Status: $BUILD_STATUS"

          # Download artifact (if available)
          ARTIFACT_URL=$(echo "$BUILD_META" | jq -r '.links.downloadPrimary.href // empty')

          if [ -z "$ARTIFACT_URL" ]; then
            echo "No artifact URL found"
            exit 0
          fi

          # Save artifact
          FILENAME="WitchMendokusai-$(date +%Y%m%d)-${BUILD_TARGET}.zip"
          curl -L -o "$FILENAME" "$ARTIFACT_URL"

          # Get latest release tag
          LATEST_TAG=$(gh release list --limit 1 --json tagName -q '.[] | .tagName')

          if [ -z "$LATEST_TAG" ]; then
            echo "No releases found"
            exit 0
          fi

          # Attach to release
          echo "Attaching $FILENAME to release $LATEST_TAG"
          gh release upload "$LATEST_TAG" "$FILENAME" --clobber

      - name: Mark build as processed
        run: |
          # Optional: Update a tracking file to avoid duplicate processing
          echo "Build processed at $(date)" >> /tmp/ucb-processed.txt
```

### 2.3 Store Secrets

In GitHub repo settings:

- `UCB_API_TOKEN`: from UCB account
- `UCB_ORG_ID`: Unity organization ID
- `UCB_PROJECT_ID`: UCB project ID

---

## Step 3: Testing

### 3.1 Trigger UCB Build Manually

1. Go to UCB project dashboard
2. Click **Build** on any target
3. Wait ~15 minutes for build completion
4. Check UCB dashboard for artifact URL

### 3.2 Test Webhook/Polling

Option A (Poll-based):
```bash
# Manually trigger GitHub Actions job
gh workflow run ucb-webhook-handler.yml
```

Option B (Incoming webhook):
```bash
# Simulate webhook from UCB
curl -X POST https://api.github.com/repos/karmoddrine/WitchMendokusai/dispatches \
  -H "Authorization: Bearer $GH_TOKEN" \
  -d '{"event_type":"ucb_build_ready","client_payload":{"build_id":"..."}}'
```

### 3.3 Verify Release

```bash
gh release view <tag> --json assets
# Should list all artifacts attached
```

---

## Limitations & Considerations

| Aspect | Notes |
|--------|-------|
| **Automation** | Requires polling or incoming webhook (not real-time) |
| **Cost** | Personal: free hour quota/month; Pro/Plus: higher limit |
| **Integration** | GitHub Release must be created first (by WM-066 release.yml) |
| **Failure handling** | Poll script must handle network errors, missing releases, etc. |
| **Maintenance** | UCB API changes → script may need updates |

---

## When to Migrate to Option 2

- Testers > 5 people (builds become frequent)
- Weekly or daily release cadence
- Multi-platform builds take > 1 hour on local machine
- Cost acceptable (or team subscriptions available)

## Reverting to Option 3 (Local Build)

If UCB setup becomes too complex:
1. Delete `.github/workflows/ucb-webhook-handler.yml`
2. Remove UCB secrets from GitHub
3. Return to manual build + attach workflow

---

## References

- UCB Docs: https://docs.unity.com/cloud-build/
- UCB API: https://build-api.cloud.unity.com/docs/
- WM-066 release.yml: Current tag → release creation flow
