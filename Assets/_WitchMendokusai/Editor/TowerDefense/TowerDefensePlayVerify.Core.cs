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
	// TowerDefensePlayVerify 의 핵 카드와 은혜 확인 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefensePlayVerify.cs 를 본다.
	public static partial class TowerDefensePlayVerify
	{
		// 코어 레벨 한 단계는 넘기고도 남는 양 — 카드가 확실히 걸리게.
		private const int CORE_XP_FOR_CARDS = 500;
		// 건물 한 단계는 넘기고도 남는 양.
		private const int BUILDING_XP_FOR_PERKS = 300;

		/// <summary>
		/// 코어를 실제로 골라 코어 카드를 띄운다 — 카드는 코어를 골라야 나온다.
		/// (수비대를 고르면 강화 선택지만 나오고 카드는 안 나온다 — 둘은 다른 화면이다.)
		/// </summary>
		private static void SelectCoreForLayout()
		{
			Transform stageRoot = FindStageRoot();
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null || match == null || stageRoot == null || match.CoreCombatant == null)
				return;

			placement.Disarm();
			placement.PlaceSelectedAt(WorldToScreen(modeCamera, match.CoreCombatant.Position));
		}

		/// <summary>
		/// 코어 카드 — 뜨는가, 그리고 *고르면 실제로 걸리는가*.
		/// ★ 화면에 떴다는 것만으론 부족하다 — 눌러도 아무 일도 안 일어나는 카드가 이 작업에서 이미 나왔다.
		/// </summary>
		private static void VerifyCoreCards()
		{
			if (match == null)
				return;

			VerifyHudLayout("코어 선택 중", mustBeUp: "SelectionPanel");

			List<TowerDefenseBoon> offers = new();
			match.OfferCoreCards(offers);
			int beforeTaken = match.BoonCount;
			bool chosen = offers.Count > 0 && match.ChooseCoreCard(0);

			string verdict = TAG + " CORE-CARDS pending=" + match.CorePendingChoices
				+ " offered=" + offers.Count + " chosen=" + chosen
				+ " 고른장수 " + beforeTaken + "→" + match.BoonCount
				+ " [" + match.BoonSummary + "]";

			if (offers.Count > 0 && chosen && match.BoonCount > beforeTaken)
				Debug.Log(verdict + " → 코어 카드가 뜨고 고르면 걸린다 ✔");
			else
				Debug.LogError(verdict + " → 카드가 안 뜨거나 골라도 안 걸린다.");
		}

		/// <summary>
		/// 건물 강화 선택지 — 자란 건물에 실제로 걸리는가.
		/// ★ 「선택지가 화면에 있다」와 「골라서 수치가 바뀐다」는 다른 얘기다. 뒤쪽까지 본다.
		/// </summary>
		private static void VerifyBuildingPerk()
		{
			if (match == null)
				return;

			MatchCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is MatchCombatant matchCombatant == false)
					continue;
				if (matchCombatant.TeamId != 0 || matchCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && matchCombatant == match.CoreCombatant)
					continue;

				target = matchCombatant;
				break;
			}

			if (target == null || match.GrantBuildingExperienceForVerification(target, BUILDING_XP_FOR_PERKS) == false)
			{
				Debug.Log(TAG + " PERK-SKIP 자라게 할 건물이 없음 — 확인 못 함");
				return;
			}

			TowerDefenseDollLabel doll = match.FindDoll(target);
			List<TowerDefenseBuildingPerk> offers = new();
			TowerDefenseBuildingProgress.Offer(doll.BuildingId, doll.Progress.Level, doll.IsHarvester, offers);

			int beforeTaken = doll.Progress.Taken.Count;
			int beforePending = doll.Progress.PendingChoices;
			bool applied = offers.Count > 0 && match.ChooseBuildingPerk(target, offers[0]);

			string verdict = TAG + " PERK level=" + doll.Progress.Level + " pending=" + beforePending
				+ " offered=" + offers.Count + " applied=" + applied
				+ " 고른수 " + beforeTaken + "→" + doll.Progress.Taken.Count;

			if (applied && doll.Progress.Taken.Count > beforeTaken)
				Debug.Log(verdict + " → 자란 건물이 선택지를 받고 고르면 걸린다 ✔");
			else
				Debug.LogError(verdict + " → 선택지가 안 나오거나 골라도 안 걸린다.");
		}

		/// <summary> 세워둔 건물을 실제로 골라 선택 패널을 띄운다(무장 해제 상태의 클릭 = 고르는 클릭). </summary>
		private static void SelectPlacedBuildingForLayout()
		{
			Transform stageRoot = FindStageRoot();
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null || match == null || stageRoot == null)
				return;

			// ★ *살아 있는* 건물을 고른다 — 앞서 판매 확인이 시험용 포탑을 팔아버려서, 그 자리를 누르면
			//   빈 땅을 누르는 꼴이 된다(그래서 패널이 영영 안 열렸다). 코어는 선택 대상이 아니다.
			MatchCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is MatchCombatant matchCombatant == false)
					continue;
				if (matchCombatant.TeamId != 0 || matchCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && matchCombatant == match.CoreCombatant)
					continue;

				target = matchCombatant;
				break;
			}

			if (target == null)
			{
				Debug.Log(TAG + " HUD-SELECT 고를 건물이 없음 — 선택 패널 배치는 확인 못 함");
				return;
			}

			placement.Disarm();
			placement.PlaceSelectedAt(WorldToScreen(modeCamera, target.Position));
		}
	}
}
