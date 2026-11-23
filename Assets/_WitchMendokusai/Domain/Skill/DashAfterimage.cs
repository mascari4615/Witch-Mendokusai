using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	// 대시 잔상. 지나간 자리에 몸 그림 복사본을 몇 장 남기고 금방 녹임 (사망 잔상과 같은 부품, 다른 겉모습 에셋)
	public static class DashAfterimage
	{
		public static void Play(SpriteRenderer body, MonsterDeathStyle style, int count, float interval, Color tint)
		{
			CombatFxRunner.Instance.Run(Emit(body, style, count, interval, tint));
		}

		private static IEnumerator Emit(SpriteRenderer body, MonsterDeathStyle style, int count, float interval, Color tint)
		{
			for (int i = 0; i < count; i++)
			{
				if (body == null || body.isVisible == false)
					yield break;
				MonsterDeathGhostPose pose = MonsterDeathGhost.Capture(body);
				pose.Color *= tint;
				MonsterDeathGhost.Play(pose, style);
				yield return new WaitForSeconds(interval);
			}
		}
	}
}
