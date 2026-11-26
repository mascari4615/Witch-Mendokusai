using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 보스가 낸 안전 원들. 공격 자산은 여러 유닛이 나눠 쓰고 행동은 이미 다음으로 가서 여기서 움직이고 판정. 씬마다 하나, 처음 쓸 때 만들어짐
	// 원마다 빈 물체 하나를 길을 따라 옮기고, 바닥 그림은 그 물체를 따라감. 주인이 죽거나 꺼지면 그 주인 원은 바로 끝
	public sealed class SafeCircleField : MonoBehaviour
	{
		private static SafeCircleField instance;

		public static SafeCircleField Instance
		{
			get
			{
				if (instance == null)
					instance = new GameObject(nameof(SafeCircleField)).AddComponent<SafeCircleField>();
				return instance;
			}
		}

		private sealed class Circle
		{
			public UnitObject Source;
			public SafeCircleMonsterAttack Attack;
			public Vector3 Start;
			public Vector3 Forward;
			public float StartTime;
			public float NextHitTime;
			public Transform Mover;
			public Action StopFx;
		}

		private readonly List<Circle> circles = new();

		public int Count => circles.Count;

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
		}

		public void Add(UnitObject source, SafeCircleMonsterAttack attack, Vector3 start, Vector3 forward)
		{
			Transform mover = new GameObject("SafeCircle").transform;
			mover.SetParent(transform, false);
			mover.position = start;
			Circle circle = new()
			{
				Source = source,
				Attack = attack,
				Start = start,
				Forward = forward,
				StartTime = Time.time,
				NextHitTime = Time.time + attack.OutsideInterval,
				Mover = mover,
			};
			circle.StopFx = attack.CircleFx.Play(new CombatFxContext
			{
				Position = start,
				Follow = mover,
				Radius = attack.Radius,
				Seconds = attack.Seconds,
			});
			// 예고 판 상한에 걸려 안 보이는 원은 판정도 안 함 (LingerField 와 같음)
			if (GroundTelegraph.IsSkipped(circle.StopFx))
			{
				Destroy(mover.gameObject);
				return;
			}
			circles.Add(circle);
		}

		// 주인이 켜진 채 옮겨지거나 (재배치) 풀로 돌아감, 단계 전환으로 다시 시작. 그 주인 원을 바로 끝냄
		public static void Abandon(UnitObject source)
		{
			if (instance == null)
				return;
			for (int i = instance.circles.Count - 1; i >= 0; i--)
			{
				if (instance.circles[i].Source == source)
					instance.End(i);
			}
		}

		// 지금 원 중심. 원이 없으면 false. 시험과 개발 창용
		public bool TryGetCenter(UnitObject source, out Vector3 center)
		{
			foreach (Circle circle in circles)
			{
				if (circle.Source != source)
					continue;
				center = circle.Mover.position;
				return true;
			}
			center = default;
			return false;
		}

		private void Update()
		{
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			for (int i = circles.Count - 1; i >= 0; i--)
			{
				Circle circle = circles[i];
				float elapsed = Time.time - circle.StartTime;
				if (IsOver(circle, elapsed))
				{
					End(i);
					continue;
				}
				SafeCircleMonsterAttack attack = circle.Attack;
				circle.Mover.position = SafeCircleMonsterAttack.PositionAt(circle.Start, circle.Forward, attack.Path, attack.Speed, elapsed);
				if (Time.time < circle.NextHitTime || player == null || player.IsAlive == false)
					continue;
				circle.NextHitTime = Time.time + attack.OutsideInterval;
				if (SafeCircleMonsterAttack.IsInside(circle.Mover.position, attack.Radius, player.transform.position))
					continue;
				// 맞은 그림은 HitEffect.HitFx (피해가 실제로 들어갔을 때만). 틱 피해라 피격 무적을 켜지 않음 (켜면 원 밖이 더 안전해짐)
				DamageInfo outside = MonsterAttack.MakeDamage(circle.Source, attack.OutsideDamage);
				outside.skipsHitInvincibility = true;
				attack.HitEffect.Strike(circle.Source, circle.Mover.position, player, outside);
			}
		}

		private static bool IsOver(Circle circle, float elapsed) =>
			elapsed >= circle.Attack.Seconds || circle.Source == null || circle.Source.isActiveAndEnabled == false || circle.Source.IsAlive == false;

		private void End(int index)
		{
			Circle circle = circles[index];
			circles.RemoveAt(index);
			circle.StopFx?.Invoke();
			if (circle.Mover != null)
				Destroy(circle.Mover.gameObject);
		}
	}
}
