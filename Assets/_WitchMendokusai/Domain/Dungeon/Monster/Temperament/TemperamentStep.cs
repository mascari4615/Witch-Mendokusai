using UnityEngine;

namespace WitchMendokusai
{
	// 성격이 이번 틱에 내린 결정 한 개
	public struct TemperamentStep
	{
		// 걷는 쪽. 0 이면 멈춤
		public Vector3 Direction;
		// 걷는 세기 0~1 (1 이 제 속도)
		public float Speed;
		// 0 보다 크면 이 속도로 이 초만큼 튀어 나감 (깡충, 덮치기). 걷기 입력을 덮음
		public Vector3 BurstVelocity;
		public float BurstSeconds;
		// 0 보다 크면 몸을 이 배율로 잠깐 부풀림 (놀람, 도움닫기)
		public float Punch;
		// 걷는 쪽과 상관없이 주인공 쪽을 봄 (뒷걸음질). 멈췄는데 이것도 아니면 보던 쪽 그대로 (잠, 모른 척)
		public bool FacesPlayer;
		// 공격 준비를 시작하지 않음 (잠듦, 모른 척, 겁먹음, 얼어붙음). 공격 (BT_WindupAttack) 이 읽음
		public bool HoldsAttack;
		// 이번 틱에 TeleportTo 로 순간이동 (점멸). 높이는 드라이버가 맞춤
		public bool Teleport;
		public Vector3 TeleportTo;

		public static TemperamentStep Walk(Vector3 direction, float speed = 1f) => new() { Direction = direction, Speed = Mathf.Clamp01(speed) };

		public static TemperamentStep Stay => new() { Direction = Vector3.zero, Speed = 0f, FacesPlayer = true };

		// 멈추고 보던 쪽 그대로, 공격도 안 함 (잠, 모른 척)
		public static TemperamentStep Idle => new() { Direction = Vector3.zero, Speed = 0f, HoldsAttack = true };

		public static TemperamentStep Burst(Vector3 velocity, float seconds, float punch = 0f) => new() { Direction = velocity.normalized, Speed = 1f, BurstVelocity = velocity, BurstSeconds = seconds, Punch = punch };
	}
}
