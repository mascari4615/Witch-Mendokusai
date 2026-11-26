using UnityEngine;

namespace WitchMendokusai
{
	// 켜져 있는 동안 쓴 유닛의 자리를 따라감. 쓰는 곳: 알리사 회전 베기 (손에 쥔 수리검)
	public class FollowUser : SkillComponent
	{
		private Transform user;

		public override void InitContext(SkillObject skillObject) => user = skillObject.Context.User.transform;

		private void LateUpdate()
		{
			if (user != null)
				transform.position = user.position;
		}
	}
}
