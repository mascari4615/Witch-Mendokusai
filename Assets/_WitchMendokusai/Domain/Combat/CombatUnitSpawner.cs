using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 유닛 하나를 「매치 참가자」로 만드는 공용 관문 — 투기장(WM-165)과 개척(WM-194)이 함께 쓴다.
	///
	/// ★ 왜 모았나: 두 게임이 같은 절차를 각자 적어두고 있었고, 한 줄이 빠지면 *그 유닛만* 혼자
	///   다르게 논다. 실제로 겪은 것이 둘이다 —
	///   ① 자동시전을 안 끄면 전술을 무시하고 스킬을 난사한다(트랩#1).
	///   ② 프리팹에 내장된 자율 brain 을 안 끄면 전술과 *같은 이동 채널*을 매 틱 다투어
	///      제자리를 맴돈다(트랩#2). 둘 다 조용히 어긋나서 로그로는 안 보인다.
	///
	/// ★ 안 모은 것: 풀에서 꺼내기 · 자리 잡기 · 활성화 · 표적 등록 시점. 두 게임이 서로 다른 순서를
	///   *의도적으로* 쓴다(개척은 Start 초기화가 가라앉도록 한 프레임 양보한 뒤 Init, 투기장은 즉시).
	///   표본 둘로 그 순서까지 묶으면 잘못된 경계가 굳는다 — 세 번째 장르가 왔을 때 다시 본다.
	/// </summary>
	public static class CombatUnitSpawner
	{
		/// <summary>
		/// Init → 자동시전 차단(트랩#1) → MatchCombatant 부여. 이 *순서*가 곧 계약이다:
		/// SkillHandler 는 Init 이 세우므로 그 전에 꺼봐야 되살아난다.
		/// </summary>
		public static MatchCombatant Enlist(UnitObject unitObject, Unit unitData, int teamId, int combatantId)
		{
			unitObject.Init(unitData);
			unitObject.SkillHandler.AutoCastEnabled = false;

			MatchCombatant combatant = unitObject.GetComponent<MatchCombatant>();
			if (combatant == null)
				combatant = unitObject.gameObject.AddComponent<MatchCombatant>();
			combatant.SetTeam(teamId, combatantId);
			return combatant;
		}

		/// <summary>
		/// 트랩#2 — 프리팹 내장 자율 brain 격리. <b>활성화(SetActive) 뒤에</b> 불러야 한다:
		/// OnEnable 이 코루틴을 띄운 다음이라야 enabled=false 가 OnDisable→정지로 이어진다.
		/// 구체 타입을 세지 않고 마커 베이스(UnitBrain)로 훑으므로 새 brain 이 생겨도 자동으로 걸린다.
		/// </summary>
		public static void SilenceBrains(GameObject unitGameObject)
		{
			foreach (UnitBrain brain in unitGameObject.GetComponents<UnitBrain>())
				brain.enabled = false;
		}
	}
}
