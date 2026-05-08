using System;
using UnityEngine;
using static WitchMendokusai.NodeHelper;

namespace WitchMendokusai
{
	public class BT_Skill : BTRunner
	{
		private readonly int skillIndex;
		private readonly float attackRange;
		private event Action OnSkillEnd;

		private Vector3 moveDest = Vector3.zero;

		public BT_Skill(UnitObject unitObject, int skillIndex, float attackRange, Action onSkillEnd = null) : base(unitObject)
		{
			this.skillIndex = skillIndex;
			this.attackRange = attackRange;
			OnSkillEnd = onSkillEnd;
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
						Condition(IsSkillReady),
						Action(UseSkill),
						Action(() =>
						{
							OnSkillEnd?.Invoke();
							return BTState.Success;
						})
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

		protected bool IsSkillReady()
		{
			if (unitObject.SkillHandler.SkillDic.ContainsKey(skillIndex) == false)
				return false;
			return unitObject.SkillHandler.SkillDic[skillIndex].IsReady;
		}

		protected BTState UseSkill()
		{
			return unitObject.UseSkill(skillIndex) ? BTState.Success : BTState.Failure;
		}
	}
}