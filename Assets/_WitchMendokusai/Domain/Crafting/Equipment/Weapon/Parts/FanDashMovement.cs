using UnityEngine;

namespace WitchMendokusai
{
	// 퍼졌다 모여 돌진하는 투사체 이동
	// 1 퍼짐: 조준 반대쪽으로 부채꼴. 발 순번마다 각도가 다름
	// 2 겨눔: 멈춰서 표적 쪽으로 돎
	// 3 돌진: 겨눈 표적의 마지막 자리로 빠르게. 지나쳐도 수명이 끝날 때까지 직진
	public class FanDashMovement : SkillComponent, IShotReceiver
	{
		private const float MIN_DIRECTION_SQUARED = 0.0001f;

		private enum Phase
		{
			Spread = 0,
			Aim = 1,
			Dash = 2,
		}

		[SerializeField, Min(0f)] private float spreadSpeed = 10f;
		[SerializeField, Min(0f)] private float spreadSeconds = 0.3f;
		[SerializeField, Min(0f)] private float aimSeconds = 0.2f;
		[SerializeField, Min(0f)] private float dashSpeed = 24f;
		[SerializeField, Min(0f), Tooltip("발 하나당 벌어지는 각도")] private float fanDegreesPerShot = 30f;
		[SerializeField, Min(0f), Tooltip("부채꼴 전체 상한 각도")] private float maxFanDegrees = 180f;
		[SerializeField, Min(0f), Tooltip("표적을 찾는 반지름")] private float targetRange = 14f;
		[SerializeField, Tooltip("퍼짐 방향의 중심. 조준 방향에서 이만큼 돌린 쪽 (180 이면 반대쪽)")] private float spreadCenterDegrees = 180f;

		private Phase phase;
		private float phaseTime;
		private Vector3 aimDirection = Vector3.forward;
		private Vector3 moveDirection = Vector3.forward;
		private Vector3 dashDirection = Vector3.forward;
		private Vector3 targetPosition;
		private Transform target;
		private int shotIndex;
		private int shotCount = 1;

		public override void InitContext(SkillObject skillObject)
		{
			shotIndex = 0;
			shotCount = 1;
			aimDirection = transform.forward;
			aimDirection.y = 0f;
			BeginSpread();
		}

		public void OnShot(ShotInfo shot)
		{
			shotIndex = shot.Index;
			shotCount = shot.Count;
			if (shot.Direction.sqrMagnitude > MIN_DIRECTION_SQUARED)
				aimDirection = shot.Direction;
			BeginSpread();
		}

		// 부채꼴 안에서 이 발이 서는 각도. 가운데가 0, 조준 반대쪽을 기준으로 좌우로
		public static float FanAngle(int index, int count, float degreesPerShot, float maxDegrees)
		{
			if (count <= 1)
				return 0f;
			float step = Mathf.Min(degreesPerShot, maxDegrees / (count - 1));
			return (index - (count - 1) * 0.5f) * step;
		}

		private void BeginSpread()
		{
			phase = Phase.Spread;
			phaseTime = 0f;
			target = null;
			float angle = FanAngle(shotIndex, shotCount, fanDegreesPerShot, maxFanDegrees);
			moveDirection = Quaternion.Euler(0f, spreadCenterDegrees + angle, 0f) * aimDirection;
			Face(moveDirection);
		}

		private void Update()
		{
			phaseTime += Time.deltaTime;
			switch (phase)
			{
				case Phase.Spread:
					transform.position += spreadSpeed * Time.deltaTime * moveDirection;
					if (phaseTime >= spreadSeconds)
						BeginAim();
					break;
				case Phase.Aim:
					TurnToTarget();
					if (phaseTime >= aimSeconds)
						BeginDash();
					break;
				default:
					transform.position += dashSpeed * Time.deltaTime * dashDirection;
					break;
			}
		}

		private void BeginAim()
		{
			phase = Phase.Aim;
			phaseTime = 0f;
			target = SurvivalTargeting.Nearest(transform.position, targetRange, null);
			targetPosition = target != null ? target.position : transform.position;
		}

		private void TurnToTarget()
		{
			if (target == null || target.gameObject.activeInHierarchy == false)
			{
				target = null;
				return;
			}
			targetPosition = target.position;
			Vector3 toTarget = targetPosition - transform.position;
			toTarget.y = 0f;
			if (toTarget.sqrMagnitude > MIN_DIRECTION_SQUARED)
				Face(toTarget);
		}

		private void BeginDash()
		{
			TurnToTarget();
			phase = Phase.Dash;
			phaseTime = 0f;
			Vector3 toTarget = targetPosition - transform.position;
			toTarget.y = 0f;
			// 표적이 없으면 처음 조준 방향으로
			dashDirection = target != null && toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : aimDirection.normalized;
			Face(dashDirection);
		}

		private void Face(Vector3 direction)
		{
			direction.y = 0f;
			if (direction.sqrMagnitude > MIN_DIRECTION_SQUARED)
				transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}
	}
}
