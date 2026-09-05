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
	// TowerDefensePlayVerify 의 살림 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		private static int killIncomeEvents;

		private static double pressureCheckAt;
		private static float pressureBefore;

		/// <summary> 시간이 흐르면 강도가 오르고, 오르면 화면이 말하는가. </summary>
		private static void CheckPressureNotice()
		{
			if (match == null)
				return;

			float now = match.Pressure;
			int alerts = 0;
			foreach (TowerDefenseAlerts.Alert alert in match.Alerts)
			{
				if (alert.Label.Contains("단단해졌다"))
					alerts++;
			}

			// ★ 알림 목록에 있다 ≠ 화면에 떴다. 이 판에서 그 둘이 갈린 적이 있어(숨긴 칸에 얹혀 있던
			//   규칙들) 목록만 보고 통과시키면 안 된다. 알림은 몇 초면 사라지므로 *여기서* 훑는다.
			bool onScreen = false;
			UIRoot pressureUiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement pressureHud = pressureUiRoot != null && pressureUiRoot.ModeHudLayer != null
				? pressureUiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (pressureHud != null)
			{
				foreach (Label label in pressureHud.Query<Label>().ToList())
				{
					if (label == null || string.IsNullOrEmpty(label.text) || label.text.Contains("단단해졌") == false)
						continue;
					onScreen = true;
					Debug.Log(TAG + " 강도 알림 — 화면 글자: 「" + label.text + "」");
					break;
				}
			}

			Debug.Log($"{TAG} 강도 알림 — 강도 {pressureBefore:F2} → {now:F2} · 「더 단단해졌다」 알림 {alerts}개"
				+ " · 화면표시 " + onScreen);

			if (alerts > 0 && pressureHud != null && onScreen == false)
				Debug.LogWarning(TAG + " 강도 알림 FAIL — 규칙은 알렸는데 화면 어디에도 안 뜬다.");

			if (now <= pressureBefore + 0.01f)
				Debug.Log(TAG + " 강도 알림 — 못 쟀다(배속을 올려도 강도가 안 올랐다). 실패가 아니다.");
			else if (now - 1f < 0.5f)
				Debug.Log($"{TAG} 강도 알림 — 못 쟀다(아직 한 칸 못 올랐다: {now - 1f:F2}/0.50). 실패가 아니다.");
			else if (alerts == 0 && match.Alerts.Count >= TowerDefenseAlerts.MAX_ALERTS)
				Debug.Log(TAG + " 강도 알림 — 못 쟀다: 알림 칸이 꽉 차 먼저 난 것이 밀려났다. 실패가 아니다.");
			else if (alerts == 0)
				Debug.LogError(TAG + " 강도 FAIL — 자리가 남았는데도 강도가 오른 것을 화면이 말하지 않는다.");

			// ★ 깨어난 서식지 마수가 *어디로 가는지*를 잰다. 코어로 행진하면 서식지는 그냥 「파도 하나 더」이고,
			//   그 일대에 머물면 「넓히는 것이 위험」이 성립한다 — 둘은 완전히 다른 게임이라 재봐야 안다.
			if (match.WakeNearestLairForVerification(out Vector3 wokenAt))
			{
				lairWakeFrom = match.AwakenedGuardDistanceToCore(out lairWakeGuards);
				lairWakePosition = wokenAt;
				lairDriftCheckAt = EditorApplication.timeSinceStartup + 5.0;
				lairWakeMatch = match; // ★ 잰 판이 깨운 판과 같은지 확인용 — 다르면 비교 자체가 무의미하다.
				lairWakeLives = match.Lives;
				lairWakeEnemies = match.WaveEnemies.Count;
				Debug.Log($"{TAG} 서식지 강제 기상 — 마수 {lairWakeGuards}기 · 코어까지 {lairWakeFrom:F1}"
					+ $" · 목숨 {lairWakeLives} · 판 위 마수 {lairWakeEnemies}");
			}
		}

		/// <summary> 보급을 끌고 갈 때 전초기지 사이 간격 — 너무 벌리면 다음 자리가 보급 밖이 된다. </summary>
		private const float OUTPOST_STEP = 10f;

		private static double relayProbeAt;
		private static int relayCapacityBefore;

		/// <summary> 세운 중계가 신호를 받아 용량을 늘렸는가 — 컨트롤넷이 말뿐인지 아닌지의 결론. </summary>
		private static void CheckRelayChain()
		{
			if (match == null)
				return;

			int fed = match.FedRelayCount;
			int capacity = match.PowerCapacity;
			Debug.Log($"{TAG} 신호 사슬 결과 — 받는 중계 {fed}기 · 용량 {relayCapacityBefore} → {capacity}");

			if (fed == 0)
				Debug.LogError(TAG + " 사슬 FAIL — 코어 옆에 세운 중계가 신호를 못 받는다(사슬이 한 칸도 안 뻗는다).");
			else if (capacity <= relayCapacityBefore)
				Debug.LogError(TAG + " 사슬 FAIL — 중계가 신호는 받는데 용량이 안 는다(받아도 아무 일도 안 일어난다).");
		}

		/// <summary>
		/// 정수 — 바깥 노드 채집이 정수를 내고, 강화(연구·승급)가 자원이 아니라 정수를 쓰는가.
		/// 「멀리 나가야 강해진다」가 두 통장으로 성립하는지 본다.
		/// </summary>
		private static void VerifyEssence(string when = "배치 직후")
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// 배치는 이미 DoPlacements 가 했다(코루틴이 끝날 시간을 벌기 위해) — 여기선 결과만 읽는다.
			// ★ 읽기 전에 판에게 *다시 세어보라*고 시킨다. 안 시켰더니 발전 인형을 세운 지 0.85초 만에
			//   읽어 「전기가 안 닿음」이라 찍혔는데, 라이브로 물어보니 전기는 닿아 있었다(정수도 나고 있었다).
			//   기다리는 시간을 늘려 해결하려다 오히려 순서가 깨졌다 — 시계와 싸우는 대신 결정적으로 만든다.
			match.RefreshSupplyForVerification();
			string verdict = TAG + " ESSENCE[" + when + "] harvesters=" + match.HarvesterCount
				+ " outer=" + match.OuterHarvesters + "/판의바깥광맥=" + match.OuterNodeCount
				+ " outerSupplied=" + match.SuppliedOuterHarvesters
				+ " outerPowered=" + match.PoweredOuterHarvesters
				+ " nextIncome=" + match.NextWaveIncome
				+ " nextEssence=" + match.NextWaveEssence
				+ " essence=" + match.Essence;

			// ★ 세 원인을 갈라 말한다 — 안 갈라 말하면 「바깥 노드인데 안 나온다」는 *거짓 실패*가 찍힌다
			//   (실측: 실제로는 바깥에 세운 적이 없거나, 세웠어도 사슬이 안 닿아 있었다).
			if (match.HarvesterCount == 0)
				Debug.Log(verdict + " → 채집을 못 세움(자원 부족/자리 없음) — 확인 못 함");
			else if (match.OuterNodeCount == 0)
				Debug.LogError(verdict + " → 이 판에 바깥 광맥이 아예 없다 — 정수를 낼 자리가 없으니 「멀리 나가야 강해진다」 축이 통째로 죽는다.");
			else if (match.OuterHarvesters == 0)
				Debug.Log(verdict + " → 바깥 광맥은 있는데 거기 못 세움 — 정수 0 은 정상, 확인 못 함");
			else if (match.SuppliedOuterHarvesters == 0)
				Debug.Log(verdict + " → 바깥에 세웠지만 사슬이 안 닿음 — 정수 0 은 규칙대로, 확인 못 함");
			else if (match.PoweredOuterHarvesters == 0)
				Debug.Log(verdict + " → 이어졌지만 전기가 안 닿음 — 정수 0 은 규칙대로, 확인 못 함");
			else if (match.NextWaveEssence > 0)
				Debug.Log(verdict + " → 이어진 바깥 채집이 정수를 낸다 ✔");
			else
				Debug.LogError(verdict + " → 이어진 바깥 채집이 있는데 정수가 안 나온다.");
		}

		/// <summary> 전초기지 — 정수로 서고, 마수의 목표(유출 지점)가 하나 느는가. </summary>
		private static void VerifyOutpost()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			if (match.Essence < match.Stage.OutpostEssenceCost)
			{
				Debug.Log(TAG + " OUTPOST-SKIP 정수 부족(" + match.Essence + "/" + match.Stage.OutpostEssenceCost
					+ ") — 첫 정산 전에는 못 세움(의도된 설계)");
				return;
			}

			int before = match.OutpostCount;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 1))
			{
				match.TryPlaceOutpost(stageRoot.TransformPoint(local.ToUnity()).ToSim());
				break;
			}

			string verdict = TAG + " OUTPOST count " + before + " → " + match.OutpostCount
				+ " essence=" + match.Essence + " supplied=" + match.SuppliedBuildings;
			if (match.OutpostCount > before)
				Debug.Log(verdict + " → 지킬 곳이 하나 늘었다 ✔");
			else
				Debug.LogError(verdict + " → 전초기지가 안 선다.");
		}

		/// <summary> 보급 — 코어에서 이어진 건물이 잡히고, 끊긴 채집이 수입에서 빠지는가. </summary>
		private static void VerifySupply()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// ★ 확인할 것은 「가까운 건 이어지고 먼 건 안 이어진다」이다. 앞 단계(판매)가 코어 근처 건물을
			//   치워버려 사슬의 시작점이 사라졌으므로, 코어 옆에 하나 세워 대조군을 만든다.
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 1))
			{
				match.TryPlaceWall(stageRoot.TransformPoint(local.ToUnity()).ToSim());
				break;
			}

			string verdict = TAG + " SUPPLY buildings=" + match.SupplyBuildingCount
				+ " reach=" + match.Stage.SupplyReach
				+ " supplied=" + match.SuppliedBuildings
				+ " disconnected=" + match.DisconnectedHarvesters
				+ " nextIncome=" + match.NextWaveIncome
				+ " nextEssence=" + match.NextWaveEssence;

			if (match.SuppliedBuildings > 0)
				Debug.Log(verdict + " → 코어에서 사슬이 이어진다 ✔");
			else
				Debug.LogError(verdict + " → 아무 건물도 보급에 안 잡힌다(사슬 계산 실패).");
		}

		/// <summary> 이벤트 웨이브 — 웨이브마다 성격이 붙고, 마리수가 실제로 달라지는가(예고와 스폰이 같은 함수). </summary>
		private static void VerifyWaveEvents()
		{
			if (match == null)
				return;

			System.Text.StringBuilder line = new();
			int eventWaves = 0;
			int countVaried = 0;
			int plainCount = match.ScaledEnemyCount(0);

			for (int wave = 0; wave < 9; wave++)
			{
				TowerDefenseWaveEventKind kind = match.WaveEventAt(wave);
				int count = match.ScaledEnemyCount(wave);
				if (kind != TowerDefenseWaveEventKind.None)
				{
					eventWaves++;
					if (count != match.Stage.Rules.EnemiesInWave(wave))
						countVaried++;
				}
				line.Append(wave).Append(':').Append(TowerDefenseWaveEvent.DisplayName(kind) is { Length: > 0 } name ? name : "-")
					.Append('(').Append(count).Append(") ");
			}

			string verdict = TAG + " WAVE-EVENTS " + line.ToString().TrimEnd()
				+ " | eventWaves=" + eventWaves + " countVaried=" + countVaried + " plain0=" + plainCount;
			if (eventWaves >= 2 && countVaried >= 1)
				Debug.Log(verdict + " → 웨이브마다 성격이 바뀐다 ✔");
			else
				Debug.LogError(verdict + " → 성격이 안 붙거나 마리수가 안 변한다.");
		}

		/// <summary> 승급 — 같은 자리에 같은 종류를 다시 지으면 단계가 오르고 사거리·피해가 자라는가. </summary>
		/// <summary>
		/// 정수가 모자랄 때 화면이 **버는 법까지** 말하는가.
		///
		/// ★ 사용자가 직접 물은 것이다: "정수 어떻게 얻어? 강화를 할 수가 없는데?" 화면이 「부족」만
		///   말하면 사람은 거기서 막힌다 — 모자란 건 이미 아는 사실이고, 필요한 건 *다음 행동*이다.
		/// ★ 승급은 아예 조용히 실패하고 있었다(눌러도 아무 말이 없다 = 고장으로 읽힌다).
		/// </summary>
		private static void VerifyEssenceShortageTalks()
		{
			if (match == null)
				return;

			// 정수를 바닥내고 정수로 사는 것을 눌러 본다 — 거절 문구가 나와야 한다.
			int essence = match.Essence;
			if (essence > 0)
				match.SpendEssenceForVerification(essence);

			int before = match.Essence;
			bool outpostRejected = match.TryPlaceOutpost(FindStageRoot() != null
				? FindStageRoot().TransformPoint(new Vector3(6f, 0f, 6f).ToUnity()).ToSim()
				: Vector3.zero) == false;

			Debug.Log($"{TAG} 정수 안내 — 정수 {before} · 전초기지 거절 {outpostRejected}");

			if (outpostRejected == false)
			{
				Debug.Log(TAG + " 정수 안내 — 못 쟀다(정수 0 인데도 지어졌다면 값이 0 인 스테이지다).");
				return;
			}

			// 마지막 거절 문구를 매치가 들고 있어야 화면이 무엇을 말했는지 잴 수 있다.
			string said = match.LastRejectReason;
			Debug.Log($"{TAG} 정수 안내 — 화면이 한 말: 「{said}」");

			// ★ 거절 사유가 *정수 말고 다른 것*일 수 있다 — 그 자리가 이미 찼거나 보급 밖이면
			//   화면은 그 이유를 말하는 게 맞다. 그걸 「정수를 안 말한다」로 부르면 멀쩡한 문구를
			//   고치러 간다. 실제로 다른 탐침이 그 자리에 전초기지를 놓아 이 검사가 거짓 실패했다.
			if (string.IsNullOrEmpty(said))
				Debug.LogError(TAG + " 정수 안내 FAIL — 거절해 놓고 이유를 아무것도 안 남긴다.");
			else if (said.Contains("정수 부족") == false)
				Debug.Log(TAG + " 정수 안내 — 못 쟀다: 정수가 아니라 다른 이유로 거절됐다(「" + said + "」). 실패가 아니다.");
			else if (said.Contains("채집") == false || said.Contains("둥지") == false || said.Contains("서식지") == false)
				Debug.LogError(TAG + " 정수 안내 FAIL — 「부족」만 말하고 *버는 법*을 안 말한다: 「" + said + "」");
		}
	}
}
