using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 분열. 쓰러지면 작은 몬스터 여럿으로 갈라짐. 갈라진 것은 다른 몬스터 자산 (분열 안 하는 것) 이라 끝이 있음
	// 시키는 행동: 넓게 맞히는 공격으로 마무리, 갈라질 자리를 생각하고 잡기
	[CreateAssetMenu(fileName = "AF_Split", menuName = "WM/Monster/Affix/Split")]
	public sealed class SplitAffix : MonsterAffix
	{
		[SerializeField, Tooltip("갈라져 나오는 몬스터. 분열 특징이 없는 것")] private Monster child;
		[SerializeField, Tooltip("갈라지는 수"), Min(1)] private int count = 2;
		[SerializeField, Tooltip("쓰러진 자리에서 퍼지는 거리 (m)"), Min(0f)] private float spread = 1.1f;

		public override string KindName => "분열";

		// 쓰러진 자리 둘레에 고르게. 첫 자리는 주인공 반대쪽이 아니라 오른쪽부터 (재현 가능)
		public static Vector3 SpotAround(Vector3 center, int index, int total, float distance)
		{
			float angle = (index / (float)Mathf.Max(1, total)) * Mathf.PI * 2f;
			return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
		}

		public override void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position)
		{
			if (child == null)
				return;
			for (int i = 0; i < count; i++)
				SummonMonsterAttack.SpawnMonster(child, SpotAround(position, i, count, spread));
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (owner.Type == MonsterType.Boss)
				errors.Add($"{name}: 보스는 분열하지 않음 (처치 사건과 같은 순간 새 몬스터가 생김)");
			if (child == null)
				errors.Add($"{name}: 갈라져 나올 몬스터 ({nameof(child)}) 비어 있음");
			else if (child == owner)
				errors.Add($"{name}: 자기 자신으로 갈라지면 끝이 없음");
			else if (child.Affixes != null)
			{
				foreach (MonsterAffix affix in child.Affixes)
				{
					if (affix is SplitAffix)
						errors.Add($"{name}: 갈라져 나온 {child.name} 도 분열함. 끝이 없을 수 있음");
				}
			}
		}
	}
}
