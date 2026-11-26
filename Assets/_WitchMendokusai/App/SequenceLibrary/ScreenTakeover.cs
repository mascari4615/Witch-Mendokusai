using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.SequenceLibrary
{
    // 게임 화면을 3D 화면에 넘겨주고 되돌리기. 한 번에 한 주인만.
    // 겹치면 두 번째가 꺼진 상태를 원래 값으로 기억해 복구 불가라서 둘째는 거부
    // 소비자: 저장된 연출 (SequenceStage), 3D 배우 무대 (DialogueCastStage)
    public sealed class ScreenTakeover
    {
        private static ScreenTakeover holder;
        private readonly string ownerName;
        private readonly Dictionary<Camera, int> cameras = new Dictionary<Camera, int>();
        private readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
        private SkyDirector capturedSky;
        private bool capturedSkyEnabled;

        public ScreenTakeover(string ownerName)
        {
            this.ownerName = ownerName;
        }

        public bool IsHeld { get { return holder == this; } }

        public void EnsureFree()
        {
            if (holder != null && holder != this)
                throw new InvalidOperationException("다른 3D 화면이 쓰는 중이다 (" + holder.ownerName + "). 그쪽을 닫은 뒤 다시 열어야 한다");
        }

        // 지금 씬의 카메라는 켠 채 그리는 층만 비움 (Camera.main 을 매 프레임 쓰는 게임 코드가 있음). Canvas, 하늘 조절은 끄기. UI Toolkit 대화창은 계속 보임
        public void Take()
        {
            EnsureFree();
            if (holder == this) return;
            holder = this;
            if (SkyDirector.TryGetExistingInstance(out capturedSky))
            {
                capturedSkyEnabled = capturedSky.enabled;
                capturedSky.enabled = false;
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) { cameras.Add(camera, camera.cullingMask); camera.cullingMask = 0; }
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) { canvases.Add(canvas, canvas.enabled); canvas.enabled = false; }
            }
        }

        public void Release()
        {
            foreach (KeyValuePair<Camera, int> entry in cameras) if (entry.Key != null) entry.Key.cullingMask = entry.Value;
            foreach (KeyValuePair<Canvas, bool> entry in canvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
            cameras.Clear();
            canvases.Clear();
            if (capturedSky != null) capturedSky.enabled = capturedSkyEnabled;
            capturedSky = null;
            if (holder == this) holder = null;
        }
    }
}
