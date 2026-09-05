using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_MoveToPlayer : BTRunner
	{
		private Vector3 moveDest = Vector3.zero;
		private bool isSpriteLookLeft;

		public BT_MoveToPlayer(UnitObject unitObject, PlayerProvider playerProvider, bool isSpriteLookLeft = true) : base(unitObject)
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
						Action(SetDestinationPlayer),
						Action(MoveToDestination),
						Action(UpdateSpriteFlip)
					)
				);
		}

		private BTState SetDestinationPlayer()
		{
			moveDest = playerProvider.Current.transform.position;
			return BTState.Success;
		}

		private BTState MoveToDestination()
		{
			// NavMeshAgent agent = unitObject.NavMeshAgent;

			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
			// agent.destination = unitObject.transform.position + dir;

			unitObject.UnitMovement.SetMoveDirection(dir);
			return BTState.Success;
		}

		private BTState UpdateSpriteFlip()
		{
			unitObject.SpriteRenderer.flipX = isSpriteLookLeft ? !IsPlayerOnLeft() : IsPlayerOnLeft();
			return BTState.Success;
		}

	}
}