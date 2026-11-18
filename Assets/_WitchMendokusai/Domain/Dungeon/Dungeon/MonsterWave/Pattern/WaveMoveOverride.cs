using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 패턴 이동 동안 자체 판단 (UnitBrain) 을 끄고 이동 명령을 직접 냄. 풀로 돌아가면 원상 복구
	[RequireComponent(typeof(UnitObject))]
	public class WaveMoveOverride : MonoBehaviour
	{
		private UnitObject unitObject;
		// 이 컴포넌트가 끈 것만. 원래 꺼져 있던 것은 복구 대상 아님
		private readonly List<UnitBrain> silencedBrains = new();
		private WaveMoveOrder order;
		private float ageSeconds;

		public static void Apply(GameObject monsterObject, WaveMoveOrder moveOrder)
		{
			if (monsterObject.TryGetComponent(out WaveMoveOverride moveOverride) == false)
				moveOverride = monsterObject.AddComponent<WaveMoveOverride>();
			moveOverride.Begin(moveOrder);
		}

		private void Begin(WaveMoveOrder moveOrder)
		{
			unitObject = GetComponent<UnitObject>();
			if (order == null)
			{
				silencedBrains.Clear();
				CombatUnitSpawner.SilenceBrains(gameObject, silencedBrains);
			}
			order = moveOrder;
			ageSeconds = 0f;
			enabled = true;
			unitObject.UnitMovement.SetMoveDirection(order.Direction(transform.position, 0f));
		}

		private void Update()
		{
			if (order == null)
			{
				enabled = false;
				return;
			}

			ageSeconds += Time.deltaTime;
			if (order.Lifetime > 0f && ageSeconds >= order.Lifetime)
			{
				// 사라질 개체는 먼저 끄고 OnDisable 에서 복구. 자체 판단이 한 번 켜졌다 꺼지는 일 방지
				if (order.DespawnOnEnd)
				{
					gameObject.SetActive(false);
					return;
				}
				Release();
				return;
			}
			unitObject.UnitMovement.SetMoveDirection(order.Direction(transform.position, ageSeconds));
		}

		private void Release()
		{
			order = null;
			enabled = false;
			CombatUnitSpawner.RestoreBrains(silencedBrains);
			silencedBrains.Clear();
		}

		// 죽거나 멀어서 꺼지면 다음 풀 사용을 위해 자체 판단 복구
		private void OnDisable()
		{
			if (order == null)
				return;
			order = null;
			CombatUnitSpawner.RestoreBrains(silencedBrains);
			silencedBrains.Clear();
		}
	}
}
