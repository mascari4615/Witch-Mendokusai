using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	// TowerDefensePlayVerify 의 이어하기와 씨앗 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		/// <summary>
		/// HUD 겹침 — 「미니맵이 선택 패널을 가리나」를 *사람 눈*이 아니라 좌표로 묻는다.
		///
		/// ★ 왜 스크린샷이 아니라 좌표인가: 그림은 사람이 봐야 하고, 봐야 하는 검사는 매번 미뤄진다
		///   (이 작업에서 「UI 겹침 확인」이 계속 뒤로 밀린 이유). 사각형이 겹치는지는 기계가 판정할 수 있다.
		/// ★ 화면 밖으로 나간 것도 잡는다 — 겹치지만 않으면 되는 게 아니라 *보여야* 한다.
		/// </summary>
		// 나가기 직전의 판 — 들어와서 이것과 같아야 「이어했다」다.
		// 시계가 이만큼은 쌓인 뒤에 나간다 — 0 에서 나가면 「되감김」과 「그대로」가 구분되지 않는다.
		private const int RESUME_MIN_CLOCK = 5;
		private static int resumeSeed;
		private static int resumeResource;
		private static int resumeEssence;
		private static int resumeBuildings;
		private static int resumeSurvived;
		private static int resumeLives;
		private static int resumeTraps;
		private static int resumeWalls;

		private static void CaptureResumeSnapshot()
		{
			if (match == null)
				return;

			resumeSeed = match.MapSeed;
			resumeResource = match.Resource;
			resumeEssence = match.Essence;
			resumeBuildings = match.DollLabels.Count;
			resumeSurvived = match.SurvivedSeconds;
			resumeLives = match.Lives;
			resumeTraps = match.TrapCount;
			resumeWalls = match.WallCellCount;
			Debug.Log(TAG + " RESUME-SNAPSHOT seed=" + resumeSeed + " 자원=" + resumeResource
				+ " 정수=" + resumeEssence + " 건물=" + resumeBuildings
				+ " 버틴시간=" + resumeSurvived + " 목숨=" + resumeLives
				+ " 함정=" + resumeTraps + " 벽=" + resumeWalls);
		}

		/// <summary>
		/// 이어하기 — 나갔다 들어온 판이 나가기 전과 같은가.
		/// ★ 땅(씨앗)이 먼저다 — 건물 수만 맞고 땅이 다르면 내 건물이 엉뚱한 데 서 있는 것이다.
		/// ★ 지갑도 본다 — 되살리며 값을 또 치르면 이어할 때마다 지갑이 깎인다(실제 결함이었다).
		/// </summary>
		private static void VerifyResume()
		{
			TowerDefenseMatch resumed = Object.FindAnyObjectByType<TowerDefenseMatch>();
			// ★ 복원이 아직 도는 중이면 재지 않는다 — 중간값을 읽으면 멀쩡한 복원을 결함으로 잡는다.
			if (resumed != null && resumed.RestoreInProgress)
			{
				Debug.Log(TAG + " RESUME 대기 — 복원이 아직 도는 중이다(다음 틱에 다시 본다).");
				return;
			}
			if (resumed == null)
			{
				Debug.LogError(TAG + " RESUME-FAIL 재진입 후 매치가 없음");
				return;
			}

			string verdict = TAG + " RESUME seed " + resumeSeed + "→" + resumed.MapSeed
				+ " · 자원 " + resumeResource + "→" + resumed.Resource
				+ " · 정수 " + resumeEssence + "→" + resumed.Essence
				+ " · 건물 " + resumeBuildings + "→" + resumed.DollLabels.Count
				+ " · 버틴시간 " + resumeSurvived + "→" + resumed.SurvivedSeconds
				+ " · 목숨 " + resumeLives + "→" + resumed.Lives
				+ " · 함정 " + resumeTraps + "→" + resumed.TrapCount
				+ " · 벽 " + resumeWalls + "→" + resumed.WallCellCount;

			// ★ 시계가 안 돌아오면 오래 버틴 판이 이어하는 순간 처음으로 되감긴다(마수가 갑자기 약해진다).
			//   딱 맞을 필요는 없다 — 재진입에 걸린 몇 초는 흘러도 되지만, 0 으로 되감기면 안 된다.
			bool sameClock = resumed.SurvivedSeconds >= resumeSurvived;
			bool sameLives = resumed.Lives == resumeLives;
			bool sameGround = resumed.MapSeed == resumeSeed;
			bool sameWallet = resumed.Resource == resumeResource && resumed.Essence == resumeEssence;
			// ★ 「그 이상」이면 통과시키면 안 된다 — 유령이 한 채씩 느는 결함이 정확히 그렇게 숨어 있었다.
			bool sameBuildings = resumed.DollLabels.Count == resumeBuildings;
			// 함정·벽도 「그대로」의 일부다 — 인형만 세면 깔아둔 것이 사라져도 초록이 뜬다.
			bool sameField = resumed.TrapCount == resumeTraps && resumed.WallCellCount == resumeWalls;

			if (sameGround && sameWallet && sameBuildings && sameClock && sameLives && sameField)
				Debug.Log(verdict + " → 나갔다 들어와도 그 판 그대로 ✔");
			else
				Debug.LogError(verdict + " → 이어하기가 판을 그대로 못 돌려준다"
					+ (sameGround ? "" : " [땅이 다름]")
					+ (sameWallet ? "" : " [지갑이 다름]")
					+ (sameBuildings ? "" : " [건물 수가 다름]")
					+ (sameField ? "" : " [함정·벽이 다름]")
					+ (sameClock ? "" : " [시계가 되감김]")
					+ (sameLives ? "" : " [목숨이 다름]"));
		}

		/// <summary>
		/// 툴팁을 실제로 띄운다 — 마우스가 없는 하네스가 이걸 못 하면 툴팁 배치는 영영 미측정으로 남는다.
		/// ★ 손이 가장 자주 가는 자리(화면 오른쪽 아래 = 핫바 위)에서 띄운다. 가운데서만 재면
		///   「가장자리에서 화면 밖으로 새는가」라는 진짜 질문을 못 묻는다.
		/// </summary>
		private static void ShowTooltipForLayout()
		{
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			if (controller.Hud == null)
				return;

			controller.Hud.ShowUnitTooltip("확인용 설명 · 두 줄짜리",
				new WitchMendokusai.Numerics.Vector2(Screen.width * 0.9f, Screen.height * 0.15f));
		}

		// 남이 건넨 씨앗처럼 쓸 숫자 — 이 값으로 연 판이 정말 그 땅인지 본다.
		private const int SHARED_SEED = 20260803;
		private static bool seedShareArmed;

		/// <summary> 다음 판에 「남이 준 씨앗」을 걸어둔다. </summary>
		private static void ArmSeedShareCheck()
		{
			if (match == null)
				return;

			match.SetNextMatchSeed(SHARED_SEED);
			seedShareArmed = true;
		}

		/// <summary>
		/// 씨앗 공유 — 건넨 숫자로 연 판이 정말 그 땅인가.
		/// ★ 저장(이어하기)이 씨앗을 덮어쓸 수 있다 — 그러면 「공유」가 조용히 「이어하기」가 된다.
		///   그 경우는 실패가 아니라 *확인 못 함*이다(둘 다 씨앗을 정하는 정당한 주인이다).
		/// </summary>
		private static void VerifySeedShare()
		{
			if (seedShareArmed == false || match == null)
				return;

			seedShareArmed = false;
			string verdict = TAG + " SEED-SHARE 건넨씨앗=" + SHARED_SEED + " 열린판=" + match.MapSeed;

			if (match.MapSeed == SHARED_SEED)
				Debug.Log(verdict + " → 건넨 씨앗으로 같은 땅이 열린다 ✔");
			else
				Debug.Log(verdict + " → 이어하기가 씨앗을 정했다(저장이 우선) — 공유는 확인 못 함");
		}
	}
}
