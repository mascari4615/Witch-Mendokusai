using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 대시 잔상. 지나간 자리에 몸 그림 복사본을 몇 장 남기고 금방 녹임 (사망 잔상과 같은 부품, 다른 겉모습 에셋)
	// 플레이어는 머리, 몸, 손이 따로 그려지고 대표 SpriteRenderer 는 꺼져 있음. 그래서 MeshParent 아래 보이는 조각을 전부 복사
	public static class DashAfterimage
	{
		private static readonly List<SpriteRenderer> parts = new();

		public static void Play(UnitObject unit, MonsterDeathStyle style, int count, float interval, Color tint)
		{
			CombatFxRunner.Instance.Run(Emit(unit, style, count, interval, tint));
		}

		private static IEnumerator Emit(UnitObject unit, MonsterDeathStyle style, int count, float interval, Color tint)
		{
			for (int i = 0; i < count; i++)
			{
				if (unit == null || CollectParts(unit) == 0)
					yield break;
				foreach (SpriteRenderer part in parts)
				{
					MonsterDeathGhostPose pose = MonsterDeathGhost.Capture(part);
					pose.Color *= tint;
					MonsterDeathGhost.Play(pose, style);
				}
				yield return new WaitForSeconds(interval);
			}
		}

		// 보이는 조각만. 조각 그림이 없는 유닛은 대표 SpriteRenderer 하나
		private static int CollectParts(UnitObject unit)
		{
			parts.Clear();
			if (unit.MeshParent != null)
			{
				unit.MeshParent.GetComponentsInChildren(false, parts);
				parts.RemoveAll(part => part.enabled == false || part.sprite == null || part.isVisible == false);
			}
			if (parts.Count == 0 && unit.SpriteRenderer != null && unit.SpriteRenderer.enabled && unit.SpriteRenderer.isVisible)
				parts.Add(unit.SpriteRenderer);
			return parts.Count;
		}
	}
}
