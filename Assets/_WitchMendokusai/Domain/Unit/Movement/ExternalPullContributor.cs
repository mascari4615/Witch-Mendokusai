using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 끌어당김 같은 외부 수평 속도를 입력 위에 더하는 채널. 입력을 덮지 않으므로 반대로 걸으면 벗어남
	/// - Before 는 Input 앞, After 는 Input 뒤에 등록. Before 가 직전 tick 에 더한 몫을 빼서 Input 가속 계산이 누적되지 않게 함
	/// - 돌진, 넉백 (IsExternallyDriven), 이동 막힘, 사망 중에는 더하지 않음
	/// - Set 은 유지 시간을 받음. 거는 쪽이 갱신을 멈추면 그 시간 뒤 저절로 풀림 (보스가 사라져도 끌림이 남지 않게)
	/// </summary>
	public class ExternalPullContributor
	{
		private readonly UnitObject unitObject;
		private Vector3 pullVelocity;
		private float remainingTime;
		private Vector3 applied;

		public IVelocityContributor Before { get; }
		public IVelocityContributor After { get; }
		public bool IsActive => remainingTime > 0f;
		public Vector3 Velocity => IsActive ? pullVelocity : Vector3.zero;

		public ExternalPullContributor(UnitObject unitObject)
		{
			this.unitObject = unitObject;
			Before = new RemoveStep(this);
			After = new AddStep(this);
		}

		public void Set(Vector3 worldHorizontalVelocity, float duration)
		{
			pullVelocity = new Vector3(worldHorizontalVelocity.x, 0f, worldHorizontalVelocity.z);
			remainingTime = Mathf.Max(0f, duration);
		}

		public void Clear()
		{
			remainingTime = 0f;
			pullVelocity = Vector3.zero;
			applied = Vector3.zero;
		}

		/// <summary> 다른 경로가 속도를 통째로 정한 tick 뒤. 직전 몫을 빼지 않게 비움 </summary>
		public void ForgetApplied() => applied = Vector3.zero;

		private void Remove(MotorContext context)
		{
			// 외부 밀림이 이미 수평 속도를 덮었으면 빼지 않음
			if (context.IsExternallyDriven == false)
			{
				context.Velocity.x -= applied.x;
				context.Velocity.z -= applied.z;
			}
			applied = Vector3.zero;
		}

		private void Add(MotorContext context, float deltaTime)
		{
			if (remainingTime <= 0f)
				return;
			remainingTime -= deltaTime;
			if (context.IsExternallyDriven || context.BlockedByExternal || unitObject.UnitStat[UnitStatType.DEAD] > 0)
				return;
			context.Velocity.x += pullVelocity.x;
			context.Velocity.z += pullVelocity.z;
			applied = pullVelocity;
		}

		private sealed class RemoveStep : IVelocityContributor
		{
			private readonly ExternalPullContributor owner;
			public RemoveStep(ExternalPullContributor owner) => this.owner = owner;
			public void Contribute(MotorContext context, float deltaTime) => owner.Remove(context);
		}

		private sealed class AddStep : IVelocityContributor
		{
			private readonly ExternalPullContributor owner;
			public AddStep(ExternalPullContributor owner) => this.owner = owner;
			public void Contribute(MotorContext context, float deltaTime) => owner.Add(context, deltaTime);
		}
	}
}
