using System;
using System.Collections.Generic;
using System.IO;
using SceneContent.Storage;
using SceneContent.Unity;
using SceneSequence;
using SceneSequence.Storage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.SequenceLibrary
{
    public sealed partial class SequenceLibraryMenu
    {
        [SerializeField] private int contentLayer = 31;
        [SerializeField] private float fieldOfView = 35;
        [SerializeField] private float framePadding = 1.65f;
        [SerializeField] private float frameOffset = .2f;
        [SerializeField] private Vector3 fallbackLightAngles = new Vector3(40, -30, 0);
        private GameObject stage;
        private ContentScenePlayer player;
        private readonly Dictionary<Camera, bool> cameras = new Dictionary<Camera, bool>();
        private readonly Dictionary<Canvas, bool> canvases = new Dictionary<Canvas, bool>();
        private SkyDirector lobbySky;
        private bool lobbySkyEnabled;

        public void LoadSequence(string file)
        {
            if (opened == false) throw new InvalidOperationException("Open the sequence library before playback");
            GameObject candidate = null;
            try
            {
                SequenceDocument sequence = SequenceFiles.Read(file);
                candidate = new GameObject("Sequence stage");
                candidate.SetActive(false);
                candidate.transform.SetParent(transform, false);
                ContentScenePlayer next = candidate.AddComponent<ContentScenePlayer>();
                StandardSurfaceFactory surfaces = new StandardSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("WM/Toon Surface"));
                next.Initialize(sequence, new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(file))), surfaces, useUnscaledTime: true);
                PrepareCamera(candidate, next.Instance);
                StopSequence();
                CaptureLobby();
                stage = candidate;
                player = next;
                candidate.SetActive(true);
                entries.style.display = DisplayStyle.None;
                transport.style.display = DisplayStyle.Flex;
                panel.style.backgroundColor = Color.clear;
                float duration = 0;
                foreach (Motion.PoseClip clip in sequence.Clips) duration = Mathf.Max(duration, clip.Duration);
                timeline.highValue = duration;
                timeline.SetEnabled(duration > 0);
                play.SetEnabled(duration > 0);
                timeline.SetValueWithoutNotify(0);
                status.text = Path.GetFileName(file).Replace(".sequence.json", "");
                player.Play();
                play.Focus();
            }
            catch (Exception exception)
            {
                if (candidate != null) Destroy(candidate);
                StopSequence();
                entries.style.display = DisplayStyle.Flex;
                transport.style.display = DisplayStyle.None;
                panel.style.backgroundColor = new Color(.08f, .09f, .12f, 1);
                status.text = "연출을 열지 못했어요: " + exception.Message;
            }
        }

        private void PrepareCamera(GameObject owner, SceneInstance scene)
        {
            foreach (Transform part in scene.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = contentLayer;
            foreach (Light authoredLight in scene.GetComponentsInChildren<Light>(true)) authoredLight.cullingMask = 1 << contentLayer;
            Camera preview = null;
            foreach (Camera camera in scene.GetComponentsInChildren<Camera>(true))
            {
                camera.cullingMask = 1 << contentLayer;
                if (camera.enabled && preview == null) preview = camera;
            }
            if (preview == null) preview = CreateFallbackCamera(owner, scene);
            if (scene.GetComponentsInChildren<Light>(true).Length > 0) return;
            Light light = new GameObject("Sequence light").AddComponent<Light>();
            light.transform.SetParent(owner.transform, false);
            light.transform.rotation = preview.transform.rotation * Quaternion.Euler(fallbackLightAngles);
            light.type = LightType.Directional;
            light.cullingMask = 1 << contentLayer;
            light.intensity = 1;
        }

        private Camera CreateFallbackCamera(GameObject owner, SceneInstance scene)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
            bool first = true;
            foreach (Renderer renderer in scene.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Camera preview = new GameObject("Sequence camera").AddComponent<Camera>();
            preview.transform.SetParent(owner.transform, false);
            preview.fieldOfView = fieldOfView;
            preview.clearFlags = CameraClearFlags.SolidColor;
            preview.backgroundColor = new Color(.13f, .16f, .2f);
            preview.cullingMask = 1 << contentLayer;
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(.1f, (float)Screen.width / Screen.height));
            float distance = Mathf.Max(.1f, halfHeight / Mathf.Tan(fieldOfView * Mathf.Deg2Rad * .5f) * framePadding);
            preview.nearClipPlane = Mathf.Max(.001f, distance * .001f);
            preview.farClipPlane = distance + bounds.size.magnitude * 2 + 1;
            preview.transform.position = bounds.center + Vector3.forward * distance;
            preview.transform.LookAt(bounds.center + Vector3.up * halfHeight * frameOffset);
            return preview;
        }

        private void CaptureLobby()
        {
            if (SkyDirector.TryGetExistingInstance(out lobbySky))
            {
                lobbySkyEnabled = lobbySky.enabled;
                lobbySky.enabled = false;
            }
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) { cameras.Add(camera, camera.enabled); camera.enabled = false; }
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) { canvases.Add(canvas, canvas.enabled); canvas.enabled = false; }
            }
        }

        private void StopSequence()
        {
            if (player != null) player.Unload();
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            player = null;
            stage = null;
            foreach (KeyValuePair<Camera, bool> entry in cameras) if (entry.Key != null) entry.Key.enabled = entry.Value;
            foreach (KeyValuePair<Canvas, bool> entry in canvases) if (entry.Key != null) entry.Key.enabled = entry.Value;
            cameras.Clear();
            canvases.Clear();
            if (lobbySky != null) lobbySky.enabled = lobbySkyEnabled;
            lobbySky = null;
        }

        private void TogglePlayback()
        {
            if (player == null || player.IsLoaded == false) return;
            if (player.IsPlaying) player.Pause();
            else { if (player.Time >= timeline.highValue) player.Seek(0); player.Play(); }
        }

        private void Update()
        {
            if (player == null || player.IsLoaded == false) return;
            timeline.SetValueWithoutNotify(player.Time);
            if (player.Failure != null) status.text = PlaybackFailure(player.Failure);
            play.text = player.IsPlaying ? "일시 정지" : "재생";
        }

        private static string PlaybackFailure(string detail)
        {
            if (detail == "Dynamics seek exceeds the step budget; advance in smaller intervals")
                return "한 번에 옮기기엔 시간이 너무 멀어요. 가까운 시간부터 나눠 이동하세요.";
            if (detail == "Obstacle encloses the particle reach" || detail == "Length and collision constraints could not be satisfied")
                return "충돌 모양과 연결 길이가 맞지 않아요. 편집기에서 반경이나 배치를 고쳐 다시 내보내세요.";
            return "연출 설정을 계산하지 못했어요. 편집기에서 확인하고 다시 내보내세요.";
        }
    }
}
