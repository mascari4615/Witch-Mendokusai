using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.NodeHelper;
using UnityEngine.AI;

namespace WitchMendokusai
{
	public class BT_Idle : BTRunner
	{
		private float randomMoveDistance;
		private bool usePivot;
		private bool isSpriteLookLeft;
		
		private Vector3 pivot = Vector3.zero;
		private Vector3 moveDest = Vector3.zero;

		public BT_Idle(UnitObject unitObject) : base(unitObject)
		{
		}

		public void Init(float randomMoveDistance = 10, bool usePivot = false, bool isSpriteLookLeft = true)
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
					Action(UpdateSpriteFlip),
					Wait(3),
					Action(SetDestinationRandom),
					Action(MoveToDestination)
				);
		}

		private void SetDestinationRandom()
		{
			Vector3 random = UnityEngine.Random.insideUnitCircle * randomMoveDistance;
			random.z = random.y;
			random.y = 0;

			if (usePivot)
				moveDest = pivot + random;
			else
				moveDest = unitObject.transform.position + random;
		}

		private void MoveToDestination()
		{
			NavMeshAgent agent = unitObject.NavMeshAgent;

			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
			agent.destination = unitObject.transform.position + dir;
		}

		private void UpdateSpriteFlip()
		{
			unitObject.SpriteRenderer.flipX = isSpriteLookLeft ? !IsPlayerOnLeft() : IsPlayerOnLeft();
		}

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}
	}
}