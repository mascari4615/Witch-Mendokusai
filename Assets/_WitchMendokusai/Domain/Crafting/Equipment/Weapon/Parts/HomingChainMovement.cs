using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 적에서 적으로 추적하는 투사체 이동. ChainOnHit 이 즉시 피해를 건네는 것과 달리 탄이 실제로 날아가 다음 적에 맞음
	// 맞히면 잠깐 멈춘 뒤, 아직 안 맞힌 가장 가까운 적으로 방향을 바꿈. 맞힐 수 있는 횟수와 다음 적 반지름은 직렬화 값
	// 피해는 같은 오브젝트의 DamagingObject 가 줌 (useHitCount 는 끄고 횟수는 여기서 셈)
	public class HomingChainMovement : SkillComponent, IShotReceiver, IHitEffect
	{
		[SerializeField, Min(0f)] private float moveSpeed = 14f;
		[SerializeField, Min(0f), Tooltip("처음 표적을 찾는 반지름")] private float acquireRange = 14f;
		[SerializeField, Min(0f), Tooltip("맞힌 뒤 다음 적을 찾는 반지름")] private float retargetRange = 5f;
		[SerializeField, Min(0f), Tooltip("맞힌 뒤 멈추는 시간")] private float hitPauseSeconds = 0.15f;
		[SerializeField, Min(1), Tooltip("맞힐 수 있는 적의 수. 다 맞히거나 다음 적이 없으면 사라짐")] private int maxHits = 4;

		private readonly List<GameObject> visited = new();
		private Vector3 direction = Vector3.forward;
		private Transform target;
		private float pauseRemaining;
		private int hits;

		public override void InitContext(SkillObject skillObject)
		{
			visited.Clear();
			target = null;
			pauseRemaining = 0f;
			hits = 0;
			direction = transform.forward;
			direction.y = 0f;
		}

		public void OnShot(ShotInfo shot)
		{
			if (shot.Direction.sqrMagnitude > 0.0001f)
				direction = shot.Direction.normalized;
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			visited.Add(victim.gameObject);
			hits++;
			target = null;
			if (hits >= maxHits)
			{
				gameObject.SetActive(false);
				return;
			}
			pauseRemaining = hitPauseSeconds;
		}

		private void Update()
		{
			if (pauseRemaining > 0f)
			{
				pauseRemaining -= Time.deltaTime;
				return;
			}

			if (target == null || target.gameObject.activeInHierarchy == false)
			{
				target = SurvivalTargeting.Nearest(transform.position, hits == 0 ? acquireRange : retargetRange, visited);
				// 한 번 맞힌 뒤 이어갈 적이 없으면 끝. 아직 못 맞혔으면 처음 방향으로 직진
				if (target == null && hits > 0)
				{
					gameObject.SetActive(false);
					return;
				}
			}

			if (target != null)
			{
				Vector3 toTarget = target.position - transform.position;
				toTarget.y = 0f;
				if (toTarget.sqrMagnitude > 0.0001f)
					direction = toTarget.normalized;
			}
			transform.position += moveSpeed * Time.deltaTime * direction;
			transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
		}
	}
}
