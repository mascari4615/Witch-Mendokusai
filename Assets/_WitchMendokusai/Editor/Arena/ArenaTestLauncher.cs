using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 아레나 v1 라이브 매치 dev 런처 — World 를 Play 로 부팅한 뒤 메뉴 실행하면 부팅된 컨텍스트
	/// (ObjectPoolManager/TimeManager) 안에 아레나(데이터=ArenaMapSO)를 z=1000 오프셋으로 생성·스폰·관전.
	/// Arena.unity 씬 불요(맵은 데이터 빌드). 콘텐츠 PlayMode 검증/튜닝용 수동 트리거.
	/// </summary>
	public static class ArenaTestLauncher
	{
		private const string CONFIG_PATH = "Assets/_WitchMendokusai/Domain/Arena/Match/ArenaMatchConfig.asset";
		private const float ARENA_OFFSET_Z = 1000f;

		[MenuItem("WM/Arena/Begin Test Match (v1)")]
		public static void BeginTestMatch()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning("ArenaTestLauncher: 먼저 World 를 Play 로 부팅한 뒤 실행하세요 (ObjectPoolManager/TimeManager 필요).");
				return;
			}

			ArenaMatchConfig config = AssetDatabase.LoadAssetAtPath<ArenaMatchConfig>(CONFIG_PATH);
			if (config == null)
			{
				Debug.LogError("ArenaTestLauncher: ArenaMatchConfig 없음 — " + CONFIG_PATH);
				return;
			}

			GameObject rootGameObject = new GameObject("ArenaMatchRoot");
			rootGameObject.transform.position = new Vector3(0f, 0f, ARENA_OFFSET_Z);

			GameObject cameraGameObject = new GameObject("ArenaSpectatorCamera");
			Camera spectatorCamera = cameraGameObject.AddComponent<Camera>();
			cameraGameObject.transform.position = new Vector3(0f, 26f, ARENA_OFFSET_Z - 30f);
			cameraGameObject.transform.rotation = Quaternion.Euler(41f, 0f, 0f);
			spectatorCamera.fieldOfView = 60f;
			spectatorCamera.depth = 100f;

			GameObject matchGameObject = new GameObject(nameof(ArenaMatch));
			ArenaMatch match = matchGameObject.AddComponent<ArenaMatch>();
			match.MatchEnded += winnerTeamId => Debug.Log("[ArenaTestLauncher] 매치 종료 — 승리 팀 = " + winnerTeamId + " (-1 = 무승부)");
			match.Begin(config, rootGameObject.transform);

			Debug.Log("ArenaTestLauncher: 매치 시작 — z=" + ARENA_OFFSET_Z + " 관전(ArenaSpectatorCamera). 슬라임 6기 스폰→접근→전투→전멸 승패.");
		}
	}
}
