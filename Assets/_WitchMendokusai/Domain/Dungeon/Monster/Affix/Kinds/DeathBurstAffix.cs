using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace WitchMendokusai
{
	// 죽으면 터짐. 쓰러진 자리에 원이 차오르고, 차면 터져 원 안 주인공에게 피해. 몸은 이미 없고 예고 원이 시계
	// 시키는 행동: 잡은 뒤 비키기. 붙어서 잡을지 떨어져서 잡을지
	[CreateAssetMenu(fileName = "AF_DeathBurst", menuName = "WM/Monster/Affix/DeathBurst")]
	public sealed class DeathBurstAffix : MonsterAffix
	{
		[SerializeField, Tooltip("터지기까지 초. 피할 틈"), Min(0.1f)] private float delaySeconds = 1f;
		[SerializeField, Tooltip("터지는 반지름 (m)"), Min(0.3f)] private float radius = 2.2f;
		[SerializeField, Tooltip("기본 피해. 시간 상승과 위협 배율은 다른 적 피해와 같게"), Min(1)] private int damage = 5;
		[SerializeField, Tooltip("차오르는 원")] private CombatFx telegraph;
		[SerializeField, Tooltip("터지는 순간 그림")] private CombatFx strikeFx;

		public override string KindName => "죽으면 터짐";

		public static bool IsInside(Vector3 center, Vector3 target, float radius, float bodyRadius)
		{
			Vector3 offset = target - center;
			offset.y = 0f;
			float reach = radius + bodyRadius;
			return offset.sqrMagnitude <= reach * reach;
		}

		public override void OnDeath(MonsterObject monster, AffixMemory memory, Vector3 position)
		{
			CombatFxContext context = new() { Position = position, Radius = radius, Seconds = delaySeconds };
			if (telegraph != null)
				telegraph.Play(context);
			// 피해는 쓰러진 순간의 몬스터 수치로 미리 셈 (몸은 곧 풀로 돌아가 다른 몬스터가 됨)
			DamageInfo burst = MonsterAttack.MakeDamage(monster, damage);
			burst.damageSource = null;
			DOVirtual.DelayedCall(delaySeconds, () => Explode(position, burst), ignoreTimeScale: false);
		}

		private void Explode(Vector3 position, DamageInfo burst)
		{
			if (strikeFx != null)
				strikeFx.Play(new CombatFxContext { Position = position, Origin = position, Radius = radius, Seconds = 0f });
			PlayerObject player = CurrentPlayer;
			if (player == null || player.IsAlive == false)
				return;
			if (IsInside(position, player.transform.position, radius, player.UnitMovement.BodyRadius))
				player.ReceiveDamage(burst);
		}

		public override void Validate(Monster owner, List<string> errors)
		{
			if (telegraph == null)
				errors.Add($"{name}: 예고 원 ({nameof(telegraph)}) 없이 터지면 피할 수 없음");
		}
	}
}
