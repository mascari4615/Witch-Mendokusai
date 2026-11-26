using UnityEngine;

namespace WitchMendokusai
{
	// 분노. 체력이 얼마 안 남으면 한 번 부풀며 붉어지고 빨라짐. 끝까지 그대로
	// 시키는 행동: 반쯤 깎았으면 단숨에 끝내기, 아니면 거리 벌리기
	[CreateAssetMenu(fileName = "AF_Enrage", menuName = "WM/Monster/Affix/Enrage")]
	public sealed class EnrageAffix : MonsterAffix
	{
		[SerializeField, Tooltip("남은 체력이 이 비율 아래로 떨어지면 화남"), Range(0f, 1f)] private float below = 0.5f;
		[SerializeField, Tooltip("화나면 이동 속도 배율"), Min(1f)] private float speedMultiplier = 1.6f;
		[SerializeField, Tooltip("화나면 몸 색에 곱함")] private Color angryTint = new(1f, 0.55f, 0.5f, 1f);
		[SerializeField, Tooltip("화날 때 몸 부풀림 배율"), Min(1f)] private float punch = 1.35f;
		[SerializeField, Tooltip("부풀림이 돌아오는 초"), Min(0.05f)] private float punchSeconds = 0.3f;

		public override string KindName => "분노";

		public static bool ShouldEnrage(int maxHealth, int currentHealth, float below) => maxHealth > 0 && currentHealth > 0 && currentHealth < maxHealth * below;

		// 기억의 IsOn 은 이미 화남
		public override void OnHurt(MonsterObject monster, AffixMemory memory, DamageInfo damageInfo)
		{
			if (memory.IsOn || ShouldEnrage(monster.UnitStat[UnitStatType.HP_MAX], monster.UnitStat[UnitStatType.HP_CUR], below) == false)
				return;
			memory.IsOn = true;
			monster.UnitStat[UnitStatType.MOVEMENT_SPEED] = Mathf.CeilToInt(monster.UnitStat[UnitStatType.MOVEMENT_SPEED] * speedMultiplier);
			monster.SpriteRenderer.color *= angryTint;
			monster.Health.PunchScale(punch, punchSeconds);
		}
	}
}
