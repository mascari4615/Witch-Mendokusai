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
