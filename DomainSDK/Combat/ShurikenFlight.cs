using System;

namespace WitchMendokusai
{
	public enum ShurikenFlightPhase
	{
		Outbound = 0,
		Returning = 1,
		Done = 2,
	}

	// 알리사 닌자 수리검 하나의 비행 상태. 나가며 튕기고, 나가는 시간이 다 되면 (튕김이 남아도) 돌아옴
	// 위치와 다음 표적은 엔진이 정하고 여기는 순서와 속도만. 풀에서 다시 쓰므로 던질 때마다 Begin
	public sealed class ShurikenFlight
	{
		private const float MIN_SECONDS = 0.01f;

		private float outboundSeconds = MIN_SECONDS;
		private float returnTimeoutSeconds = MIN_SECONDS;
		private bool returns;
		private float elapsed;

		public ShurikenFlightPhase Phase { get; private set; } = ShurikenFlightPhase.Done;
		public int BouncesLeft { get; private set; }

		public void Begin(float outboundSeconds, bool returns, int bounces, float returnTimeoutSeconds)
		{
			this.outboundSeconds = Math.Max(MIN_SECONDS, outboundSeconds);
			this.returnTimeoutSeconds = Math.Max(MIN_SECONDS, returnTimeoutSeconds);
			this.returns = returns;
			BouncesLeft = Math.Max(0, bounces);
			Phase = ShurikenFlightPhase.Outbound;
			elapsed = 0f;
		}

		// 지금 꺾으면 닿을 수 있는 거리. 다음 적은 이 안에서 찾음 (마지막 구간은 느려져 절반만 감)
		public float ReachAfterBounce(float outboundSpeed)
		{
			float full = outboundSpeed * outboundSeconds;
			return returns && BouncesLeft <= 1 ? full / 2f : full;
		}

		// 맞힌 순간. 다음 적으로 꺾어야 하면 true. 돌아오는 길에는 꺾지 않음 (끝없이 남지 않게)
		public bool TryBounce(bool hasNextTarget)
		{
			if (Phase != ShurikenFlightPhase.Outbound || BouncesLeft <= 0 || hasNextTarget == false)
				return false;
			BouncesLeft--;
			// 꺾을 때마다 나가는 시간을 새로 셈
			elapsed = 0f;
			return true;
		}

		// 관통을 다 씀. 돌아오는 수리검은 바로 돌아서고 아니면 끝
		public void Expend()
		{
			if (Phase == ShurikenFlightPhase.Outbound)
				EndOutbound(0f);
		}

		// 긴 프레임의 남은 시간은 다음 단계로 넘김
		public void Tick(float deltaSeconds)
		{
			if (Phase == ShurikenFlightPhase.Done)
				return;
			elapsed += Math.Max(0f, deltaSeconds);
			if (Phase == ShurikenFlightPhase.Outbound && elapsed >= outboundSeconds)
				EndOutbound(elapsed - outboundSeconds);
			if (Phase == ShurikenFlightPhase.Returning && elapsed >= returnTimeoutSeconds)
				Phase = ShurikenFlightPhase.Done;
		}

		// 돌아오다 주인에게 닿음
		public void Catch()
		{
			if (Phase == ShurikenFlightPhase.Returning)
				Phase = ShurikenFlightPhase.Done;
		}

		// 지금 속도. 튕김이 남은 동안은 고른 속도, 돌아오는 수리검의 마지막 구간만 끝에서 멈췄다 빨라짐 (되돌아오는빗자루와 같은 꼴)
		public float Speed(float outboundSpeed, float returnSpeed)
		{
			switch (Phase)
			{
				case ShurikenFlightPhase.Outbound:
					return returns && BouncesLeft == 0 ? outboundSpeed * (1f - elapsed / outboundSeconds) : outboundSpeed;
				case ShurikenFlightPhase.Returning:
					return returnSpeed * Math.Min(1f, elapsed / outboundSeconds);
				default:
					return 0f;
			}
		}

		private void EndOutbound(float carriedSeconds)
		{
			Phase = returns ? ShurikenFlightPhase.Returning : ShurikenFlightPhase.Done;
			elapsed = carriedSeconds;
		}
	}
}
