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
	// TowerDefensePlayVerify 의 서식지와 뚫림 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		/// <summary>
		/// 서식지가 깔렸는가 + 파도가 테두리 토막에서 오는가 — 둘 다 「판에 있어야」 의미가 있다.
		/// </summary>
		private static void VerifyLairsAndInvasion()
		{
			if (match == null)
				return;

			int lairCount = match.SleepingLairCount;
			string nextDirection = match.IsBorderInvasion ? match.NextInvasionDirectionName() : "(꺼짐)";

			Debug.Log($"{TAG} 서식지 {lairCount}곳 · 테두리 침공 {match.IsBorderInvasion} · 다음 파도 {nextDirection}쪽");

			if (lairCount == 0)
				Debug.LogError($"{TAG} 서식지 FAIL — 0곳. 넓히는 것이 위험이 되는 층이 통째로 빠졌다.");
			if (match.IsBorderInvasion && string.IsNullOrEmpty(nextDirection))
				Debug.LogError($"{TAG} 예고 FAIL — 다음 파도 방향을 못 말한다. 예고가 성립하지 않는다.");
		}

		private static double lairDriftCheckAt;
		private static float lairWakeFrom;
		private static int lairWakeGuards;
		private static TowerDefenseMatch lairWakeMatch;
		private static int lairWakeLives;
		private static int lairWakeEnemies;
		private static Vector3 lairWakePosition;

		/// <summary> 깨운 마수가 8초 동안 코어 쪽으로 얼마나 다가갔나 — 「행진」과 「지킴」을 가른다. </summary>
		private static void CheckLairDrift()
		{
			if (match == null)
				return;

			// ★ 깨운 판과 지금 판이 다르면 비교가 성립하지 않는다 — 모드를 나갔다 들어오면 판이 새로 태어나
			//   서식지도 전부 다시 잠든 채로 깔린다. 그걸 모르고 재면 「깨운 마수가 전부 사라졌다」는
			//   *존재하지 않는 결함*을 보고하게 된다(실제로 그렇게 두 사이클을 썼다).
			if (lairWakeMatch == null || match != lairWakeMatch)
			{
				Debug.Log(TAG + " 서식지 이동 — 잴 수 없음(판이 그 사이에 새로 시작됐다). 이번 회차는 건너뛴다.");
				return;
			}

			float now = match.AwakenedGuardDistanceToCore(out int aliveNow, out int destroyedNow, out int disabledNow);
			Debug.Log($"{TAG} 서식지 이동 — 마수 {lairWakeGuards}기 → 살아있음 {aliveNow} · 파괴됨 {destroyedNow} · 꺼짐 {disabledNow}"
				+ $" · 코어까지 {lairWakeFrom:F1} → {now:F1}");

			// ★ 살아남은 것이 없으면 거리는 뜻이 없다 — 「가까워졌다」가 아니라 「죽어서 없다」다.
			if (aliveNow == 0)
			{
				// ★ 「죽었다」와 「유출로 사라졌다」와 「무대 밖으로 치워졌다」는 고치는 자리가 전부 다르다.
				//   목숨이 줄었으면 유출, 안 줄었으면 죽거나 치워진 것 — 그 둘을 여기서 가른다.
				Debug.LogError($"{TAG} 서식지 FAIL — 깨운 마수 {lairWakeGuards}기가 8초 만에 전부 사라졌다"
					+ $" (목숨 {lairWakeLives}→{match.Lives} · 판 위 마수 {lairWakeEnemies}→{match.WaveEnemies.Count}"
					+ $" · 파괴 {destroyedNow} · 꺼짐 {disabledNow})."
					+ " 코어에서 멀어 포탑에 죽은 것이 아니다 — 서식지가 판에 아무 영향을 못 준다.");
				return;
			}

			// ★ 판정은 **집에서 얼마나 멀어졌나**로 한다 — 코어까지의 거리로 재면 그 서식지가 원래
			//   코어에 가까웠는지 멀었는지에 답이 좌우된다(같은 행동이 판마다 통과·실패로 갈린다).
			float fromHome = match.AwakenedGuardDistanceFromHome();
			float leash = TowerDefenseModeControllerLeash();
			Debug.Log($"{TAG} 서식지 목줄 — 집에서 최대 {fromHome:F1} (목줄 {leash:F1})"
				+ $" · 깨운 {match.LairsAwakened}곳 · 쓸어낸 {match.LairsCleared}곳 · 정수 {match.Essence}");

			if (leash > 0f && fromHome > leash * 1.5f)
			{
				Debug.LogError($"{TAG} 서식지 FAIL — 집에서 {fromHome:F1} 까지 벗어났다(목줄 {leash:F1})."
					+ " 「넓히는 것이 위험」이 아니라 파도가 하나 더 있는 것이다.");
			}

			// ★ 보상은 *다 죽어야* 나오는데 하네스는 전투로 그걸 못 만든다 — 조건만 만들어 규칙을 확인한다.
			int essenceBefore = match.Essence;
			int clearedBefore = match.LairsCleared;
			if (match.ClearAwakenedLairForVerification())
				lairClearCheckAt = EditorApplication.timeSinceStartup + 1.0;
			lairClearEssenceBefore = essenceBefore;
			lairClearBefore = clearedBefore;
		}

		private static double breachCheckAt;
		private static bool breachArmed;
		private static float breachAngleBefore;
		private static Vector3 breachLostAt;
		private static TowerDefenseMatch breachMatch;

		/// <summary>
		/// 「뚫린 자리가 다음 파도를 끌어당긴다」를 실제로 재려면 *건물을 잃어야* 한다.
		///
		/// ★ 마수가 부술 때까지 기다리는 검사는 판마다 오거나 안 온다 — 적응 검사에서 그렇게
		///   다섯 사이클을 날렸다. 재는 쪽이 사건을 일으킨다: 가장 먼 건물을 일부러 없앤다.
		/// </summary>
		private static void ArmBreachProbe(double now)
		{
			if (breachArmed || match == null || match.CoreCombatant == null)
				return;
			if (match.SurvivedSeconds < 12f)
				return;

			breachAngleBefore = match.InvasionAngleAt(match.WaveIndex + 1);
			if (match.DestroyFarthestBuildingForVerification(out Vector3 lostAt) == false)
			{
				breachArmed = true;
				Debug.Log(TAG + " 뚫린 자리 — 못 쟀다: 없앨 내 건물이 없다(코어뿐). 실패가 아니다.");
				return;
			}

			breachArmed = true;
			breachLostAt = lostAt;
			breachMatch = match;
			breachCheckAt = now + 2.0; // 손실은 다음 틱에 집계된다 — 같은 프레임에 재면 늘 「안 바뀜」이다.
			Debug.Log(TAG + " 뚫린 자리 — 가장 먼 건물을 없앴다 · 끌리기 전 방향 "
				+ breachAngleBefore.ToString("F1") + "도(" + TowerDefenseWaveOrigin.DirectionName(breachAngleBefore) + ")");
		}

		/// <summary> 잃은 쪽으로 다음 파도가 끌렸는가 — 안 끌리면 이 규칙은 화면에 없는 규칙이다. </summary>
		private static void CheckBreachPull()
		{
			if (match == null || match != breachMatch)
			{
				Debug.Log(TAG + " 뚫린 자리 — 못 쟀다: 재는 중에 판이 새로 시작됐다. 실패가 아니다.");
				return;
			}

			int hot = match.BreachHotCount;
			float after = match.InvasionAngleAt(match.WaveIndex + 1);

			Vector3 core = match.CoreCombatant != null ? match.CoreCombatant.Position : Vector3.zero;
			Vector3 offset = breachLostAt - core;
			float lostAngle = Mathf.Repeat(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg, 360f);

			float movedBefore = Mathf.Abs(Mathf.DeltaAngle(breachAngleBefore, lostAngle));
			float movedAfter = Mathf.Abs(Mathf.DeltaAngle(after, lostAngle));

			Debug.Log($"{TAG} 뚫린 자리 결과 — 뜨거운 자리 {hot}곳 · 잃은 쪽 {lostAngle:F1}도"
				+ $" · 파도 방향 {breachAngleBefore:F1} → {after:F1}"
				+ $" · 잃은 쪽과의 차이 {movedBefore:F1} → {movedAfter:F1}");

			// ★ 방향이 조용히 바뀌기만 하면 사람은 자기 선택과 결과를 못 잇는다 — 말도 떠야 규칙이다.
			//   열기는 10초쯤이면 식으므로 *여기서* 훑는다. 나중에 보면 늘 「없음」이다(한 번 겪었다).
			bool spoken = false;
			UIRoot breachUiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement breachHud = breachUiRoot != null && breachUiRoot.ModeHudLayer != null
				? breachUiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (breachHud != null)
			{
				foreach (Label label in breachHud.Query<Label>().ToList())
				{
					if (label == null || string.IsNullOrEmpty(label.text) || label.text.Contains("노린다") == false)
						continue;
					spoken = true;
					Debug.Log(TAG + " 뚫린 자리 — 화면 글자: 「" + label.text + "」");
					break;
				}
			}

			// ★ 순서가 중요하다. 규칙이 손실을 아예 못 봤으면 화면을 탓할 일이 아니다 —
			//   예전엔 둘 다 FAIL 로 찍혀 「화면 문제」라는 잘못된 실마리를 하나 더 만들었다.
			if (hot == 0)
			{
				Debug.LogWarning(TAG + " 뚫린 자리 FAIL — 건물을 잃었는데 뜨거운 자리가 0곳이다(규칙이 손실을 못 봤다).");
			}
			else if (breachHud == null)
			{
				Debug.Log(TAG + " 뚫린 자리 — 화면은 못 쟀다: HUD 를 못 찾았다.");
			}
			else if (spoken == false)
			{
				// 알림 칸은 유한하다 — 꽉 차 있었으면 밀려난 것이지 안 뜬 것이 아니다.
				if (match.Alerts.Count >= TowerDefenseAlerts.MAX_ALERTS)
					Debug.Log(TAG + " 뚫린 자리 — 화면은 못 쟀다: 알림 칸이 이미 꽉 차 있었다. 실패가 아니다.");
				else
					Debug.LogWarning(TAG + " 뚫린 자리 FAIL — 자리가 남았는데도 왜 그쪽으로 오는지 화면이 말하지 않는다.");
			}
			else if (movedAfter >= movedBefore - 0.5f)
				Debug.LogWarning(TAG + " 뚫린 자리 FAIL — 뜨거운 자리는 생겼는데 다음 파도가 그쪽으로 안 끌린다.");
			else
				Debug.Log(TAG + " 뚫린 자리 — 끌렸다. 잃은 쪽으로 " + (movedBefore - movedAfter).ToString("F1") + "도 다가왔다.");
		}

		private static double lairClearCheckAt;
		private static int lairClearEssenceBefore;
		private static int lairClearBefore;

		/// <summary> 서식지를 다 쓸면 정수가 들어오나 — 「싸워서 버는 길」이 실제로 이어져 있는지. </summary>
		private static void CheckLairClearReward()
		{
			if (match == null)
				return;

			int gained = match.Essence - lairClearEssenceBefore;
			int clearedNow = match.LairsCleared - lairClearBefore;
			Debug.Log($"{TAG} 서식지 소탕 보상 — 쓸어낸 곳 +{clearedNow} · 정수 +{gained}");

			if (clearedNow <= 0)
				Debug.LogError(TAG + " 소탕 FAIL — 다 쓸었는데 「쓸어낸 서식지」가 안 는다.");
			else if (gained <= 0)
				Debug.LogError(TAG + " 소탕 FAIL — 쓸었다고 세는데 정수가 한 푼도 안 들어온다.");
		}
	}
}
