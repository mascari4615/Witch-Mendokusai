using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	/// <summary>
	/// 마계 야수 도주 behavior (TASK-WM-182). <see cref="BT_MoveToPlayer"/> 의 정반대 —
	/// 플레이어로부터 멀어지는 방향으로 이동한다. 약한 야수가 사냥꾼을 피해 달아나는 거동.
	/// </summary>
	public class BT_Flee : BTRunner
	{
		private Vector3 moveDest = Vector3.zero;
		private readonly bool isSpriteLookLeft;

		public BT_Flee(UnitObject unitObject, PlayerProvider playerProvider, bool isSpriteLookLeft = true) : base(unitObject)
		{
			this.playerProvider = playerProvider;
			this.isSpriteLookLeft = isSpriteLookLeft;
		}

		protected override Node MakeNode()
		{
			return
				Selector
				(
					Sequence
					(
						Action(SetFleeDestination),
						Action(MoveToDestination),
						Action(UpdateSpriteFlip)
					)
				);
		}

		private BTState SetFleeDestination()
		{
			Vector3 fleeDirection = FleeDirection(unitObject.transform.position, playerProvider.Current.transform.position);
			moveDest = unitObject.transform.position + fleeDirection;
			return BTState.Success;
		}

		private BTState MoveToDestination()
		{
			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
			unitObject.UnitMovement.SetMoveDirection(dir);
			return BTState.Success;
		}

		private BTState UpdateSpriteFlip()
		{
			// 도주 = 추격의 역. BT_MoveToPlayer 와 반대로 플레이어 반대쪽을 향한다.
			unitObject.SpriteRenderer.flipX = isSpriteLookLeft ? IsPlayerOnLeft() : IsPlayerOnLeft() == false;
			return BTState.Success;
		}

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}

		/// <summary>
		/// 도주 방향 = 플레이어로부터 멀어지는 단위 벡터. <see cref="BT_MoveToPlayer"/> 의 추격 방향
		/// <c>(player - self)</c> 의 정반대. 순수 함수 — EditMode 단위검증 seam (MonoBehaviour 의존 0).
		/// </summary>
		public static Vector3 FleeDirection(Vector3 selfPosition, Vector3 playerPosition)
		{
			return (selfPosition - playerPosition).normalized;
		}
	}
}
