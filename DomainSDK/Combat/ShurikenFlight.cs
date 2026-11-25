using System;

namespace WitchMendokusai
{
	public enum ShurikenFlightPhase
	{
		Outbound = 0,
		Returning = 1,
		Done = 2,
	}

	// 알리사 닌자 수리검 하나의 비행 상태. 튕김을 다 쓴 뒤에 돌아옴 (둘 다 받은 판: 표창 갈림길의 귀환 + 그림자 묶음의 튕김)
	// 위치와 다음 표적은 엔진이 정하고 여기는 순서와 속도만
	public sealed class ShurikenFlight
	{
		private readonly float outboundSeconds;
		private readonly float returnTimeoutSeconds;
		private readonly bool returns;
		private float elapsed;

		public ShurikenFlight(float outboundSeconds, bool returns, int bounces, float returnTimeoutSeconds)
		{
			this.outboundSeconds = Math.Max(0.01f, outboundSeconds);
			this.returnTimeoutSeconds = Math.Max(0.01f, returnTimeoutSeconds);
			this.returns = returns;
			BouncesLeft = Math.Max(0, bounces);
		}

		public ShurikenFlightPhase Phase { get; private set; } = ShurikenFlightPhase.Outbound;
		public int BouncesLeft { get; private set; }

		// 맞힌 순간. 다음 적으로 꺾어야 하면 true. 돌아오는 길에는 꺾지 않음 (끝없이 남지 않게)
		public bool TryBounce(bool hasNextTarget)
		{
			if (Phase != ShurikenFlightPhase.Outbound || BouncesLeft <= 0 || hasNextTarget == false)
				return false;
			BouncesLeft--;
			// 꺾을 때마다 나가는 길을 새로 셈. 늦게 맞혀도 다음 적까지 닿게
			elapsed = 0f;
			return true;
		}

		public void Tick(float deltaSeconds)
		{
			if (Phase == ShurikenFlightPhase.Done)
				return;
			elapsed += Math.Max(0f, deltaSeconds);
			if (Phase == ShurikenFlightPhase.Outbound && elapsed >= outboundSeconds)
			{
				Phase = returns ? ShurikenFlightPhase.Returning : ShurikenFlightPhase.Done;
				elapsed = 0f;
			}
			else if (Phase == ShurikenFlightPhase.Returning && elapsed >= returnTimeoutSeconds)
				Phase = ShurikenFlightPhase.Done;
		}

		// 돌아오다 주인에게 닿음
		public void Catch()
		{
			if (Phase == ShurikenFlightPhase.Returning)
				Phase = ShurikenFlightPhase.Done;
		}

		// 지금 속도. 돌아오는 수리검만 끝에서 멈췄다 빨라짐 (되돌아오는빗자루와 같은 꼴), 아니면 고른 속도
		public float Speed(float outboundSpeed, float returnSpeed)
		{
			switch (Phase)
			{
				case ShurikenFlightPhase.Outbound:
					return returns ? outboundSpeed * (1f - elapsed / outboundSeconds) : outboundSpeed;
				case ShurikenFlightPhase.Returning:
					return returnSpeed * Math.Min(1f, elapsed / outboundSeconds);
				default:
					return 0f;
			}
		}
	}
}
