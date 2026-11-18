using UnityEngine;

namespace WitchMendokusai
{
	// 패턴이 정한 이동. 수명 동안 몬스터의 자체 판단 대신 적용
	public abstract class WaveMoveOrder
	{
		// 0 이하면 수명 없음
		public float Lifetime { get; }
		// 수명 끝에 드롭 없이 사라짐. false 면 자체 판단으로 돌아감
		public bool DespawnOnEnd { get; }

		protected WaveMoveOrder(float lifetime, bool despawnOnEnd)
		{
			Lifetime = lifetime;
			DespawnOnEnd = despawnOnEnd;
		}

		public abstract Vector3 Direction(Vector3 position, float ageSeconds);
	}

	// 고정 방향 직진
	public class StraightMoveOrder : WaveMoveOrder
	{
		private readonly Vector3 direction;

		public StraightMoveOrder(Vector3 direction, float lifetime, bool despawnOnEnd) : base(lifetime, despawnOnEnd)
		{
			direction.y = 0f;
			this.direction = direction.normalized;
		}

		public override Vector3 Direction(Vector3 position, float ageSeconds) => direction;
	}

	// 타원 위 자기 자리를 지키며 반지름을 줄임
	public class EllipseSlotMoveOrder : WaveMoveOrder
	{
		private readonly Transform followCenter;
		private readonly Vector3 fixedCenter;
		private readonly Vector3 slotOffset;
		private readonly float shrinkPerSecond;
		private readonly float minScale;
		private readonly float arriveDistance;

		public EllipseSlotMoveOrder(Transform followCenter, Vector3 fixedCenter, Vector3 slotOffset, float shrinkPerSecond, float minScale, float arriveDistance, float lifetime, bool despawnOnEnd)
			: base(lifetime, despawnOnEnd)
		{
			this.followCenter = followCenter;
			this.fixedCenter = fixedCenter;
			this.slotOffset = slotOffset;
			this.shrinkPerSecond = shrinkPerSecond;
			this.minScale = minScale;
			this.arriveDistance = arriveDistance;
		}

		public override Vector3 Direction(Vector3 position, float ageSeconds)
		{
			Vector3 center = followCenter != null ? followCenter.position : fixedCenter;
			float scale = WavePatternRules.ContractedScale(ageSeconds, shrinkPerSecond, minScale);
			Vector3 toSlot = center + slotOffset * scale - position;
			toSlot.y = 0f;
			return toSlot.sqrMagnitude <= arriveDistance * arriveDistance ? Vector3.zero : toSlot.normalized;
		}
	}
}
