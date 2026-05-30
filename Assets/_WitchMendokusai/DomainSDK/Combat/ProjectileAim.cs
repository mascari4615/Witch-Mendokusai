using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 발사체/스킬 조준 방향 해석 — 우선순위: 전술 타겟 > 레거시 폴백(플레이어 조준/호밍) > finalForward.
	/// 순수(Vector3 만, MonoBehaviour 0) → EditMode 테스트 가능. y평면 투영(아레나/탑다운 정합).
	/// WM-165: SkillContext.Target 소비(전술 타겟 지정 시전) — Current null 가드 포함.
	/// </summary>
	public static class ProjectileAim
	{
		private const float EPSILON_SQR = 1e-6f;

		public static Vector3 Resolve(Vector3 origin, Vector3? targetPosition, Vector3? fallbackAim, Vector3 finalForward)
		{
			if (targetPosition.HasValue)
			{
				Vector3 toTarget = targetPosition.Value - origin;
				toTarget.y = 0f;
				if (toTarget.sqrMagnitude > EPSILON_SQR)
					return toTarget.normalized;
			}

			if (fallbackAim.HasValue)
			{
				Vector3 aim = fallbackAim.Value;
				aim.y = 0f;
				if (aim.sqrMagnitude > EPSILON_SQR)
					return aim.normalized;
			}

			return finalForward;
		}
	}
}
