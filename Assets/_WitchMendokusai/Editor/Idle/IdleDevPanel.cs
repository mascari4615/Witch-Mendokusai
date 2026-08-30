using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

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
			DrawSaveData();
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

			if (GUILayout.Button("Playground 창"))
			{
				IdlePlaygroundWindow.Open();
			}

			if (EditorApplication.isPlaying && GUILayout.Button("플레이 멈춤"))
			{
				EditorApplication.isPlaying = false;
			}

			if (EditorApplication.isPlaying == false)
			{
				IdleBattleScreen.PreviewRunning = EditorGUILayout.ToggleLeft(
					"미리보기 시뮬 진행 (끄면 첫 틱 뒤 정지. UI 와 정적 3D 확인용)", IdleBattleScreen.PreviewRunning);
			}

			EditorGUILayout.Space(8f);
		}

		private bool showRaw;
		private double loadedAt;
		private IdleSaveData? loaded;
		private string loadedRaw = string.Empty;
		private string loadedInfo = string.Empty;

		/// <summary>저장 파일을 읽어 둔다. 2초마다 다시 (매 프레임 디스크 읽기 X)</summary>
		private void RefreshSave()
		{
			if (EditorApplication.timeSinceStartup - loadedAt < 2d)
			{
				return;
			}

			loadedAt = EditorApplication.timeSinceStartup;
			string path = System.IO.Path.Combine(Application.persistentDataPath, "idle.json");

			if (System.IO.File.Exists(path) == false)
			{
				loaded = null;
				loadedRaw = string.Empty;
				loadedInfo = "저장 없음 (처음 켠 사람)";
				return;
			}

			System.IO.FileInfo file = new System.IO.FileInfo(path);
			loadedInfo = string.Format("{0:yyyy-MM-dd HH:mm:ss}  {1:N0} B", file.LastWriteTime, file.Length);
			loadedRaw = System.IO.File.ReadAllText(path);
			loaded = IdleSaveStore.Load();
		}

		private void DrawSaveData()
		{
			RefreshSave();

			EditorGUILayout.LabelField("저장된 데이터", EditorStyles.boldLabel);
			EditorGUILayout.LabelField(loadedInfo, EditorStyles.miniLabel);

			if (loaded.HasValue == false)
			{
				EditorGUILayout.Space(8f);
				return;
			}

			IdleSaveData data = loaded.Value;
			int heroes = data.Heroes != null ? data.Heroes.Length : 0;
			int bag = data.BagItems != null ? data.BagItems.Length : 0;
			string party = data.Party != null ? string.Join(",", data.Party) : "-";
			System.DateTimeOffset seen = System.DateTimeOffset.FromUnixTimeSeconds(data.LastSeenUnixSeconds).ToLocalTime();

			EditorGUILayout.LabelField("구역", string.Format("{0} (최고 {1}, 클리어 {2}){3}", data.Stage, data.BestStage, data.ClearedStage, data.Repeating ? " 반복 중" : string.Empty));
			EditorGUILayout.LabelField("골드", string.Format("{0:N0}", data.Resource));
			EditorGUILayout.LabelField("처치", string.Format("{0:N0}", data.Kills));
			EditorGUILayout.LabelField("강화", string.Format("공격 Lv{0} / 속도 Lv{1}", data.DamageLevel, data.AttackSpeedLevel));
			EditorGUILayout.LabelField("환생", string.Format("{0}회, 조각 {1}", data.Ascensions, data.PrestigePoints));
			EditorGUILayout.LabelField("뽑기", string.Format("재화 {0}, 누적 {1}회, 천장까지 {2}", data.Stones, data.PullsDone, data.PullsSincePity));
			EditorGUILayout.LabelField("인형", string.Format("{0}종, 편성 [{1}]", heroes, party));
			EditorGUILayout.LabelField("가방", string.Format("{0}개", bag));
			EditorGUILayout.LabelField("마지막 접속", seen.ToString("yyyy-MM-dd HH:mm:ss"));

			showRaw = EditorGUILayout.Foldout(showRaw, "원문 JSON");
			if (showRaw)
			{
				EditorGUILayout.TextArea(loadedRaw, GUILayout.MinHeight(120f));
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
