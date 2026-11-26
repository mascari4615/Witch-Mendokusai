using UnityEngine;

namespace WitchMendokusai
{
	// 성격이 드라이버에게 세어 달라는 주변 사실. 켠 것만 TemperamentContext 에 채워짐
	[System.Flags]
	public enum TemperamentSense
	{
		None = 0,
		Kin = 1 << 0,
		Ally = 1 << 1,
		Loot = 1 << 2,
		Fallen = 1 << 3,
	}

	// 몬스터의 걷는 성격. 공격이 쉬는 동안 어디로 어떻게 움직일지만 정함 (공격 박자는 MonsterAttack)
	// 몬스터 자산 (Monster.Temperament) 에 끼우면 곧장 쫓기 대신 이것을 따름. 비우면 지금처럼 곧장 쫓음
	// 여러 유닛이 나눠 쓰므로 자산에는 상태를 두지 않음. 유닛마다의 기억은 TemperamentMemory
	// 계산은 위치와 시간만 받는 순수 함수라 EditMode 시험에서 그대로 돌림. 정본 memo wm/design/monster-temperament-20261126
	public abstract class MonsterTemperament : ScriptableObject
	{
		/// <summary>도감과 개발 창에 보이는 성격 이름 (임시 표기)</summary>
		public abstract string KindName { get; }

		[field: SerializeField, Tooltip("몸 부풀림 (놀람, 도움닫기) 이 돌아오는 초"), Min(0.05f)] public float PunchSeconds { get; private set; } = 0.25f;
		[field: SerializeField, Tooltip("카메라 옆 방향 성분이 이보다 작으면 (거의 위아래로 걸음) 좌우를 안 바꿈. 떨림 방지"), Range(0f, 1f)] public float FaceDeadZone { get; private set; } = 0.05f;

		[field: SerializeField, Tooltip("감각 (같은 성격, 다른 몬스터, 전리품, 처치) 을 세는 반지름 (m). 감각을 쓰는 성격만"), Min(0f)] public float SenseRadius { get; private set; } = 8f;
		[field: SerializeField, Tooltip("감각을 다시 세는 초. 짧을수록 비쌈 (몬스터 수만큼 거리 계산)"), Min(0.02f)] public float SenseSeconds { get; private set; } = 0.25f;

		/// <summary>이 성격이 쓰는 감각. 없으면 드라이버가 목록을 훑지 않음</summary>
		public virtual TemperamentSense Senses => TemperamentSense.None;

		/// <summary>순간이동 때 떠난 자리와 닿은 자리에 낼 먼지 고리. 순간이동하는 성격만</summary>
		public virtual MonsterSpawnStyle TeleportPuff => null;

		public abstract TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory);
	}
}
