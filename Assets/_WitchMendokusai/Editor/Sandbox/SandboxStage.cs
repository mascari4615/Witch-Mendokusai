using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.Sandbox
{
	// 격리 미니 무대 — 빈 additive 씬 + 땅 + 조명 + 데모, Scene View 오토프레임, 에디트 모드 라이브 틱.
	// 열린 씬(World 등) 무접촉: additive=언로드 X·저장 프롬프트 X, Close 시 이전 active 씬 복원. Play 미사용.
	public static class SandboxStage
	{
		private const string STAGE_SCENE_NAME = "WM Sandbox";
		private const float GROUND_SCALE = 2f;
		private const float MIN_TICK_INTERVAL = 0.1f;

		private static Scene activeStage;
		private static Scene previousActive;
		private static GameObject activeRoot;
		private static ISandboxAnimatedDemo activeAnimated;
		private static double nextTickTime;

		public static bool HasActiveStage => activeRoot != null;

		// 데모를 격리 무대에 띄운다(에디트 모드). 기존 무대 있으면 먼저 정리.
		public static GameObject Open(ISandboxDemo demo)
		{
			if (demo == null)
			{
				Debug.LogError("[Sandbox] demo == null");
				return null;
			}

			Close();

			previousActive = SceneManager.GetActiveScene();

			Scene stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
			SceneManager.SetActiveScene(stage); // 이후 생성 오브젝트가 이 씬에 들어가도록

			GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
			ground.name = "Sandbox Ground";
			ground.transform.localScale = new Vector3(GROUND_SCALE, 1f, GROUND_SCALE);

			GameObject lightGo = new("Sandbox Light");
			Light light = lightGo.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.1f;
			lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

			GameObject root;
			try
			{
				root = demo.Build();
			}
			catch (Exception exception)
			{
				Debug.LogError($"[Sandbox] '{demo.Title}' Build 실패: {exception}");
				Close();
				return null;
			}

			if (root == null)
			{
				Debug.LogError($"[Sandbox] '{demo.Title}' Build 가 null 반환");
				Close();
				return null;
			}

			activeStage = stage;
			activeRoot = root;

			FrameSceneView(root);

			if (demo is ISandboxAnimatedDemo animated)
			{
				activeAnimated = animated;
				nextTickTime = EditorApplication.timeSinceStartup + Mathf.Max(MIN_TICK_INTERVAL, animated.TickInterval);
				EditorApplication.update += OnEditorTick;
			}

			Debug.Log($"[Sandbox] 무대 열림: '{demo.Title}' ({(activeAnimated == null ? "정적" : $"라이브 {activeAnimated.TickInterval}s")}). " +
				"Scene View 확인. 닫기 = WM Sandbox 창 또는 WMSandbox.Close().");
			return root;
		}

		// 무대 정리 — 틱 해제 + additive 씬 언로드 + 이전 active 씬 복원. 열린(World 등) 씬 무접촉.
		public static void Close()
		{
			EditorApplication.update -= OnEditorTick;
			activeAnimated = null;
			activeRoot = null;

			if (activeStage.IsValid() && activeStage.isLoaded)
			{
				EditorSceneManager.CloseScene(activeStage, true);
			}
			activeStage = default;

			if (previousActive.IsValid() && previousActive.isLoaded)
			{
				SceneManager.SetActiveScene(previousActive);
			}
			previousActive = default;
		}

		private static void OnEditorTick()
		{
			if (activeRoot == null || activeAnimated == null)
			{
				Close(); // 무대 사라짐(수동 삭제/도메인 리로드) → 정리
				return;
			}

			double now = EditorApplication.timeSinceStartup;
			if (now < nextTickTime)
			{
				return;
			}

			nextTickTime = now + Mathf.Max(MIN_TICK_INTERVAL, activeAnimated.TickInterval);

			try
			{
				activeAnimated.Tick();
			}
			catch (Exception exception)
			{
				Debug.LogError($"[Sandbox] Tick 실패: {exception}");
				Close();
				return;
			}

			SceneView.RepaintAll();
		}

		private static void FrameSceneView(GameObject target)
		{
			Selection.activeGameObject = target;

			SceneView view = SceneView.lastActiveSceneView;
			if (view == null)
			{
				return;
			}

			view.Frame(ComputeBounds(target), true);
			view.Repaint();
		}

		// 대상 렌더러 bounds. 아직 가시 오브젝트가 없으면(에디트 모드 빌드 전 등) 원점 근처 기본 볼륨.
		private static Bounds ComputeBounds(GameObject root)
		{
			Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
			if (renderers.Length == 0)
			{
				return new Bounds(root.transform.position + new Vector3(2f, 0.5f, 0f), new Vector3(8f, 3f, 4f));
			}

			Bounds bounds = renderers[0].bounds;
			for (int index = 1; index < renderers.Length; index++)
			{
				bounds.Encapsulate(renderers[index].bounds);
			}

			bounds.Expand(1.5f);
			return bounds;
		}
	}
}
