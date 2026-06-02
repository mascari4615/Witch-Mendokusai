using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 v1 라이브 매치 dev 런처 — World 를 Play 로 부팅한 뒤 메뉴 실행하면 부팅된 컨텍스트
	/// (ObjectPoolManager/TimeManager) 안에 아레나(데이터=ArenaMapSO)를 z=1000 오프셋으로 생성·스폰·관전.
	/// Arena.unity 씬 불요(맵은 데이터 빌드). 콘텐츠 PlayMode 검증/튜닝용 수동 트리거.
	///
	/// 두 진입점: (v1)=전술 에디터 UI 거쳐 시작 / (Headless)=UI 게이트 우회 즉시 시작.
	/// Headless = behavior-verify 자동화용 — 사용자 클릭 1회면 매치가 UI 없이 돌고 `[Arena-Verify]`
	/// 로그가 Editor.log 에 남아 MCP wedge 중에도 패트롤/전진 판별 가능(ArenaMatch 계측).
	/// </summary>
	public static class ArenaTestLauncher
	{
		private const string CONFIG_PATH = "Assets/_WitchMendokusai/Domain/Arena/Match/ArenaMatchConfig.asset";
		private const float ARENA_OFFSET_Z = 1000f;

		[MenuItem("WM/Arena/Begin Test Match (v1)")]
		public static void BeginTestMatch()
		{
			if (TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config) == false)
				return;

			// 프리-매치 전술 에디터 — 로스터 전술을 행 리스트로 편집 후 [매치 시작]. UIRoot 없으면 바로 시작.
			if (UIRoot.TryGetExistingInstance(out UIRoot uiRoot) && uiRoot.ScreenLayer != null)
			{
				List<TacticEditorView.Entry> entries = new List<TacticEditorView.Entry>();
				foreach (ArenaMatchConfig.ArenaUnitEntry rosterEntry in config.Roster)
				{
					if (rosterEntry.UnitData == null || rosterEntry.Tactic == null)
						continue;
					entries.Add(new TacticEditorView.Entry
					{
						Label = rosterEntry.UnitData.Name + " (팀" + rosterEntry.TeamId + ")",
						Authoring = new RowListAuthoring(rosterEntry.Tactic),
					});
				}
				new TacticEditorView(uiRoot.ScreenLayer, entries, () => match.Begin(config, root));
				Debug.Log("ArenaTestLauncher: 전술 에디터 열림 — 행 편집 후 [매치 시작] 클릭. z=" + ARENA_OFFSET_Z + " 관전.");
			}
			else
			{
				Debug.LogWarning("ArenaTestLauncher: UIRoot 없음 — 에디터 생략, 바로 매치 시작.");
				match.Begin(config, root);
			}
		}

		[MenuItem("WM/Arena/Begin Test Match (Headless, no UI)")]
		public static void BeginTestMatchHeadless()
		{
			if (TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config) == false)
				return;

			// 헤드리스: UIRoot 전술 에디터 게이트 전부 우회 — 즉시 시작(behavior-verify 자동화).
			match.Begin(config, root);
			Debug.Log("[Arena-Verify] HEADLESS — UI 게이트 우회, 즉시 매치 시작. z=" + ARENA_OFFSET_Z);
		}

		/// <summary> 공통 셋업 — config 로드 + 아레나 루트(z 오프셋) + 관전 카메라 + ArenaMatch 컴포넌트 생성. 실패 시 false. </summary>
		private static bool TryPrepareMatch(out ArenaMatch match, out Transform root, out ArenaMatchConfig config)
		{
			match = null;
			root = null;
			config = null;

			if (Application.isPlaying == false)
			{
				Debug.LogWarning("ArenaTestLauncher: 먼저 World 를 Play 로 부팅한 뒤 실행하세요 (ObjectPoolManager/TimeManager 필요).");
				return false;
			}

			config = AssetDatabase.LoadAssetAtPath<ArenaMatchConfig>(CONFIG_PATH);
			if (config == null)
			{
				Debug.LogError("ArenaTestLauncher: ArenaMatchConfig 없음 — " + CONFIG_PATH);
				return false;
			}

			GameObject rootGameObject = new GameObject("ArenaMatchRoot");
			rootGameObject.transform.position = new Vector3(0f, 0f, ARENA_OFFSET_Z);
			root = rootGameObject.transform;

			GameObject cameraGameObject = new GameObject("ArenaSpectatorCamera");
			Camera spectatorCamera = cameraGameObject.AddComponent<Camera>();
			cameraGameObject.transform.position = new Vector3(0f, 26f, ARENA_OFFSET_Z - 30f);
			cameraGameObject.transform.rotation = Quaternion.Euler(41f, 0f, 0f);
			spectatorCamera.fieldOfView = 60f;
			spectatorCamera.depth = 100f;

			GameObject matchGameObject = new GameObject(nameof(ArenaMatch));
			match = matchGameObject.AddComponent<ArenaMatch>();
			match.MatchEnded += winnerTeamId => Debug.Log("[ArenaTestLauncher] 매치 종료 — 승리 팀 = " + winnerTeamId + " (-1 = 무승부)");

			return true;
		}
	}
}
