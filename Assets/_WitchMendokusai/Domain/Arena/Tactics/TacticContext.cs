using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 평가 1회에 필요한 읽기 표면(Self/Targeting/IsSkillReady) + 행동 표면(Actuator).
	/// 전부 인터페이스/델리게이트라 MonoBehaviour 없이 EditMode 테스트 가능(TacticBTRunner 와 함께).
	/// </summary>
	public class TacticContext
	{
		public ICombatant Self { get; }
		public ITargetResolver Targeting { get; }
		public ITacticActuator Actuator { get; }
		// 슬롯 스킬 준비 여부(SkillReady 조건). 실제 = SkillHandler, 테스트 = 람다.
		public Func<int, bool> IsSkillReady { get; }

		public TacticContext(ICombatant self, ITargetResolver targeting, ITacticActuator actuator, Func<int, bool> isSkillReady)
		{
			Self = self;
			Targeting = targeting;
			Actuator = actuator;
			IsSkillReady = isSkillReady;
		}
	}
}
