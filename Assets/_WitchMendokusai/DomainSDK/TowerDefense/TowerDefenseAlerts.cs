using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 「지금 어디서 무슨 일이 났나」 — 화면 밖 사건을 알리는 목록 (TASK-WM-194).
	///
	/// ★ 왜 필요한가 (데아빌 레퍼런스 조사): 이 장르에서 사람들이 가장 많이 꼽는 불만이
	///   **무슨 일이 났는지 안 알려준다**는 것이다. 시야 밖 일은 그쪽을 보고 있던 사람만 알고,
	///   한 곳이 뚫리면 알아챘을 땐 이미 늦는다. 개척도 똑같은 구멍이 있었다 — 화면 밖에서
	///   서식지가 깨어나거나 건물이 부서져도 화면은 조용했다.
	/// ★ 소리·글자가 아니라 **자리**로 알린다: 「무슨 일」보다 「어디」가 먼저 필요하다(그쪽으로
	///   화면을 돌려야 하므로). 그래서 알림은 항상 월드 좌표를 들고 다닌다.
	///
	/// ★ 같은 자리에서 연달아 나는 일은 하나로 묶는다 — 안 그러면 한 곳이 무너지는 동안
	///   알림이 화면을 도배해서, 정작 *다른 곳*에서 난 일이 안 보인다(알림이 알림을 가린다).
	///
	/// 순수 규칙 — 씬·RNG 0. 시간은 밖에서 넣어 준다(테스트가 시계를 쥔다).
	/// </summary>
	public sealed class TowerDefenseAlerts
	{
		public readonly struct Alert
		{
			public readonly string Label;
			public readonly Vector3 Position;
			public readonly float ExpiresAt;

			public Alert(string label, Vector3 position, float expiresAt)
			{
				Label = label;
				Position = position;
				ExpiresAt = expiresAt;
			}
		}

		/// <summary> 동시에 띄우는 최대 개수 — 이보다 많으면 화면이 표식으로 덮여 아무것도 안 읽힌다. </summary>
		public const int MAX_ALERTS = 4;

		/// <summary> 이 거리 안에서 같은 종류가 또 나면 같은 사건으로 본다. </summary>
		public const float MERGE_DISTANCE = 8f;

		private readonly List<Alert> alerts = new();

		public IReadOnlyList<Alert> Active => alerts;

		/// <summary> 알림 하나. 같은 자리·같은 말이면 새로 안 쌓고 시간만 늘린다. </summary>
		public void Raise(string label, Vector3 position, float now, float lifetime)
		{
			if (string.IsNullOrEmpty(label) || lifetime <= 0f)
				return;

			float expiresAt = now + lifetime;

			for (int index = 0; index < alerts.Count; index++)
			{
				if (alerts[index].Label != label)
					continue;
				if ((alerts[index].Position - position).sqrMagnitude > MERGE_DISTANCE * MERGE_DISTANCE)
					continue;

				// 같은 사건 — 자리를 최신으로 옮기고 시간만 늘린다(개수는 안 는다).
				alerts[index] = new Alert(label, position, expiresAt);
				return;
			}

			alerts.Add(new Alert(label, position, expiresAt));

			// 넘치면 *가장 먼저 사라질 것*부터 버린다 — 새 사건이 옛 사건에 밀려 안 뜨면 알림이 무의미하다.
			while (alerts.Count > MAX_ALERTS)
			{
				int oldest = 0;
				for (int index = 1; index < alerts.Count; index++)
				{
					if (alerts[index].ExpiresAt < alerts[oldest].ExpiresAt)
						oldest = index;
				}
				alerts.RemoveAt(oldest);
			}
		}

		/// <summary> 시간이 다 된 알림을 걷어낸다. </summary>
		public void Prune(float now)
		{
			for (int index = alerts.Count - 1; index >= 0; index--)
			{
				if (alerts[index].ExpiresAt <= now)
					alerts.RemoveAt(index);
			}
		}

		public void Clear() => alerts.Clear();

		/// <summary>
		/// 「이만큼 오를 때마다 한 번」을 위한 단계 번호.
		///
		/// ★ 이 계산이 규칙층에 있어야 화면 없이 잴 수 있다. 매치 안에 두면 「오를 때만 한 번」이라는
		///   가장 틀리기 쉬운 부분(경계값·되돌림·꺼짐)이 Play 로만 확인 가능해지고, 그러면 사실상
		///   확인 안 하게 된다.
		/// stepSize 가 0 이하면 -1 = 「알리지 않음」.
		/// </summary>
		public static int StepFor(float value, float baseline, float stepSize)
		{
			if (stepSize <= 0f)
				return -1;
			return Mathf.FloorToInt((value - baseline) / stepSize);
		}
	}
}
