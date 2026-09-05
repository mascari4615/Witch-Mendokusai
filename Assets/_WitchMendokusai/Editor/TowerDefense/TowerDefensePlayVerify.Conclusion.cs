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
	// TowerDefensePlayVerify 의 판 끝 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		/// <summary> 결말 화면 검증 — 배너가 실제로 떠야 플레이어가 끝났다는 걸 안다. </summary>
		private static void VerifyConclusion(double now)
		{
			if (now - restartAt < 1.0)
				return;

			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			VisualElement banner = hud != null ? hud.Q("BannerWrapper") : null;

			bool bannerVisible = banner != null && banner.resolvedStyle.display == DisplayStyle.Flex;
			string bannerText = banner != null ? (banner.Q<Label>() != null ? banner.Q<Label>().text : "no-label") : "no-banner";

			if (bannerVisible)
				Debug.Log(TAG + " CONCLUSION-BANNER visible=True text=\"" + bannerText + "\"");
			else
				Debug.LogError(TAG + " CONCLUSION-BANNER 결과 배너가 안 뜸 — 끝났는데 화면이 아무 말도 안 한다. banner=" + (banner != null));

			// 결말 상태에서 「다시 도전」이 실제로 새 판을 여는가 (막다른 화면이 되지 않는가).
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
			{
				Debug.LogError(TAG + " CONCLUSION-FAIL controller 없음");
				Finish();
				return;
			}

			Debug.Log(TAG + " CONCLUSION-RESTART 결말 상태에서 재시작 요청");
			controller.Restart();
			restartAt = now;
			step = Step.RestartFromConclusion;
		}

		/// <summary> 결말 → 재시작이 진짜 새 판인지 (자원/웨이브/국면이 처음으로 돌아왔는지). </summary>
		private static void VerifyRestartFromConclusion(double now)
		{
			if (now - restartAt < 3.0)
				return;

			if (match == null)
			{
				Debug.LogError(TAG + " CONCLUSION-RESTART-FAIL 매치 없음");
				Finish();
				return;
			}

			bool freshWave = match.WaveIndex == 0;
			bool freshOutcome = match.Outcome == TowerDefenseOutcome.InProgress;
			bool freshResource = match.Resource > 0;

			string verdict = TAG + " CONCLUSION-RESTART-RESULT wave=" + match.WaveIndex
				+ " outcome=" + match.Outcome
				+ " resource=" + match.Resource
				+ " phase=" + match.Phase;

					// ★ 다시 시작한 판은 신호가 **0 부터** 차야 한다. 안 비우면 두 번째 판이 이미 가득 찬 채로
					//   시작해 「점점 채워진다」가 통째로 사라진다(사용자가 콕 집어 요구한 것).
					TowerDefenseMatch fresh = Object.FindAnyObjectByType<TowerDefenseMatch>();
					if (fresh != null)
					{
						Debug.Log($"{TAG} 재시작 신호 — 코어 충전 {fresh.CoreSignalCharge:F2} (0 에 가까워야 한다)");
						if (fresh.CoreSignalCharge > 0.9f)
							Debug.LogError(TAG + " 재시작 FAIL — 새 판이 이미 가득 찬 신호로 시작한다(옛 판 상태가 남았다).");

						// ★ 새 판이 시작하자마자 「내 것이 부서졌다」가 뜨면, 판이 끝나며 청산된 것을
						//   적이 부순 것으로 오인한 것이다 — 첫인상이 거짓 경고면 알림 전체를 못 믿게 된다.
						int falseBreak = 0;
						foreach (TowerDefenseAlerts.Alert alert in fresh.Alerts)
						{
							if (alert.Label.Contains("부서졌다"))
								falseBreak++;
						}
						Debug.Log($"{TAG} 재시작 알림 — 「부서졌다」 {falseBreak}개 · 전체 {fresh.Alerts.Count}개 (둘 다 0 이어야 한다)");
						if (falseBreak > 0)
							Debug.LogError(TAG + " 재시작 FAIL — 새 판이 시작하자마자 옛 판 건물을 「부서졌다」고 알린다.");

						// ★ 그림도 판마다 하나여야 한다. 옛 판 것이 안 치워지면 신호장이 두 벌 겹쳐 그려지고,
						//   판을 거듭할수록 는다(눈에는 「좀 진해졌네」로만 보여서 늦게 발견된다).
						int fields = 0;
						foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
						{
							if (candidate.name == "SignalField")
								fields++;
						}
						Debug.Log($"{TAG} 재시작 그림 — 신호장 {fields}벌 (1 이어야 한다)");
						if (fields > 1)
							Debug.LogError($"{TAG} 재시작 FAIL — 신호장이 {fields}벌 겹쳐 있다(옛 판 것이 안 치워졌다).");
					}

			if (freshWave && freshOutcome && freshResource)
				Debug.Log(verdict + " → 새 판 성립 ✔");
			else
				Debug.LogError(verdict + " → 결말 뒤 재시작이 새 판이 아니다(막다른 상태).");

			Finish();
		}

		private static void OnMatchEnded(TowerDefenseOutcome outcome)
		{
			matchEndedSeen = true;
			Debug.Log(TAG + " MATCH-ENDED outcome=" + outcome);
		}

		private static void Finish()
		{
			// ★ 끝났다는 말을 안 하면 로그만 보고는 「끝났나 멈췄나」를 못 가린다 — 실제로 전체 실행이
			//   정상 종료했는데 10분을 「행」으로 의심하며 기다렸다. 검사는 자기 상태를 말해야 한다.
			double elapsed = EditorApplication.timeSinceStartup - playStart;

			// ★ 견줄 수 있는 한 줄. 경고 *줄 수*는 같은 마수가 4초마다 다시 찍혀 부풀고 판 길이에 휘둘린다 —
			//   그걸로 두 실행을 비교하다 두 번 헛짚었다(좋아진 줄 알았던 것이 그냥 다른 판이었다).
			//   굳은 *자리 수*는 「판의 어디가 막히는가」라서 실행끼리 견줄 수 있다.
			if (match != null)
			{
				(int total, int byTerrain, int byUnit) = match.StuckCellSummary;
				Debug.Log($"{TAG} STUCK-SUMMARY 굳은 자리 {total}곳 (지형에 막힘 {byTerrain} · 서로 막음 {byUnit})"
					+ $" · 왕복(굳음 아님) {match.OscillatingCellCount}곳"
					+ $" · 판 씨앗 {match.MapSeed} · 암반 {match.ObstacleCount}칸"
					+ " → 두 실행을 견줄 때는 이 줄만 보면 된다(경고 줄 수는 판 길이에 휘둘린다).");
			}

			Debug.Log($"{TAG} 검증 끝 — 마지막 단계 {step} · {elapsed:F0}초 · 모드 "
				+ (placeOnly ? "배치만" : wavesOnly ? "파도만" : conclusionOnly ? "결말만" : "전체")
				+ " (실패 항목은 위에 별도로 찍힌다. 없으면 없다.)");

			EditorApplication.update -= Tick;
			if (match != null)
				match.MatchEnded -= OnMatchEnded;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
