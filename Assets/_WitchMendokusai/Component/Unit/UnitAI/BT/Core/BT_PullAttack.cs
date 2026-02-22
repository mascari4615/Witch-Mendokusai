using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_PullAttack : BTRunner
	{
		private float attackRange;

		private Vector3 moveDest = Vector3.zero;

		public BT_PullAttack(UnitObject unitObject, float attackRange = 5f) : base(unitObject)
		{
			this.attackRange = attackRange;
		}

		protected override Node MakeNode()
		{
			return
				Selector
				(
					// 근처에 있으면 Player향해서 Move
					Sequence
					(
						Condition(IsPlayerFar),

						Action(SetDestinationPlayer),
						Action(MoveToDestination),
						Action(UpdateSpriteFlip)
					),

					Sequence
					(
						Condition(IsSkill0Ready),
						Action(PullAttack)
					)
				);
		}

		private BTState SetDestinationPlayer()
		{
			moveDest = Player.Instance.transform.position;
			return BTState.Success;
		}

		protected bool IsPlayerFar()
		{
			float distance = Vector3.Distance(Player.Instance.transform.position, unitObject.transform.position);
			bool isPlayerFar = distance > attackRange;

			return isPlayerFar;
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
			unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
			return BTState.Success;
		}

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}
		
		protected bool IsSkill0Ready()
		{
			return unitObject.SkillHandler.SkillDic[0].IsReady;
		}

		protected BTState PullAttack()
		{
			unitObject.UseSkill(0);
			return BTState.Success;
		}
	}
}