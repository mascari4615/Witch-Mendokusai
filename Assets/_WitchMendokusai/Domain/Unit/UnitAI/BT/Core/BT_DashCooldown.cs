using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 일반 적 돌진 (DashMonsterAttack). 대기 중에는 추적 (성격이 있으면 성격대로 걷고, 성격이 막거나 사거리 밖이면 기다림), 준비되면 BT_Dash 한 번 (다가가기, 멈칫 예고, 돌진, 숨 고르기), 끝나면 다시 대기
	// 돌진 한 번이 끝날 때마다 BT_Dash 를 새로 만들어 행동 트리 상태가 이월되지 않게 함
	public class BT_DashCooldown : BTRunner, IAttackCycle
	{
		private readonly DashMonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private BT_Dash dash;
		private float readyTime;

		public int CompletedCount { get; private set; }
		public bool IsAttacking => dash != null && dash.HasStarted;

		public BT_DashCooldown(UnitObject unitObject, PlayerProvider playerProvider, DashMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			// 기절, 빙결, 대기 중과 성격이 기다리게 할 때는 준비를 시작하지 않음. 돌진 도중이면 BT_Dash 가 끝까지 책임
			if (dash == null && IsWaiting())
			{
				// 성격이 있으면 대기 걸음도 성격대로 (맴돌기, 낮잠 순찰 등). 없으면 곧장 추적
				if (MonsterTemperamentDriver.TryMove(unitObject, playerProvider))
					return BTState.Success;
				return chase.UpdateBT();
			}
			dash ??= attack.CreateDash(unitObject, playerProvider, OnDashEnd);
			return dash.UpdateBT();
		}

		private bool IsWaiting()
		{
			if (Time.time < readyTime || unitObject.IsActionSuspended)
				return true;
			// 성격이 없으면 전과 같이 BT_Dash 가 다가가 돌진
			if (MonsterTemperamentDriver.Has(unitObject) == false)
				return false;
			// 성격이 막는 동안 (잠듦, 앉음, 낮잠) 과 사거리 밖에서는 돌진을 시작하지 않고 성격대로 걸음
			if (MonsterTemperamentDriver.HoldsAttack(unitObject))
				return true;
			// BT_Dash.IsPlayerFar 와 같은 재기 (높이 포함). 다르면 돌진이 만들어진 뒤 곧장 다가감
			float distance = Vector3.Distance(playerProvider.Current.transform.position, unitObject.transform.position);
			return distance > attack.TriggerRange;
		}

		private void OnDashEnd()
		{
			dash = null;
			readyTime = Time.time + attack.Cooldown;
			CompletedCount++;
		}

		public void Interrupt()
		{
			dash?.Cancel();
			dash = null;
		}
	}
}
