using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 굽기 한 번. 바늘이 0 과 1 사이를 오가고, 알맞은 칸에서 누르면 잘 구움
	// - 실패 없음: 칸 밖에서 눌러도, 끝까지 안 눌러도 음식은 나옴
	// - 바늘의 시작 자리는 뽑음. 같은 때 누르기만으로 늘 잘 굽지 않게
	public sealed class CookTiming
	{
		private readonly HomesteadTuning tuning;
		private float elapsed;
		private readonly float startPhase;

		public bool IsOver { get; private set; }
		public bool IsWellDone { get; private set; }
		public float Needle { get; private set; }
		public bool IsSweet => Needle >= tuning.CookSweetFrom && Needle <= tuning.CookSweetTo;
		public float SweetFrom => tuning.CookSweetFrom;
		public float SweetTo => tuning.CookSweetTo;
		// 남은 몫. 화면이 다 구워지기까지를 보임
		public float TimeLeft => Math.Max(0f, 1f - elapsed / tuning.CookMaxSeconds);

		internal CookTiming(HomesteadTuning tuning, Random random)
		{
			this.tuning = tuning;
			startPhase = (float)random.NextDouble();
			Needle = NeedleAt(0f);
		}

		// 왕복 한 번 (0, 1, 0) 의 길이는 CookSwingSeconds
		private float NeedleAt(float seconds)
		{
			float phase = (startPhase + seconds / tuning.CookSwingSeconds) % 1f;
			return phase < 0.5f ? phase * 2f : 2f - phase * 2f;
		}

		public void Step(float seconds)
		{
			if (IsOver)
			{
				return;
			}
			elapsed += seconds;
			Needle = NeedleAt(elapsed);
			if (elapsed >= tuning.CookMaxSeconds)
			{
				IsOver = true;
			}
		}

		public void Press()
		{
			if (IsOver)
			{
				return;
			}
			IsWellDone = IsSweet;
			IsOver = true;
		}
	}
}
