using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	// 일반 적 돌진 (DashMonsterAttack). 대기 중에는 추적, 준비되면 BT_Dash 한 번 (다가가기, 멈칫 예고, 돌진, 숨 고르기), 끝나면 다시 대기
	// 돌진 한 번이 끝날 때마다 BT_Dash 를 새로 만들어 행동 트리 상태가 이월되지 않게 함
	public class BT_DashCooldown : BTRunner, IAttackCycle
	{
		private readonly DashMonsterAttack attack;
		private readonly BT_MoveToPlayer chase;
		private BT_Dash dash;
		private float readyTime;

		public int CompletedCount { get; private set; }
		public bool IsAttacking => dash != null;

		public BT_DashCooldown(UnitObject unitObject, PlayerProvider playerProvider, DashMonsterAttack attack) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.attack = attack;
			chase = new BT_MoveToPlayer(unitObject, playerProvider);
		}

		protected override Node MakeNode() => Action(Tick);

		private BTState Tick()
		{
			// 기절, 빙결 중에는 준비를 시작하지 않음. 돌진 도중이면 BT_Dash 가 끝까지 책임
			if (dash == null && (Time.time < readyTime || unitObject.IsActionSuspended))
				return chase.UpdateBT();
			dash ??= attack.CreateDash(unitObject, playerProvider, OnDashEnd);
			return dash.UpdateBT();
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
