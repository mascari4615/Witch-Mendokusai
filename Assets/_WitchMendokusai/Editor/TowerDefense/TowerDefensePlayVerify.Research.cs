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
	// TowerDefensePlayVerify 의 연구 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		/// <summary>
		/// 연구 — 코어를 골라 정수로 한 단계 올린다. 자원이 아니라 *정수*로 사므로 앞의 항목들과 지갑이 달라
		/// 순서를 다툴 일이 없다. 정수가 모자라면 「확인 못 함」으로 남긴다(가짜 실패 X).
		///
		/// ★ 예전엔 이 확인이 「연구 인형을 짓는다」였는데, 그 건물은 핫바에서 사라진 뒤로 *플레이어가
		///   갈 수 없는 길*이 되어 있었다 — 그런데도 확인은 초록불을 켰다(핫바를 우회해 직접 불렀으니까).
		///   아무도 못 가는 길에 켜지는 초록불은 없느니만 못하다. 살아있는 길로 옮긴다.
		/// </summary>
		/// <summary>
		/// 갈래 하나를 찍고 *판에서 읽히는 값*이 실제로 오르는지 잰다.
		/// 통과해도 전후 숫자를 남긴다 — 「올랐다」만 적으면 얼마나 올랐는지 사람이 못 본다.
		/// </summary>
		private static void VerifyResearchEffect(string label, TowerDefenseResearchEffect effect,
			System.Func<float> read)
		{
			float before = read();
			bool taken = match.TryTakeResearchNode(effect, 0.25f, cost: 0, usesEssence: false);
			float after = read();
			Debug.Log(TAG + " RESEARCH-NODE " + label + " accepted=" + taken
				+ " " + before.ToString("F2") + " → " + after.ToString("F2"));
			if (taken == false)
				Debug.LogError(TAG + " RESEARCH-FAIL " + label + " 마디를 못 찍었다.");
			else if (after <= before)
				Debug.LogError(TAG + " RESEARCH-FAIL " + label + " 갈래가 하는 일이 없다 — 찍어도 판이 그대로다.");
		}

		private static void VerifyResearch()
		{
			if (match == null)
				return;

			// ★ 플레이어가 실제로 가는 길로 확인한다. 예전엔 옛 연구(단추 한 번에 한 단계)를 불렀는데,
			//   그 길은 이제 화면 어디에서도 안 열린다 — **아무도 못 가는 길을 확인하고 초록불**을 켜고
			//   있었다. 확인 도구가 실물과 다른 길을 밟으면 그 뒤로 무엇을 확인해도 못 믿는다.
			//   지금 연구 = 성좌의 마디를 찍는 것. 값은 0 으로 부른다(정수 유무에 흔들리지 않게 —
			//   값을 치르는 길은 규칙층 시험이 따로 잠갔다).
			float damageBefore = match.TowerDamageMultiplier;
			bool damageTaken = match.TryTakeResearchNode(TowerDefenseResearchEffect.TowerDamage, 0.25f, cost: 0, usesEssence: false);
			Debug.Log(TAG + " RESEARCH-NODE 피해 accepted=" + damageTaken
				+ " damageMultiplier " + damageBefore.ToString("F2") + " → " + match.TowerDamageMultiplier.ToString("F2"));
			if (damageTaken == false)
				Debug.LogError(TAG + " RESEARCH-FAIL 마디를 못 찍었다 — 성좌에서 아무것도 못 고른다는 뜻이다.");
			else if (match.TowerDamageMultiplier <= damageBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 찍었는데 포탑이 안 세졌다 — 연구가 하는 일이 없다.");

			// ★ 여섯 갈래를 *전부* 잰다. 예전엔 둘만 봤는데, 안 보는 갈래는 조용히 죽어도 아무도 모른다
			//   (실제로 오늘 카드 셋이 「뽑히는데 효과 0」인 채로 살아 있었다).
			//   각 갈래마다 *판에서 실제로 읽히는 값*을 전후로 찍는다 — 통과해도 숫자를 남겨야
			//   「무엇이 얼마나 세졌나」를 사람이 눈으로 확인할 수 있다.
			// ★ 배수가 아니라 *화면이 그리는 원*으로 잰다 — 배수만 재면 「총은 멀리 나가는데 원은 그대로」를
			//   못 본다(실제로 그랬다). 원이 거짓말하면 배치 판단의 근거가 통째로 사라진다.
			VerifyResearchEffect("사거리", TowerDefenseResearchEffect.TowerRange,
				() => match.TowerRange());
			VerifyResearchEffect("보급 거리", TowerDefenseResearchEffect.SupplyReach,
				() => match.EffectiveSupplyReach);
			// 규칙이 아니라 *화면에 그려진 원*도 같이 잰다 — 사거리에서 겪은 그 병(총만 멀리 나감)이
			// 보급에도 그대로 있었다. 규칙만 재면 원이 굳어도 초록불이 켜진다.
			Debug.Log(TAG + " SUPPLY-RING 그려진 원 " + match.DrawnSupplyReach.ToString("F2")
				+ " · 규칙 " + match.EffectiveSupplyReach.ToString("F2"));
			if (match.DrawnSupplyReach > 0f
				&& Mathf.Approximately(match.DrawnSupplyReach, match.EffectiveSupplyReach) == false)
			{
				Debug.LogError(TAG + " SUPPLY-RING-FAIL 원과 실제 보급 거리가 다르다 — 원이 거짓말한다.");
			}
			VerifyResearchEffect("채집 수입", TowerDefenseResearchEffect.HarvestYield,
				() => match.NextWaveIncome);
			VerifyResearchEffect("코어 방어", TowerDefenseResearchEffect.CoreArmor,
				() => match.CoreCombatant != null && match.CoreCombatant.UnitObject != null
					? match.CoreCombatant.UnitObject.UnitStat[UnitStatType.HP_MAX] : 0f);
			// 영웅은 판에 영웅이 서 있어야 값이 읽힌다 — 누적 비율로 확인한다(그것마저 안 오르면 배선이 끊긴 것).
			VerifyResearchEffect("영웅", TowerDefenseResearchEffect.HeroPower,
				() => match.ResearchBonus(TowerDefenseResearchEffect.HeroPower));

			// 큰 마디 = 연구 한 단계 = 새 칸 해금. 이 고리가 끊기면 성좌를 다 뚫어도 지을 것이 그대로다.
			int levelBefore = match.ResearchLevel;
			int slotsBefore = match.AvailableSlots.Count;
			match.GrantResearchLevel();
			Debug.Log(TAG + " RESEARCH-LEVEL " + levelBefore + " → " + match.ResearchLevel
				+ " 칸 " + slotsBefore + " → " + match.AvailableSlots.Count);
			if (match.ResearchLevel <= levelBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 큰 마디를 뚫었는데 단계가 안 올랐다.");

			VerifyResearchRestoreWithoutPanel();
			VerifyResearchPanel();
			// ★ *포탑이 선 뒤*에 잰다 — 채집만 세운 시점에 재봤더니 사거리 원이 아예 0개라
			//   「0개 중 0개 어긋남」이라는 헛초록불이 켜졌다. 안 돈 검사는 검사가 아니다.
			VerifyRingMeaning();
		}

		/// <summary>
		/// 세워진 물건의 원이 *그 물건이 뜻하는 것*을 그리는가.
		///
		/// ★ 사거리 원은 「이만큼 쏜다」는 뜻이다 — 쏘지 않는 물건에 뜨면 그 자체로 거짓말이다.
		///   지금은 세우는 쪽이 채집·발전을 걸러내고 있어서 성립하는데, 그 가드가 사라지면 조용히 깨진다.
		/// ★ 하나도 못 쟀으면 실패로 본다 — 안 돈 검사는 통과가 아니다(실제로 그 헛초록불을 봤다).
		/// </summary>
		private static void VerifyRingMeaning()
		{
			// ★ 꺼져 있는 것도 센다 — 사거리 원은 물어볼 때만 보이므로 평소엔 숨어 있다.
			//   숨은 것을 안 세면 「원이 하나도 없다」는 거짓 진단이 나온다.
			TowerDefenseRing[] rings = Object.FindObjectsByType<TowerDefenseRing>(FindObjectsInactive.Include);
			int total = 0;
			int wrong = 0;
			foreach (TowerDefenseRing ring in rings)
			{
				if (ring == null || ring.name != "RangeRing")
					continue;

				total++;
				Transform owner = ring.transform.parent;
				if (owner != null && owner.GetComponent<TowerDefenseWeapon>() != null)
					continue;

				wrong++;
				Debug.LogError(TAG + " RING-MEANING-FAIL " + (owner != null ? owner.name : "?")
					+ " 에 사거리 원이 " + ring.Radius.ToString("F2") + " 로 떠 있는데 쏘는 물건이 아니다.");
			}

			// 이름별로 남긴다 — 0 이 나왔을 때 「원이 없다」인지 「이름이 다르다」인지 바로 갈린다.
			string names = "";
			foreach (TowerDefenseRing ring in rings)
			{
				if (ring != null)
					names += ring.name + " ";
			}

			Debug.Log(TAG + " RING-MEANING 사거리 원 " + total + "개 · 쏘지 않는데 뜬 것 " + wrong
				+ "개 · 판 위의 모든 원 [" + (names == "" ? "없음" : names.Trim()) + "]");
			if (total == 0)
				Debug.LogError(TAG + " RING-MEANING-FAIL 잴 것이 하나도 없었다 — 검사가 헛돈 것이지 통과가 아니다.");
		}

		/// <summary>
		/// 성좌 *화면* — 열리는가 · 전체화면인가 · 마디가 그려지는가 · 열면 판이 멈추고 닫으면 도는가.
		///
		/// ★ 이걸 재는 이유: 지금까지 성좌는 규칙층만 두드려 검사했고 **화면은 한 번도 안 열어봤다**.
		///   「전체화면으로」와 「그래프식으로」는 사용자가 직접 요청한 것인데, 그게 지켜지는지 말해주는
		///   기계가 하나도 없었다 — 안 재는 것은 조용히 죽는다.
		/// </summary>
		private static void VerifyResearchPanel()
		{
			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null || match == null)
			{
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 판 진행자를 못 찾았다.");
				return;
			}

			bool pausedBefore = match.IsPaused;
			controller.OpenResearchPanel();

			if (controller.IsResearchOpen == false)
			{
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 안 열린다 — 사람도 못 연다는 뜻이다.");
				return;
			}

			Debug.Log(TAG + " RESEARCH-PANEL 열림 · 마디 " + controller.ResearchNodeCount
				+ "개 · 판 멈춤 " + match.IsPaused);
			if (controller.ResearchNodeCount <= 1)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 마디가 없다 — 그래프가 아니라 빈 판이다.");
			if (match.IsPaused == false)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 화면을 덮었는데 판이 계속 돈다 — 그 사이 코어가 털린다.");

			// ★ 크기는 *다음 틱*에 잰다 — 연 그 프레임엔 자리가 아직 안 잡혀 NaN 이다(실측).
			//   재시작 단계가 바로 다음 틱에 돌므로 거기서 재고 닫는다(성좌를 연 채로 오래 두지 않는다).
			researchPanelPausedBefore = pausedBefore;
			researchPanelMeasurePending = true;
		}

		private static bool researchPanelPausedBefore;
		private static bool researchPanelMeasurePending;

		/// <summary> 열어둔 성좌를 *한 틱 뒤에* 재고 닫는다 — 「전체화면으로」가 지켜지는지는 이 숫자뿐이다. </summary>
		private static void MeasureAndCloseResearchPanel()
		{
			if (researchPanelMeasurePending == false)
				return;
			researchPanelMeasurePending = false;

			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null || match == null)
				return;

			// ★ 화면 픽셀과 견주면 안 된다 — UI 는 자기 좌표계(논리 픽셀)로 잰다. 배율이 1 이 아니면
			//   둘의 단위가 달라 「덮는 비율 55%」 같은 헛수가 나온다(실측: 배율을 Expand 로 바꾼 직후
			//   멀쩡한 전체화면이 실패로 찍혔다). *같은 좌표계에 있는 UI 뿌리*와 견준다.
			Rect panel = controller.ResearchScreenRect;
			Rect host = controller.UiRootRect;
			float hostArea = Mathf.Max(1f, host.width * host.height);
			float coverage = (panel.width * panel.height) / hostArea;
			Debug.Log(TAG + " RESEARCH-PANEL 덮는 비율 " + coverage.ToString("P0")
				+ " (" + panel.width.ToString("F0") + "x" + panel.height.ToString("F0")
				+ " / UI 뿌리 " + host.width.ToString("F0") + "x" + host.height.ToString("F0")
				+ " · 화면 " + Screen.width + "x" + Screen.height + ")");
			// 한 틱 뒤에 재는데도 NaN 이면 그건 「아직」이 아니라 자리가 영영 안 잡힌 것 — 실패다.
			if (float.IsNaN(coverage))
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 한 틱 뒤에도 자리가 안 잡혔다 — 크기를 잴 수 없다.");
			else if (coverage < 0.9f)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 전체화면이 아니다 — 요청은 화면을 통째로 덮는 것이었다.");

			controller.CloseOverlays();
			Debug.Log(TAG + " RESEARCH-PANEL 닫음 · 열림 " + controller.IsResearchOpen
				+ " · 판 멈춤 " + match.IsPaused + "(열기 전 " + researchPanelPausedBefore + ")");
			if (controller.IsResearchOpen)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 안 닫힌다.");
			if (match.IsPaused != researchPanelPausedBefore)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 닫았는데 멈춤 상태가 원래대로 안 돌아온다.");
		}

		/// <summary>
		/// 이어하기 — 성좌 화면을 *한 번도 안 연* 채로 저장을 되돌려도 연구가 살아 있는가.
		///
		/// ★ 이걸 재는 이유: 되돌리는 일을 성좌 화면이 들고 있으면, 화면은 사람이 처음 열 때 세워지는데
		///   이어하기는 그보다 먼저 일어난다 → 되돌릴 곳이 없어 저장에 적힌 연구가 통째로 조용히 사라진다.
		///   실제로 그랬다. 규칙이 화면 유무와 무관한지는 「화면 없이」 재야만 드러난다.
		/// </summary>
		private static void VerifyResearchRestoreWithoutPanel()
		{
			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 판 진행자를 못 찾았다.");
				return;
			}

			// ★ 사람이 찍는 것과 *같은 문*으로 찍는다 — 규칙층을 직접 두드리면 저장에 안 적히는
			//   병(방금 그것)을 검사기가 못 본다.
			// 값이 없으면 사람도 못 찍는다 — 찍는 일 자체가 목적이 아니므로 넉넉히 채워두고 시작한다.
			match.GrantForVerification(0, 99);
			if (controller.TryGetFirstResearchNodeId(out int firstNode) == false)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 코어에서 이어지는 마디가 하나도 없다 — 성좌가 안 세워졌다.");
				return;
			}

			if (controller.ChooseResearchNode(firstNode) == false)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 첫 마디를 못 찍었다(값 " + match.Essence + ") — 사람도 못 찍는다.");
				return;
			}

			List<int> saved = new List<int>();
			match.CollectResearchInto(saved);
			if (saved.Count == 0)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 찍었는데 저장에 적힐 마디가 0개다.");
				return;
			}

			// ★ 한 갈래만 재면 안 된다 — 되돌린 마디가 *다른* 갈래면 그 갈래는 그대로라 거짓 실패가 난다
			//   (실제로 처음 그렇게 재서 멀쩡한 고침을 실패로 읽었다). 갈래 전부의 합으로 잰다.
			float before = TotalResearchBonus();
			match.ClearResearch();
			float cleared = TotalResearchBonus();
			match.RestoreResearchFrom(saved);
			float after = TotalResearchBonus();

			Debug.Log(TAG + " RESEARCH-RESTORE 마디 " + saved.Count + "개 · 갈래 합 "
				+ before.ToString("F2") + " → 지움 " + cleared.ToString("F2") + " → 되돌림 " + after.ToString("F2"));
			if (Mathf.Approximately(cleared, 0f) == false)
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 새 판인데 지난 판 연구가 남아 있다.");
			if (after <= cleared)
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 이어하기가 연구를 못 되돌렸다 — 저장은 적혔는데 판이 안 받는다.");
		}

		/// <summary> 갈래 전부의 누적 합 — 어느 갈래가 되돌아왔든 잡힌다. </summary>
		private static float TotalResearchBonus()
		{
			float total = 0f;
			foreach (TowerDefenseResearchEffect effect in System.Enum.GetValues(typeof(TowerDefenseResearchEffect)))
				total += match.ResearchBonus(effect);
			return total;
		}
	}
}
