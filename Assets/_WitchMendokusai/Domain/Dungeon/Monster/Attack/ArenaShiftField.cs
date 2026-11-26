using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 보스가 잠근 무대 칸들. 공격 자산은 상태가 없어 여기서 들고 감. 씬마다 하나, 처음 쓸 때 만들어짐
	// 잠긴 칸 안의 주인공: 계속 둔화, 간격마다 피해 (칸이 겹쳐도 한 번). 주인이 죽거나 꺼지면 그 주인 칸은 바로 풀림
	public sealed class ArenaShiftField : MonoBehaviour
	{
		private static ArenaShiftField instance;

		public static ArenaShiftField Instance
		{
			get
			{
				if (instance == null)
					instance = new GameObject(nameof(ArenaShiftField)).AddComponent<ArenaShiftField>();
				return instance;
			}
		}

		private sealed class Lock
		{
			public UnitObject Source;
			public ArenaShiftMonsterAttack Attack;
			public ArenaCell Cell;
			public float EndTime;
			public Action StopFx;
		}

		private readonly List<Lock> locks = new();
		private float nextTickTime;

		public int Count => locks.Count;

		private void OnDestroy()
		{
			if (instance == this)
				instance = null;
		}

		public void Add(UnitObject source, ArenaShiftMonsterAttack attack, ArenaCell cell)
		{
			float endTime = attack.LockSeconds > 0f ? Time.time + attack.LockSeconds : float.PositiveInfinity;
			Lock entry = new() { Source = source, Attack = attack, Cell = cell, EndTime = endTime };
			entry.StopFx = attack.LockFx.Play(new CombatFxContext
			{
				Position = cell.Center,
				Direction = cell.Direction,
				Radius = cell.Outer,
				Length = cell.Outer,
				InnerRadius = cell.Inner,
				ArcDegrees = cell.ArcDegrees,
				// 끝없는 칸은 시전자가 쓰러질 때 치움
				Seconds = attack.LockSeconds > 0f ? attack.LockSeconds : attack.EndlessFxSeconds,
			});
			// 예고 판 상한에 걸려 안 보이는 칸은 잠그지도 않음 (LingerField 와 같음)
			if (GroundTelegraph.IsSkipped(entry.StopFx))
				return;
			locks.Add(entry);
		}

		// source 가 attack 으로 잠근 칸을 모두 풂 (새 시전이 앞 시전을 갈아 끼움)
		public static void Release(UnitObject source, ArenaShiftMonsterAttack attack)
		{
			if (instance == null)
				return;
			for (int i = instance.locks.Count - 1; i >= 0; i--)
			{
				Lock entry = instance.locks[i];
				if (entry.Source != source || entry.Attack != attack)
					continue;
				instance.locks.RemoveAt(i);
				entry.StopFx?.Invoke();
			}
		}

		// 주인이 켜진 채 옮겨지거나 (재배치) 풀로 돌아감, 단계 전환으로 다시 시작. 그 주인 칸을 바로 풂
		public static void Abandon(UnitObject source)
		{
			if (instance == null)
				return;
			for (int i = instance.locks.Count - 1; i >= 0; i--)
			{
				Lock entry = instance.locks[i];
				if (entry.Source != source)
					continue;
				instance.locks.RemoveAt(i);
				entry.StopFx?.Invoke();
			}
		}

		// 생성, 소환, 재배치 자리를 잠긴 칸 밖으로. 칸이 없으면 그대로
		public static Vector3 PushOutOfLocked(Vector3 position, float bodyRadius)
		{
			if (instance == null)
				return position;
			foreach (Lock entry in instance.locks)
				position = entry.Cell.PushOut(position, bodyRadius);
			return position;
		}

		public static bool IsLocked(Vector3 position, float margin)
		{
			if (instance == null)
				return false;
			foreach (Lock entry in instance.locks)
			{
				if (entry.Cell.Contains(position, margin))
					return true;
			}
			return false;
		}

		private void Update()
		{
			for (int i = locks.Count - 1; i >= 0; i--)
			{
				Lock entry = locks[i];
				if (Time.time >= entry.EndTime || entry.Source == null || entry.Source.isActiveAndEnabled == false || entry.Source.IsAlive == false)
				{
					locks.RemoveAt(i);
					entry.StopFx?.Invoke();
				}
			}
			if (locks.Count == 0 || Time.time < nextTickTime)
				return;
			PlayerObject player = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (player == null || player.IsAlive == false)
				return;
			Lock hit = FindLockAt(player.transform.position);
			if (hit == null)
				return;
			ArenaShiftMonsterAttack attack = hit.Attack;
			nextTickTime = Time.time + attack.TickInterval;
			// 둔화는 피해가 무적에 막혀도 걸림. 다음 간격까지 이어짐 (SlowSeconds > TickInterval)
			if (attack.SlowRatio > 0f)
				UnitStatus.ApplySlow(player, hit.Source, attack.SlowSeconds, attack.SlowRatio);
			if (attack.TickDamage > 0)
			{
				// 틱 피해라 피격 무적을 켜지 않음
				DamageInfo tick = MonsterAttack.MakeDamage(hit.Source, attack.TickDamage);
				tick.skipsHitInvincibility = true;
				attack.HitEffect.Strike(hit.Source, player.transform.position, player, tick);
			}
		}

		private Lock FindLockAt(Vector3 position)
		{
			foreach (Lock entry in locks)
			{
				if (entry.Cell.Contains(position, 0f))
					return entry;
			}
			return null;
		}
	}
}
