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
	[InitializeOnLoad]
	public class EditorStartInit
	{
		/// <summary>본편이 아닌 씬 — 여기서는 Play 를 가로채지 않는다.</summary>
		private const string SIDE_GAME_SCENE = "Idle";

		static EditorStartInit()
		{
			Apply();
			EditorSceneManager.sceneOpened += (scene, mode) => Apply();
		}

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
