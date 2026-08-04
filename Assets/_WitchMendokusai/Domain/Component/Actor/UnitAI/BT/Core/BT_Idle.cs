using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_Idle : BTRunner
	{
		private readonly float randomMoveDistance;
		private readonly bool usePivot;
		private readonly bool isSpriteLookLeft;
		// 제자리에 머무는 시간과 한 번 어슬렁거린 뒤 쉬는 시간. 「살아있어 보이는」 리듬을 정하는 값.
		private readonly float idleWaitSeconds;
		private readonly float randomMoveWaitSeconds;

		private Vector3 pivot = Vector3.zero;
		private Vector3 moveDest = Vector3.zero;

		public BT_Idle(UnitObject unitObject, float randomMoveDistance = 10, bool usePivot = false, bool isSpriteLookLeft = true, float idleWaitSeconds = 3f, float randomMoveWaitSeconds = 3f) : base(unitObject)
		{
			this.randomMoveDistance = randomMoveDistance;
			this.usePivot = usePivot;
			this.isSpriteLookLeft = isSpriteLookLeft;
			this.idleWaitSeconds = idleWaitSeconds;
			this.randomMoveWaitSeconds = randomMoveWaitSeconds;
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
						Wait(idleWaitSeconds)
					),
					Sequence // # [랜덤 이동]
					(
						Action(SetDestinationRandom),
						Action(SetUnitMoveDestination),
						Action(UpdateSpriteFlip),
						Wait(randomMoveWaitSeconds)
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

	}
}