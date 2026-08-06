using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 매치가 유닛을 세우는 공용 관문(`CombatUnitSpawner`)의 계약 잠금.
	///
	/// ★ 왜 시험이 필요한가: 여기 담긴 것은 **조용히 어긋나는 트랩**이다. 자율 brain 을 안 끄면
	///   전술과 같은 이동 채널을 매 틱 다투어 유닛이 제자리를 맴돈다(트랩#2) — 로그도 예외도 안 뜨고
	///   화면에서 「왜 안 가지」로만 보인다. 실제로 투기장 초기에 이 병으로 한참을 헤맸고,
	///   개척의 다섯 경로가 각자 같은 세 줄을 되풀이하던 것도 그래서였다.
	///   한 곳으로 모은 지금, 그 한 곳이 일을 하는지는 기계가 지켜야 한다.
	///
	/// `Enlist`(Init→자동시전 차단→MatchCombatant 부여)는 `UnitObject`(추상 MonoBehaviour)와
	/// `Unit`(SO) 실물이 필요해 EditMode 단독으로 세우기 어렵다 → PlayMode 하네스(`Arm Auto-Verify`)
	/// 담당. 여기서는 순수하게 검증 가능한 `SilenceBrains` 만 잠근다. 못 하는 걸 하는 척하지 않는다.
	/// </summary>
	public class CombatUnitSpawnerTests
	{
		private sealed class FakeBrain : UnitBrain { }


		// ── 반납 복구 ─────────────────────────────────────────────────────────────
		//
		// ★ 왜 (2026-08-06 실측): 풀은 상태를 안 씻는다. 편입 때 끈 brain 을 그대로 반납하면
		//   **그 인스턴스가 다음에 던전에서 나올 때 안 움직인다.** 투기장 자기 주석이 팀 틴트에 대해
		//   「관전용 색이 본편으로 새는 것이다」라고 적어뒀는데, 거동은 색보다 나쁘고 눈에도 안 띈다.
		//   개척은 `TowerDefenseUnitLease` 스냅샷/복구로 이미 풀어놨고 투기장만 색까지만 되돌렸다.

		[Test]
		public void 끈_brain_만_기록한다()
		{
			GameObject unit = new GameObject(nameof(끈_brain_만_기록한다));
			try
			{
				FakeBrain wasOn = unit.AddComponent<FakeBrain>();
				FakeBrain wasOff = unit.AddComponent<FakeBrain>();
				wasOff.enabled = false;

				List<UnitBrain> silenced = new();
				CombatUnitSpawner.SilenceBrains(unit, silenced);

				Assert.AreEqual(1, silenced.Count, "원래 꺼져 있던 brain 까지 기록하면 반납 때 **없던 걸 켜게 된다**");
				Assert.AreSame(wasOn, silenced[0]);
				Assert.IsFalse(wasOn.enabled);
				Assert.IsFalse(wasOff.enabled);
			}
			finally { Object.DestroyImmediate(unit); }
		}

		[Test]
		public void RestoreBrains_가_끈_것만_되살린다()
		{
			GameObject unit = new GameObject(nameof(RestoreBrains_가_끈_것만_되살린다));
			try
			{
				FakeBrain wasOn = unit.AddComponent<FakeBrain>();
				FakeBrain wasOff = unit.AddComponent<FakeBrain>();
				wasOff.enabled = false;

				List<UnitBrain> silenced = new();
				CombatUnitSpawner.SilenceBrains(unit, silenced);
				CombatUnitSpawner.RestoreBrains(silenced);

				Assert.IsTrue(wasOn.enabled, "우리가 끈 brain 이 안 돌아왔다 — 풀로 돌아간 유닛이 안 움직인다");
				Assert.IsFalse(wasOff.enabled, "원래 꺼져 있던 brain 을 켰다 — 복구가 아니라 변조다");
			}
			finally { Object.DestroyImmediate(unit); }
		}

		[Test]
		public void 복구_함수들은_null_에_안_넘어진다()
		{
			Assert.DoesNotThrow(() => CombatUnitSpawner.RestoreBrains(null));
			Assert.DoesNotThrow(() => CombatUnitSpawner.RestoreAutoCast(null));
		}

		[Test]
		public void SilenceBrains_붙어있는_brain_을_전부_끈다()
		{
			GameObject unit = new GameObject(nameof(SilenceBrains_붙어있는_brain_을_전부_끈다));
			try
			{
				FakeBrain first = unit.AddComponent<FakeBrain>();
				FakeBrain second = unit.AddComponent<FakeBrain>();

				CombatUnitSpawner.SilenceBrains(unit);

				Assert.IsFalse(first.enabled, "하나만 끄면 남은 하나가 이동 채널을 계속 잡는다");
				Assert.IsFalse(second.enabled);
			}
			finally
			{
				Object.DestroyImmediate(unit);
			}
		}

		// 구체 타입을 세지 않고 마커 베이스로 훑는 것이 이 관문의 핵심이다 —
		// 새 brain 종류가 생겨도 손 안 대고 걸려야 한다(그래서 `UnitBrain` 마커가 있다).
		[Test]
		public void SilenceBrains_새로운_brain_종류도_자동으로_걸린다()
		{
			GameObject unit = new GameObject(nameof(SilenceBrains_새로운_brain_종류도_자동으로_걸린다));
			try
			{
				AnotherFakeBrain unknownKind = unit.AddComponent<AnotherFakeBrain>();

				CombatUnitSpawner.SilenceBrains(unit);

				Assert.IsFalse(unknownKind.enabled);
			}
			finally
			{
				Object.DestroyImmediate(unit);
			}
		}

		[Test]
		public void SilenceBrains_brain_이_없으면_아무_일도_안_한다()
		{
			GameObject unit = new GameObject(nameof(SilenceBrains_brain_이_없으면_아무_일도_안_한다));
			try
			{
				Assert.DoesNotThrow(() => CombatUnitSpawner.SilenceBrains(unit));
			}
			finally
			{
				Object.DestroyImmediate(unit);
			}
		}

		private sealed class AnotherFakeBrain : UnitBrain { }
	}
}
