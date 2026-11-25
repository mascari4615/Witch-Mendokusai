using UnityEngine;

namespace WitchMendokusai
{
	// 몬스터의 걷는 성격. 공격이 쉬는 동안 어디로 어떻게 움직일지만 정함 (공격 박자는 MonsterAttack)
	// 몬스터 자산 (Monster.Temperament) 에 끼우면 곧장 쫓기 대신 이것을 따름. 비우면 지금처럼 곧장 쫓음
	// 여러 유닛이 나눠 쓰므로 자산에는 상태를 두지 않음. 유닛마다의 기억은 TemperamentMemory
	// 계산은 위치와 시간만 받는 순수 함수라 EditMode 시험에서 그대로 돌림. 정본 memo wm/design/monster-temperament-20261126
	public abstract class MonsterTemperament : ScriptableObject
	{
		/// <summary>도감과 개발 창에 보이는 성격 이름 (임시 표기)</summary>
		public abstract string KindName { get; }

		[field: SerializeField, Tooltip("몸 부풀림 (놀람, 도움닫기) 이 돌아오는 초"), Min(0.05f)] public float PunchSeconds { get; private set; } = 0.25f;

		public abstract TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory);
	}
}
