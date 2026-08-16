using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 런처 (TASK-WM-411) — 빈 씬을 열고 감독 하나만 얹은 뒤 Play 로 들어간다.
	/// 본편(World) 부팅을 타지 않으므로 heavy-boot 대기·MCP 브릿지 wedge 와 무관하고, 켜는 데 몇 초면 된다.
	/// 판·카메라·조명·싸우는 둘은 전부 <see cref="VersusMatchDirector"/> 가 런타임에 짓는다(씬 에셋 0).
	/// </summary>
	public static class VersusTestLauncher
	{
		private const string SCENE_NAME = "VersusPrototype";

		[MenuItem("WM/Versus/한 판 열기 (v0)")]
		public static void OpenAndPlay()
		{
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
				return;

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			scene.name = SCENE_NAME;

			GameObject director = new GameObject("VersusMatch");
			director.AddComponent<VersusMatchDirector>();
			Selection.activeGameObject = director;

			EditorApplication.isPlaying = true;
		}

		/// <summary> 지금 열려 있는 씬에 감독만 얹는다 — 다른 씬에서 대결 판만 띄워 보고 싶을 때. </summary>
		[MenuItem("WM/Versus/이 씬에 감독 얹기")]
		public static void AddDirectorToCurrentScene()
		{
			GameObject director = new GameObject("VersusMatch");
			director.AddComponent<VersusMatchDirector>();
			Undo.RegisterCreatedObjectUndo(director, "Add VersusMatchDirector");
			Selection.activeGameObject = director;
		}
	}
}
