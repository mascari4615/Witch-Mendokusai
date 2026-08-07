using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 뚫린 자리(TASK-WM-194) — 내 건물이 부서진 곳은 잊히지 않는다.
	///
	/// ★ 왜 필요한가: 여태 건물이 부서지면 그냥 손실이었다. 그래서 「지킬 수 있는 만큼만 넓혀라」가
	///   말로만 있고 규칙엔 없었다 — 멀리 흩뿌려 짓다가 하나 잃어도 다음 파도는 아무 상관 없는
	///   방향에서 왔다. 데아빌의 핵심은 *한 번 뚫린 곳이 다음 위협을 부른다*는 것이다.
	///
	/// ★ 무엇을 하는가: 부서진 자리마다 열기를 쌓고, 가까운 자리끼리는 하나로 합친다. 열기는
	///   시간이 지나면 식는다(한 번 실수가 영원한 벌이 되면 안 된다). 열기가 남아 있는 동안
	///   다음 파도는 *그쪽으로 치우쳐서* 온다.
	///
	/// ★ 왜 「그쪽에서 스폰」이 아니라 「그쪽으로 치우침」인가: 부서진 자리는 대개 내 기지 안이다.
	///   거기서 마수가 솟으면 방어선이라는 개념 자체가 무의미해진다. 테두리 침공이라는 규칙은
	///   그대로 두고 *어느 테두리인가*만 끌어당긴다 — 대응할 수 있는 위협이어야 재미가 된다.
	///
	/// 순수 계산이라 프레임 없이 시험할 수 있다. Unity 의존은 Vector3 뿐.
	/// </summary>
	public sealed class TowerDefenseBreach
	{
		/// <summary> 한 자리의 열기 상한 — 여기 이상으론 안 쌓인다(한 곳만 잃어도 영영 지옥이 되면 안 된다). </summary>
		public const float MAX_HEAT = 3f;

		/// <summary> 이 열기부터 「아직 뜨겁다」로 본다. 이 아래는 잊힌 자리다. </summary>
		public const float HOT_THRESHOLD = 0.5f;

		public readonly struct Site
		{
			public readonly Vector3 Position;
			public readonly float Heat;

			public Site(Vector3 position, float heat)
			{
				Position = position;
				Heat = heat;
			}
		}

		private readonly List<Vector3> positions = new();
		private readonly List<float> heats = new();

		/// <summary> 지금 뜨거운 자리 수 — 화면·검사가 「규칙이 살아 있나」를 볼 창. </summary>
		public int HotCount
		{
			get
			{
				int count = 0;
				for (int index = 0; index < heats.Count; index++)
				{
					if (heats[index] >= HOT_THRESHOLD)
						count++;
				}
				return count;
			}
		}

		public int SiteCount => positions.Count;

		public Site SiteAt(int index) => new(positions[index], heats[index]);

		public void Clear()
		{
			positions.Clear();
			heats.Clear();
		}

		/// <summary>
		/// 건물 하나를 잃었다. 가까운 자리가 이미 있으면 거기에 얹는다 —
		/// 한 구역이 무너지는 것과 사방에서 하나씩 잃는 것은 다른 사건이다.
		///
		/// 돌려주는 값 = **이번에 그 자리가 처음으로 뜨거워졌는가.** 화면이 이걸 보고 딱 한 번
		/// 알린다 — 잃을 때마다 매번 외치면 정작 급한 알림을 덮고, 안 외치면 규칙이 안 보인다.
		/// </summary>
		public bool Add(Vector3 worldPosition, float mergeDistance, float heatPerLoss)
		{
			float mergeSqr = mergeDistance * mergeDistance;
			for (int index = 0; index < positions.Count; index++)
			{
				if ((positions[index] - worldPosition).sqrMagnitude > mergeSqr)
					continue;

				bool wasHot = heats[index] >= HOT_THRESHOLD;
				heats[index] = Mathf.Min(MAX_HEAT, heats[index] + heatPerLoss);
				return wasHot == false && heats[index] >= HOT_THRESHOLD;
			}

			positions.Add(worldPosition);
			float heat = Mathf.Min(MAX_HEAT, heatPerLoss);
			heats.Add(heat);
			return heat >= HOT_THRESHOLD;
		}

		/// <summary> 열기가 식는다. 다 식은 자리는 지워진다 — 목록이 무한히 자라면 안 된다. </summary>
		public void Tick(float deltaTime, float coolPerSecond)
		{
			if (deltaTime <= 0f || coolPerSecond <= 0f)
				return;

			for (int index = heats.Count - 1; index >= 0; index--)
			{
				heats[index] -= coolPerSecond * deltaTime;
				if (heats[index] > 0f)
					continue;

				heats.RemoveAt(index);
				positions.RemoveAt(index);
			}
		}

		/// <summary>
		/// 뚫린 쪽이 어느 방향인가(코어 기준, 도 단위 0=북·시계방향). 뜨거운 자리가 없으면 false.
		///
		/// ★ 열기로 가중한 *방향의 합*이다 — 반대쪽 두 곳이 똑같이 뜨거우면 서로 상쇄돼
		///   「한쪽으로 치우칠 이유가 없다」가 된다. 그게 맞다: 사방이 뚫렸으면 어디로 올지 모른다.
		/// </summary>
		public bool TryGetBiasAngle(Vector3 core, out float angleDegrees)
		{
			angleDegrees = 0f;
			Vector2 sum = Vector2.zero;

			for (int index = 0; index < positions.Count; index++)
			{
				if (heats[index] < HOT_THRESHOLD)
					continue;

				Vector3 offset = positions[index] - core;
				Vector2 flat = new(offset.x, offset.z);
				if (flat.sqrMagnitude <= Mathf.Epsilon)
					continue;

				sum += flat.normalized * heats[index];
			}

			if (sum.sqrMagnitude <= Mathf.Epsilon)
				return false;

			// 북(+z)=0 도, 동(+x)=90 도 — 파도 원점이 쓰는 것과 같은 셈법이어야 한다.
			angleDegrees = Mathf.Repeat(Mathf.Atan2(sum.x, sum.y) * Mathf.Rad2Deg, 360f);
			return true;
		}
	}
}
