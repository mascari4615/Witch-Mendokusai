using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_Idle : BTRunner
	{
		private readonly float randomMoveDistance;
		private readonly bool usePivot;
		private readonly bool isSpriteLookLeft;

		private Vector3 pivot = Vector3.zero;
		private Vector3 moveDest = Vector3.zero;

		public BT_Idle(UnitObject unitObject, float randomMoveDistance = 10, bool usePivot = false, bool isSpriteLookLeft = true) : base(unitObject)
		{
			this.randomMoveDistance = randomMoveDistance;
			this.usePivot = usePivot;
			this.isSpriteLookLeft = isSpriteLookLeft;
		}

		protected override Node MakeNode()
		{
			return
				Sequence
				(
					Sequence // # [제자리]
					(
						Action(SetDestinationZero),
						Action(SetUnitMoveDestination),
						Action(UpdateSpriteFlip),
						Wait(3)
					),
					Sequence // # [랜덤 이동]
					(
						Action(SetDestinationRandom),
						Action(SetUnitMoveDestination),
						Action(UpdateSpriteFlip),
						Wait(3)
					)
				);
		}

		private BTState SetDestinationZero()
		{
			moveDest = unitObject.transform.position;
			return BTState.Success;
		}

		private BTState SetDestinationRandom()
		{
			Vector3 random = Random.insideUnitCircle * randomMoveDistance;
			random.z = random.y;
			random.y = 0;

			if (usePivot)
				moveDest = pivot + random;
			else
				moveDest = unitObject.transform.position + random;

			return BTState.Success;
		}

		private BTState SetUnitMoveDestination()
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

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}
	}
}