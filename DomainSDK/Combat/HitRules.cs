using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// 적중 규칙 플래그. 같은 프레임 같은 대상은 항상 1회
	public readonly struct HitRule
	{
		// 한 판정이 같은 대상을 한 번만
		public readonly bool OncePerTarget;
		// 대상이 범위를 나갔다 다시 들어와야 재적중
		public readonly bool ReenterToRehit;
		// 재적중 간격 초. 0이면 간격 없음
		public readonly float RehitInterval;

		public HitRule(bool oncePerTarget, bool reenterToRehit, float rehitInterval)
		{
			OncePerTarget = oncePerTarget;
			ReenterToRehit = reenterToRehit;
			RehitInterval = Math.Max(0f, rehitInterval);
		}

		public HitRule WithInterval(float interval) => new HitRule(OncePerTarget, ReenterToRehit, interval);
	}

	// 대상별 적중 기록. 같은 프레임 중복 제거, 대상당 1회, 나갔다 와야 재적중, 재적중 간격을 한 구조로
	// - 대상은 정수 번호 (엔진 쪽 개체 번호)
	public sealed class HitLedger
	{
		private struct Entry
		{
			public int LastFrame;
			public float NextTime;
			public bool Left;
		}

		private readonly Dictionary<long, Entry> entries = new Dictionary<long, Entry>();

		public int Count => entries.Count;

		public void Clear() => entries.Clear();

		// 적중 가능하면 기록하고 true. 안 되면 기록 안 함
		public bool TryHit(long targetId, float time, int frame, HitRule rule)
		{
			if (entries.TryGetValue(targetId, out Entry entry))
			{
				if (entry.LastFrame == frame)
					return false;
				if (rule.OncePerTarget)
					return false;
				if (rule.ReenterToRehit && entry.Left == false)
					return false;
				if (rule.RehitInterval > 0f && time < entry.NextTime)
					return false;
			}
			entries[targetId] = new Entry
			{
				LastFrame = frame,
				NextTime = time + rule.RehitInterval,
				Left = false,
			};
			return true;
		}

		// 대상이 범위를 나감. 기록이 없으면 무시
		public void Leave(long targetId)
		{
			if (entries.TryGetValue(targetId, out Entry entry) == false)
				return;
			entry.Left = true;
			entries[targetId] = entry;
		}
	}

	// 넉백 방향 종류. 기본은 공격자 반대 (기존 동작)
	public enum KnockbackDirectionKind
	{
		AwayFromAttacker = 0,
		// 판정 중심에서 대상 쪽
		FromHitCenter = 1,
		// 판정이 향한 방향
		HitForward = 2,
		// 판정이 움직이는 방향
		HitVelocity = 3,
	}

	public static class KnockbackDirections
	{
		// 바닥 평면 (x, z) 방향. 정할 수 없으면 false 이고 기본 (공격자 반대) 으로
		public static bool TryResolve(
			KnockbackDirectionKind kind,
			float targetX, float targetZ,
			float centerX, float centerZ,
			float forwardX, float forwardZ,
			float velocityX, float velocityZ,
			out float directionX, out float directionZ)
		{
			float x;
			float z;
			switch (kind)
			{
				case KnockbackDirectionKind.FromHitCenter:
					x = targetX - centerX;
					z = targetZ - centerZ;
					break;
				case KnockbackDirectionKind.HitForward:
					x = forwardX;
					z = forwardZ;
					break;
				case KnockbackDirectionKind.HitVelocity:
					x = velocityX;
					z = velocityZ;
					break;
				default:
					directionX = 0f;
					directionZ = 0f;
					return false;
			}
			float length = (float)Math.Sqrt((x * x) + (z * z));
			if (length < 0.0001f)
			{
				directionX = 0f;
				directionZ = 0f;
				return false;
			}
			directionX = x / length;
			directionZ = z / length;
			return true;
		}
	}
}
