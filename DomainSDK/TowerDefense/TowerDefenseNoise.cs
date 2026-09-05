using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 소리(TASK-WM-194) — 내가 무엇을 하면 그 소리가 퍼지고, 자는 것을 깨운다.
	///
	/// ★ 왜 필요한가: 지금 서식지는 **가까이 가야만** 깨어난다. 그래서 「멀찍이서 조용히 크는 것」과
	///   「바로 옆에서 포탑을 난사하는 것」이 똑같이 안전하다 — 개척의 위험이 *거리 하나*로만 표현된다.
	///   데아빌의 축은 거리가 아니라 **내 행동**이다: 짓고, 쏘고, 얻어맞는 소리가 마수를 부른다.
	///   그래야 「지금 저 둥지 옆에서 싸워도 되나」가 매 순간의 판단이 된다.
	///
	/// ★ 소리 사태(cascade)가 이 규칙의 심장이다. 얻어맞으면 소리가 나고, 그 소리가 더 부르고,
	///   더 온 놈들이 또 때려서 더 시끄러워진다. 방치하면 한 구석이 통째로 무너진다 —
	///   *빨리 끊어야 하는 이유*가 규칙에서 나온다.
	///
	/// ★ 왜 「점 목록」인가: 격자 전체에 값을 깔면 판 크기만큼 메모리·계산이 든다. 소리는 몇 곳에서만
	///   나고 금방 잦아들므로 그때그때 생긴 자리만 들고 있으면 된다(잦아든 자리는 스스로 사라진다).
	///
	/// 순수 계산이라 프레임 없이 시험할 수 있다. Unity 의존은 Vector3 뿐.
	/// </summary>
	public sealed class TowerDefenseNoise
	{
		/// <summary> 한 자리에 쌓이는 소리의 상한 — 사태가 나도 무한히 커지진 않는다. </summary>
		public const float MAX_LEVEL = 100f;

		/// <summary> 이 아래로 잦아들면 없는 소리로 친다(목록에서 지운다). </summary>
		private const float SILENCE = 0.01f;

		private readonly List<Vector3> positions = new();
		private readonly List<float> levels = new();

		public int SourceCount => positions.Count;

		/// <summary> 지금 판에서 가장 시끄러운 소리 — 화면·검사가 「규칙이 도나」를 볼 창. </summary>
		public float LoudestLevel
		{
			get
			{
				float loudest = 0f;
				for (int index = 0; index < levels.Count; index++)
				{
					if (levels[index] > loudest)
						loudest = levels[index];
				}
				return loudest;
			}
		}

		public void Clear()
		{
			positions.Clear();
			levels.Clear();
		}

		/// <summary>
		/// 그 자리에서 소리가 났다. 가까운 자리는 하나로 합친다 — 같은 자리에서 스무 번 쏘면
		/// 자리가 스무 개 생기는 게 아니라 *한 자리가 스무 배 시끄러워야* 한다.
		/// </summary>
		public void Emit(Vector3 worldPosition, float amount, float mergeDistance)
		{
			if (amount <= 0f)
				return;

			float mergeSqr = mergeDistance * mergeDistance;
			for (int index = 0; index < positions.Count; index++)
			{
				if ((positions[index] - worldPosition).sqrMagnitude > mergeSqr)
					continue;

				levels[index] = Mathf.Min(MAX_LEVEL, levels[index] + amount);
				return;
			}

			positions.Add(worldPosition);
			levels.Add(Mathf.Min(MAX_LEVEL, amount));
		}

		/// <summary>
		/// 소리는 잦아든다 — *비율로* 준다(초당 절반 식). 일정량씩 빼면 큰 소리는 영원히 남고
		/// 작은 소리는 한 틱에 사라져, 「크게 났다가 잦아든다」는 느낌 자체가 안 생긴다.
		/// </summary>
		public void Tick(float deltaTime, float decayPerSecond)
		{
			if (deltaTime <= 0f || decayPerSecond <= 0f)
				return;

			float keep = Mathf.Pow(Mathf.Clamp01(1f - decayPerSecond), deltaTime);
			for (int index = levels.Count - 1; index >= 0; index--)
			{
				levels[index] *= keep;
				if (levels[index] >= SILENCE)
					continue;

				levels.RemoveAt(index);
				positions.RemoveAt(index);
			}
		}

		/// <summary>
		/// 그 자리에서 들리는 소리의 크기. 멀수록 작아지고(선형), 들리는 거리 밖은 0 이다.
		/// 여러 소리는 **더해진다** — 사방에서 조금씩 나는 것도 모이면 깨울 수 있어야 사태가 성립한다.
		/// </summary>
		public float LevelAt(Vector3 worldPosition, float hearingRadius)
		{
			if (hearingRadius <= 0f)
				return 0f;

			float heard = 0f;
			for (int index = 0; index < positions.Count; index++)
			{
				float distance = Vector3.Distance(positions[index], worldPosition);
				if (distance >= hearingRadius)
					continue;

				heard += levels[index] * (1f - distance / hearingRadius);
			}
			return heard;
		}
	}
}
