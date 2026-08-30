using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// Idle 개발용 패널. 메뉴를 뒤지는 대신 창 하나에서 개발 기능을 누른다 (사용자 2026-08-30, 시험 도입).
	///
	/// ★ 메뉴 경로는 영문만 (rules/unity.md). 창 안 글자는 한국어 가능
	/// ★ 기능은 여기서 새로 만들지 않는다. 기존 정적 진입점을 부르기만 (한 기능 한 정본)
	/// </summary>
	public sealed class IdleDevPanel : EditorWindow
	{
		private const string TITLE = "Idle Dev";
		private Vector2 scroll;

		[MenuItem("WM/Idle/Dev Panel")]
		public static void Open()
		{
			IdleDevPanel window = GetWindow<IdleDevPanel>(TITLE);
			window.minSize = new Vector2(320f, 420f);
			window.Show();
		}

		private void OnGUI()
		{
			scroll = EditorGUILayout.BeginScrollView(scroll);

			DrawStatus();
			DrawScene();
			DrawSave();
			DrawBuild();

			EditorGUILayout.EndScrollView();
		}

		private static void DrawStatus()
		{
			EditorGUILayout.LabelField("상태", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("씬", EditorSceneManager.GetActiveScene().name);
			EditorGUILayout.LabelField("플레이", EditorApplication.isPlaying ? "돌고 있음" : "정지");
			EditorGUILayout.LabelField("시작 씬", EditorSceneManager.playModeStartScene != null
				? EditorSceneManager.playModeStartScene.name
				: "(열어 둔 씬 그대로)");
			EditorGUILayout.Space(8f);
		}

		private static void DrawScene()
		{
			EditorGUILayout.LabelField("씬", EditorStyles.boldLabel);

			if (GUILayout.Button("V2 열고 플레이 (Ctrl+Shift+U)", GUILayout.Height(32f)))
			{
				IdleV2SceneBuilder.OpenAndPlay();
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("V2 씬 짓기"))
			{
				IdleV2SceneBuilder.Build();
			}

			if (GUILayout.Button("V2 씬 검사"))
			{
				bool fine = IdleV2SceneBuilder.Verify();
				Debug.Log("[IdleDev] V2 씬 검사: " + (fine ? "초록" : "빨강"));
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("V1 열고 플레이"))
			{
				IdleSceneBuilder.OpenAndPlay();
			}

			if (GUILayout.Button("Playground 창"))
			{
				IdlePlaygroundWindow.Open();
			}
			EditorGUILayout.EndHorizontal();

			if (EditorApplication.isPlaying && GUILayout.Button("플레이 멈춤"))
			{
				EditorApplication.isPlaying = false;
			}

			EditorGUILayout.Space(8f);
		}

		private static void DrawSave()
		{
			EditorGUILayout.LabelField("저장", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(Application.persistentDataPath, EditorStyles.miniLabel);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("데이터 초기화 (저장 삭제)"))
			{
				IdlePlayerBuild.WipeSave();
			}

			if (GUILayout.Button("저장 폴더 열기"))
			{
				EditorUtility.RevealInFinder(Application.persistentDataPath);
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.HelpBox("플레이 중이면 화면 좌하 '데이터 초기화' 버튼이 저장 안 하고 다시 켠다.", MessageType.None);
			EditorGUILayout.Space(8f);
		}

		private static void DrawBuild()
		{
			EditorGUILayout.LabelField("빌드", EditorStyles.boldLabel);

			if (GUILayout.Button("플레이어 빌드 (이 게임만)"))
			{
				IdlePlayerBuild.Build();
			}
		}
	}
}
