using System;
using System.IO;
using SceneContent.Storage;
using SceneContent.Unity;
using SceneSequence;
using SceneSequence.Storage;
using UnityEngine;
using WitchMendokusai.ContentRuntime;

namespace WitchMendokusai.SequenceLibrary
{
    // 저장된 연출 하나를 게임 화면 대신 표시, 넘겨받은 카메라와 화면은 원래대로 복구
    // 소비자: 로비 연출 보관함, 대화의 `@연출`
    public sealed class SequenceStage : MonoBehaviour
    {
        [SerializeField] private int contentLayer = 31;
        [SerializeField] private float cameraDepth = 100;
        [SerializeField] private float fieldOfView = 35;
        [SerializeField] private float framePadding = 1.65f;
        [SerializeField] private float frameOffset = .2f;
        [SerializeField] private Vector3 fallbackLightAngles = new Vector3(40, -30, 0);
        private GameObject stage;
        private ContentScenePlayer player;
        private readonly ScreenTakeover takeover = new ScreenTakeover("저장된 연출");

        public ContentScenePlayer Player { get { return player; } }
        public bool IsShowing { get { return player != null && player.IsLoaded; } }
        public float Duration { get; private set; }

        // 새 연출을 다 세운 뒤에만 화면 교체. 실패면 예외, 이전 화면 유지
        public void Show(string file)
        {
            GameObject candidate = new GameObject("Sequence stage");
            candidate.SetActive(false);
            candidate.transform.SetParent(transform, false);
            try
            {
                SequenceDocument sequence = SequenceFiles.Read(file);
                ContentScenePlayer next = candidate.AddComponent<ContentScenePlayer>();
                StandardSurfaceFactory surfaces = new StandardSurfaceFactory(Shader.Find("Universal Render Pipeline/Unlit"), Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("WM/Toon Surface"));
                next.Initialize(sequence, new DirectoryContentSource(Path.GetDirectoryName(Path.GetFullPath(file))), surfaces, useUnscaledTime: true);
                PrepareCamera(candidate, next.Instance);
                takeover.EnsureFree();
                Hide();
                stage = candidate;
                player = next;
                CaptureScreen();
                foreach (Motion.PoseClip clip in sequence.Clips) Duration = Mathf.Max(Duration, clip.Duration);
                candidate.SetActive(true);
                player.Play();
            }
            catch
            {
                // 화면을 넘겨받은 뒤의 실패는 복구, 그 전의 실패는 후보만 폐기
                if (stage == candidate) Hide();
                else Destroy(candidate);
                throw;
            }
        }

        public void Hide()
        {
            if (player != null) player.Unload();
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            player = null;
            stage = null;
            Duration = 0;
            takeover.Release();
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void PrepareCamera(GameObject owner, SceneInstance scene)
        {
            foreach (Transform part in scene.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = contentLayer;
            foreach (Light authoredLight in scene.GetComponentsInChildren<Light>(true)) authoredLight.cullingMask = 1 << contentLayer;
            Camera preview = null;
            foreach (Camera camera in scene.GetComponentsInChildren<Camera>(true))
            {
                camera.cullingMask = 1 << contentLayer;
                camera.depth = cameraDepth;
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
            preview.depth = cameraDepth;
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

        private void CaptureScreen()
        {
            takeover.Take();
        }
    }
}
