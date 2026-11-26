using UnityEngine;

namespace WitchMendokusai
{
	// 엄폐 판정. 시전자와 주인공 사이에 콜라이더가 있으면 막힘. 때리는 순간 한 번만 부름 (매 프레임 아님)
	// 층은 공격 자산이 정함 (맵 물체와 지형은 Default, 유닛은 UNIT). 시전자와 주인공 자신의 콜라이더는 건너뜀
	public static class MonsterCover
	{
		// 한 선에 걸리는 콜라이더 상한. 넘으면 늘림
		private static RaycastHit[] hits = new RaycastHit[8];

		public static bool Blocks(Vector3 from, Vector3 to, float height, LayerMask mask, Transform caster, Transform target)
		{
			if (mask.value == 0)
				return false;
			from.y += height;
			to.y += height;
			Vector3 offset = to - from;
			if (offset.sqrMagnitude < SlimeKingPatternMath.DIRECTION_EPSILON_SQR)
				return false;
			float distance = offset.magnitude;
			int count;
			while ((count = Physics.RaycastNonAlloc(from, offset / distance, hits, distance, mask, QueryTriggerInteraction.Ignore)) == hits.Length)
				hits = new RaycastHit[hits.Length * 2];
			for (int i = 0; i < count; i++)
			{
				Transform hit = hits[i].collider.transform;
				if (IsPartOf(hit, caster) || IsPartOf(hit, target))
					continue;
				return true;
			}
			return false;
		}

		private static bool IsPartOf(Transform hit, Transform owner) => owner != null && hit.IsChildOf(owner);
	}
}
