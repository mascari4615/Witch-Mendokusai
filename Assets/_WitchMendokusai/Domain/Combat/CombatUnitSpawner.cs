using System.Collections.Generic;
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
			SilenceBrains(unitGameObject, null);
		}

		/// <summary>
		/// 같은 일을 하되 <b>실제로 끈 것만</b> <paramref name="silencedOut"/> 에 적어둔다 —
		/// 반납할 때 그것만 되돌리기 위해서다.
		///
		/// ★ 왜 「끈 것만」인가: 원래 꺼져 있던 brain 을 반납 때 켜면 그건 복구가 아니라 **변조**다.
		///   유닛마다 어떤 brain 이 켜져 있었는지는 프리팹 사정이라 여기서 알 수 없다.
		/// </summary>
		public static void SilenceBrains(GameObject unitGameObject, List<UnitBrain> silencedOut)
		{
			foreach (UnitBrain brain in unitGameObject.GetComponents<UnitBrain>())
			{
				if (brain.enabled == false)
					continue;

				brain.enabled = false;
				silencedOut?.Add(brain);
			}
		}

		/// <summary>
		/// 편입 때 우리가 끈 brain 들을 되돌린다 — 매치가 끝나 유닛을 풀에 돌려주기 <b>전에</b> 부른다.
		///
		/// ★ 왜 필요한가 (2026-08-06 실측): 풀은 상태를 안 씻는다. 편입 때 끈 것들이 그대로 남은 채
		///   반납되면 <b>그 인스턴스가 다음에 던전·본편에서 나올 때 그대로 고장나 있다</b> —
		///   brain 이 꺼져 있으니 안 움직이고, 자동시전이 꺼져 있으니 스킬을 안 쓴다.
		///   게다가 자동시전은 <c>UnitObject.Init</c> 이 **일부러 보존**한다(재-Init 로도 안 돌아온다.
		///   그 보존은 애초에 투기장 트랩#1 때문에 들어간 것이다) — 즉 명시 복구 말고는 길이 없다.
		///   투기장 자기 주석이 팀 틴트에 대해 「관전용 색이 본편으로 새는 것이다」라고 적어뒀는데,
		///   **거동은 색보다 나쁘고 더 안 보인다.**
		///   개척은 이 문제를 유닛별 <c>TowerDefenseUnitLease</c>(스냅샷/복구)로 이미 풀어놨다 —
		///   이 함수는 그 지식의 최소판이다(투기장은 lease 를 안 쓴다).
		/// </summary>
		public static void RestoreBrains(IReadOnlyList<UnitBrain> silenced)
		{
			if (silenced == null)
				return;

			foreach (UnitBrain brain in silenced)
			{
				if (brain != null)
					brain.enabled = true;
			}
		}

		/// <summary>
		/// 자동시전을 되살린다. <c>UnitObject.Init</c> 이 이 값을 **일부러 보존**하므로(그 보존이 애초에
		/// 투기장 트랩#1 때문에 들어갔다) 재-Init 로는 안 돌아온다 — 명시 복구 말고는 길이 없다.
		///
		/// ★ brain 과 달리 <b>무조건 true</b> 로 되돌리는 게 맞다(스냅샷 불요) — 전수 확인 결과
		///   <c>AutoCastEnabled = false</c> 를 놓는 곳은 <b>매치 시스템 둘뿐</b>이고
		///   (<see cref="Enlist"/> / <c>TacticDriver.Initialize</c>) 기본값은 true 다.
		///   즉 매치 밖에서 이걸 꺼두는 주체가 없으므로 「원래 꺼져 있었을 수도」가 성립하지 않는다.
		///   brain 은 반대다 — 프리팹마다 꺼둔 것이 있을 수 있어서 <b>끈 것만</b> 기록해 되돌린다.
		///   (개척의 lease 도 같은 이유로 여기만 하드코딩 true 다.)
		/// </summary>
		public static void RestoreAutoCast(GameObject unitGameObject)
		{
			if (unitGameObject == null)
				return;

			UnitObject unitObject = unitGameObject.GetComponent<UnitObject>();
			if (unitObject != null && unitObject.SkillHandler != null)
				unitObject.SkillHandler.AutoCastEnabled = true;
		}
	}
}
