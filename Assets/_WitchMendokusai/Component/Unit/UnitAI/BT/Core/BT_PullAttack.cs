using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_PullAttack : BTRunner
	{
		private float attackRange;

		private Vector3 moveDest = Vector3.zero;

		public BT_PullAttack(UnitObject unitObject) : base(unitObject)
		{
		}

		public void Init(float attackRange = 5f)
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
						Action(UseSkill0)
					)
				);
		}

		private void SetDestinationPlayer()
		{
			moveDest = Player.Instance.transform.position;
		}

		protected bool IsPlayerFar()
		{
			float distance = Vector3.Distance(Player.Instance.transform.position, unitObject.transform.position);
			bool isPlayerFar = distance > attackRange;

			return isPlayerFar;
		}

		private void MoveToDestination()
		{
			// NavMeshAgent agent = unitObject.NavMeshAgent;

			Vector3 dir = (moveDest - unitObject.transform.position).normalized;
			// agent.destination = unitObject.transform.position + dir;

			unitObject.UnitMovement.SetMoveDirection(dir);
		}

		private void UpdateSpriteFlip()
		{
			unitObject.SpriteRenderer.flipX = IsPlayerOnLeft();
		}

		protected bool IsPlayerOnLeft()
		{
			return Camera.main.WorldToViewportPoint(unitObject.transform.position).x > .5f;
		}
		
		protected bool IsSkill0Ready()
		{
			return unitObject.SkillHandler.SkillDic[0].IsReady;
		}

		protected void UseSkill0()
		{
			unitObject.UseSkill(0);
		}
	}
}