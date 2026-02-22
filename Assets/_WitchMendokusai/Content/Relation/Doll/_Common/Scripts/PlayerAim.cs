using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerAim
	{
		private const float MaxAimDistance = 100f;

		private GameObject lastAutoTarget;

		private readonly Transform playerTr;
		private readonly List<GameObject> targets;

		public PlayerAim(Transform transform, List<GameObject> targets)
		{
			playerTr = transform;
			this.targets = targets;
		}

		public GameObject GetNearestTarget()
		{
			bool TryItsNearest(GameObject target, float minDistance, out float distance)
			{
				int layerMask = LayerMask.GetMask("UNIT");
				Vector3 actualTargetPosition = target.transform.position + Vector3.up * 0.5f;

				distance = Vector3.Distance(playerTr.position, actualTargetPosition);
				Vector3 direction = actualTargetPosition - playerTr.position;

				if (distance >= minDistance)
					return false;

				// Debug.DrawRay(transform.position, direction, Color.red, 0.1f);
				if (Physics.Raycast(playerTr.position, direction, out RaycastHit hit, distance, layerMask) == false)
					return false;

				if (hit.collider.gameObject != target)
				{
					// Debug.Log($"Hit Object : {hit.collider.gameObject.name}, Target Object : {target.name}");
					return false;
				}

				return true;
			}

			if (targets.Count == 0)
				return null;

			GameObject nearestAutoTarget = null;
			float minDistance = MaxAimDistance;

			// 최적화 : 마지막 오토 타겟을 먼저 검사
			// 마지막 오토 타겟이 여전히 가장 가까울 확률이 높기 때문에
			// 미리 검사하면 minDistance를 처음부터 작게 시작할 수 있음
			if (lastAutoTarget != null)
			{
				if (lastAutoTarget.activeSelf == false)
					lastAutoTarget = null;
				else if (TryItsNearest(lastAutoTarget, minDistance, out float distance))
				{
					nearestAutoTarget = lastAutoTarget;
					minDistance = distance;
				}
			}

			// 그 다음 나머지 타겟들 검사
			foreach (GameObject target in targets)
			{
				if (TryItsNearest(target, minDistance, out float distance))
				{
					nearestAutoTarget = target;
					minDistance = distance;
				}
			}

			lastAutoTarget = nearestAutoTarget;
			return nearestAutoTarget;
		}

		public Vector3 CalcAim(bool useAutoAim)
		{
			GameObject nearestTarget = GetNearestTarget();
			return useAutoAim && (nearestTarget != null) ?
				nearestTarget.transform.position :
				InputManager.Instance.MouseWorldPosition;
		}

		public Vector3 CalcAimDirection(bool useAutoAim)
		{
			Vector3 targetPosition = CalcAim(useAutoAim);
			return (targetPosition - playerTr.position).normalized;
		}
	}
}