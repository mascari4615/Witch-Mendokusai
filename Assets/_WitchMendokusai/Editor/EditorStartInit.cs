#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// https://mentum.tistory.com/657
	//
	// Play 를 누르면 <어떤 씬을 열어 뒀든> 첫 씬부터 시작하게 한다 — 본편은 Intro→Lobby 흐름이라
	// 중간 씬에서 눌러도 제대로 도는 게 편하다.
	//
	// ★ 단, <b>본편이 아닌 씬</b>은 가로채지 않는다 (TASK-WM-406).
	//   방치형(`Idle`)은 따로 파는 게임이라 Intro 도 로비도 안 쓴다.
	//   이 가로채기 때문에 방치형 씬을 열고 Play 를 눌러도 계속 본편이 떴다 —
	//   활성 씬을 찍어 보고서야 알았다(`Bootstrap` 시점에 이미 'Intro' 였다).
	//   씬을 열 때마다 다시 판단한다 — 에디터 켤 때 한 번만 정하면 그 뒤 바꾼 씬을 못 따라간다.
	//
	// ★ Play 누르는 순간에도 다시 고정 (2026-11-23)
	//   - 원인: 에디터 시작 직후 Apply 의 씬 읽기 null 가능 (MOIS3 실측, 'Failed to load VirtualArtifacts/...')
	//   - 증상: 시작 씬 빈 채 World 에서 바로 시작, 데이터 로드 (Intro, Lobby 경로) 건너뜀, KeyNotFound 와 NRE 수천 건
	//   - 근거: ExitingEditMode 에서 고정한 시작 씬은 그 Play 에 바로 적용 (같은 날 에디터 실험)
	[InitializeOnLoad]
	public class EditorStartInit
	{
		/// <summary>본편이 아닌 씬 — 여기서는 Play 를 가로채지 않는다.</summary>
		private const string SIDE_GAME_SCENE = "Idle";

		static EditorStartInit()
		{
			Apply();
			EditorSceneManager.sceneOpened += (scene, mode) => Apply();
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
		{
			if (playModeStateChange != PlayModeStateChange.ExitingEditMode)
			{
				return;
			}

			Apply();

			// Play 직전에도 첫 씬 읽기 실패면 Play 중단. 열린 씬 시작은 데이터 없는 실행, 에러 수천 건에 원인 묻힘
			if (WantsFirstScene() && EditorSceneManager.playModeStartScene == null)
			{
				UnityEngine.Debug.LogError($"[EditorStartInit] 첫 씬을 못 읽어 Play 를 멈췄다: {EditorBuildSettings.scenes[0].path}");
				EditorApplication.isPlaying = false;
			}
		}

		/// <summary>이 Play 를 첫 빌드 씬에서 시작해야 하는가. 방치형 씬이거나 빌드 씬이 없으면 아니다</summary>
		private static bool WantsFirstScene() =>
			SceneManager.GetActiveScene().name != SIDE_GAME_SCENE && EditorBuildSettings.scenes.Length > 0;

		private static void Apply()
		{
			if (SceneManager.GetActiveScene().name == SIDE_GAME_SCENE)
			{
				// 열어 둔 씬에서 그대로 시작한다.
				EditorSceneManager.playModeStartScene = null;
				return;
			}

			if (EditorBuildSettings.scenes.Length == 0)
			{
				return;
			}

			string pathOfFirstScene = EditorBuildSettings.scenes[0].path;
			SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(pathOfFirstScene);
			EditorSceneManager.playModeStartScene = sceneAsset;
		}
	}
}
#endif
