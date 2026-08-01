using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 특수시공 개척(TD) dev 진입 메뉴 (TASK-WM-194 증분3) — ArenaTestLauncher 와 달리 컨텐츠를 코드로
	/// 조립하지 않는다(TowerDefenseModeController 프리팹이 다음 증분에서 씬/DI 에 배선됨). 본 메뉴는
	/// GameModeManager.SetMode 만 호출 — 실제 진입/이탈 로직은 컨트롤러가 OnModeChanged 로 수신.
	/// </summary>
	public static class TowerDefenseDevLauncher
	{
		[MenuItem("WM/TowerDefense/Enter Mode")]
		public static void EnterMode()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning($"{nameof(TowerDefenseDevLauncher)}: 먼저 World 를 Play 로 부팅한 뒤 실행하세요.");
				return;
			}
			if (GameModeManager.Instance == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseDevLauncher)}: GameModeManager.Instance 없음 — World 부팅 완료 후 재시도.");
				return;
			}

			GameModeManager.Instance.SetMode(GameMode.TowerDefense);
		}

		[MenuItem("WM/TowerDefense/Exit Mode")]
		public static void ExitMode()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning($"{nameof(TowerDefenseDevLauncher)}: 먼저 World 를 Play 로 부팅한 뒤 실행하세요.");
				return;
			}
			if (GameModeManager.Instance == null)
			{
				Debug.LogWarning($"{nameof(TowerDefenseDevLauncher)}: GameModeManager.Instance 없음 — World 부팅 완료 후 재시도.");
				return;
			}

			GameModeManager.Instance.SetMode(GameMode.Default);
		}
	}
}
