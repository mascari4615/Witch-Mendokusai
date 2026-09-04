using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Presentation;

namespace WitchMendokusai.Idle
{
	/// <summary>
	/// 전투 사건의 볼트, 피해 숫자, 충격, 화면 흔들림
	///
	/// ★ 근거는 사진의 <see cref="IdleHit"/> 뿐. 연출이 스스로 박자를 세면 숫자와 체력이 어긋남
	/// ★ 한 프레임의 타격은 대상별로 합쳐 숫자 하나로. 초당 수십 타면 낱개는 판독 불가
	/// </summary>
	internal sealed class BattleFx
	{
		internal sealed class Settings
		{
			public float BoltSeconds { get; set; } = 0.18f;
			public float ShakeSeconds { get; set; } = 0.08f;
			public float ShakeDistance { get; set; } = 0.025f;
			public float NumberSeconds { get; set; } = 0.8f;
			public float NumberRise { get; set; } = 1.2f;
			public float NumberSize { get; set; } = 0.12f;
			/// <summary>충격 알갱이 크기 (m). 인형 몸통이 0.44 라 그 절반쯤이 읽힌다</summary>
			public float ImpactSize { get; set; } = 0.36f;

			public float ImpactSeconds { get; set; } = 0.34f;
			public float ImpactSpeed { get; set; } = 3.4f;
			public int ImpactCount { get; set; } = 7;

			/// <summary>적 색에 흰색을 섞는 몫. 안 섞으면 적 몸에 묻힌다</summary>
			public float ImpactWhiten { get; set; } = 0.55f;

			public Color BoltColor { get; set; }
			public Color NumberColor { get; set; }
			public Color HurtColor { get; set; }
		}

		private sealed class Bolt
		{
			public Transform Piece;
			public Vector3 From;
			public long Target;
			public float Age;
		}

		private sealed class Number
		{
			public FloatingTextElement Text;
			public Vector3 WorldPosition;
			public float Age;
		}

		private sealed class Impact
		{
			public Transform Piece;
			public Vector3 Way;
			public float Size;
			public float Age;
		}

		private readonly Transform holder;
		private readonly Settings settings;
		private readonly List<Bolt> bolts = new List<Bolt>();
		private readonly List<Number> numbers = new List<Number>();
		private readonly Stack<FloatingTextElement> numberPool = new Stack<FloatingTextElement>();
		private readonly List<Impact> impacts = new List<Impact>();
		private readonly Dictionary<long, double> dealtByFoe = new Dictionary<long, double>();
		private readonly Dictionary<int, double> takenBySeat = new Dictionary<int, double>();
		private float shakeLeft;
		private float clock;
		private VisualElement floatingTextRoot;

		public BattleFx(Transform holder, Settings settings)
		{
			this.holder = holder;
			this.settings = settings;
		}

		public void SetFloatingTextRoot(VisualElement root)
		{
			for (int index = numbers.Count - 1; index >= 0; index--)
			{
				numbers[index].Text.RemoveFromHierarchy();
			}

			numbers.Clear();
			numberPool.Clear();
			floatingTextRoot = root;
		}

		/// <summary>이번 프레임의 타격을 연출로. 대상별 합산 숫자 하나, 자리당 볼트 하나</summary>
		public void Consume(IdleHit[] hits, BattleEntityPresenter entities)
		{
			if (hits == null || hits.Length == 0)
			{
				return;
			}

			dealtByFoe.Clear();
			takenBySeat.Clear();

			for (int at = 0; at < hits.Length; at++)
			{
				IdleHit hit = hits[at];

				if (hit.ByFoe)
				{
					takenBySeat.TryGetValue(hit.Seat, out double taken);
					takenBySeat[hit.Seat] = taken + hit.Damage;
					entities.PlayAllyHit(hit.Seat);
					continue;
				}

				dealtByFoe.TryGetValue(hit.FoeIndex, out double dealt);
				dealtByFoe[hit.FoeIndex] = dealt + hit.Damage;
				entities.PlayAllyAttack(hit.Seat);

				bool firstOfSeat = at == 0 || hits[at - 1].Seat != hit.Seat || hits[at - 1].ByFoe;
				if (firstOfSeat && entities.TryGetAllyHead(hit.Seat, out Vector3 from))
				{
					SpawnBolt(from + new Vector3(0.3f, -0.4f, 0f), hit.FoeIndex);
				}
			}

			foreach (KeyValuePair<long, double> pair in dealtByFoe)
			{
				if (entities.TryGetFoeHead(pair.Key, out Vector3 head))
				{
					SpawnNumber(head, Numerics.BigNumberText.Format(pair.Value), FloatingTextKind.Normal);
				}

				if (entities.TryGetFoeImpact(pair.Key, out Vector3 impact, out Color color))
				{
					shakeLeft = Mathf.Max(shakeLeft, settings.ShakeSeconds);
					SpawnImpact(impact, color);
					entities.PlayFoeHit(pair.Key);
				}
			}

			foreach (KeyValuePair<int, double> pair in takenBySeat)
			{
				if (entities.TryGetAllyHead(pair.Key, out Vector3 head))
				{
					SpawnNumber(head, "-" + Numerics.BigNumberText.Format(pair.Value), FloatingTextKind.Hurt);
				}
			}
		}

		public void PlayVolley(long target, BattleEntityPresenter entities)
		{
			if (entities.TryGetFoeImpact(target, out Vector3 impact, out Color color) == false)
			{
				return;
			}

			for (int seat = 0; seat < IdleHeroes.PARTY_SLOTS; seat++)
			{
				if (entities.TryGetAllyHead(seat, out Vector3 from))
				{
					SpawnBolt(from, target);
				}
			}

			shakeLeft = Mathf.Max(shakeLeft, settings.ShakeSeconds * 2f);
			SpawnImpact(impact, color);
			SpawnImpact(impact + new Vector3(0.25f, 0.1f, 0.25f), settings.BoltColor);
			SpawnNumber(impact + Vector3.up, "일제 사격", FloatingTextKind.Critical);
		}

		public void PlaySupply(BattleEntityPresenter entities)
		{
			for (int seat = 0; seat < IdleHeroes.PARTY_SLOTS; seat++)
			{
				if (entities.TryGetAllyHead(seat, out Vector3 head))
				{
					SpawnNumber(head, "보급", FloatingTextKind.Buff);
					SpawnImpact(head + new Vector3(0f, -0.5f, 0f), settings.BoltColor);
				}
			}
		}

		public void PlayAppraise(BattleEntityPresenter entities)
		{
			for (int seat = 0; seat < IdleHeroes.PARTY_SLOTS; seat++)
			{
				if (entities.TryGetAllyHead(seat, out Vector3 head))
				{
					SpawnNumber(head, "감정", FloatingTextKind.Experience);
					SpawnImpact(head + new Vector3(0f, -0.35f, 0f), Color.Lerp(settings.BoltColor, Color.magenta, 0.45f));
				}
			}
		}

		public void Advance(float delta, BattleEntityPresenter entities)
		{
			clock += delta;

			if (shakeLeft > 0f)
			{
				shakeLeft -= delta;
				float left = settings.ShakeSeconds > 0f ? Mathf.Clamp01(shakeLeft / settings.ShakeSeconds) : 0f;
				holder.localPosition = BattleMotion.Shake(clock, settings.ShakeDistance, left);
			}
			else
			{
				holder.localPosition = Vector3.zero;
			}

			AdvanceBolts(delta, entities);
			AdvanceNumbers(delta);
			AdvanceImpacts(delta);
		}

		private void SpawnBolt(Vector3 from, long target)
		{
			GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			piece.name = "Bolt";
			piece.transform.SetParent(holder, false);
			piece.transform.position = from;
			piece.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
			BattleVisualFactory.Paint(piece, settings.BoltColor);

			bolts.Add(new Bolt { Piece = piece.transform, From = from, Target = target });
		}

		private void SpawnNumber(Vector3 position, string value, FloatingTextKind kind)
		{
			if (floatingTextRoot == null)
			{
				return;
			}

			FloatingTextElement text = numberPool.Count > 0 ? numberPool.Pop() : new FloatingTextElement();
			if (text.parent == null)
			{
				floatingTextRoot.Add(text);
			}

			text.style.display = DisplayStyle.Flex;
			text.Show(kind, value);
			numbers.Add(new Number { Text = text, WorldPosition = position });
		}

		/// <summary>
		/// 맞은 자리에서 튀는 조각. 볼트와 같은 방식으로 직접 이동
		///
		/// ★ ParticleSystem 을 안 쓴다 (실측 2026-08-31): 에디트 모드에서 자동 재생이 안 돼
		///   미리보기에서 타격이 통째로 안 보였다. 직접 움직이면 두 모드가 동일
		/// ★ 적 색에 흰색을 섞음. 안 섞으면 적 몸에 묻힘
		/// </summary>
		private void SpawnImpact(Vector3 position, Color color)
		{
			Color bright = Color.Lerp(color, Color.white, settings.ImpactWhiten);
			Material skin = BattleVisualFactory.MakeGlowing(bright, 0.8f);
			Mesh mesh = BattleVisualFactory.BuildImpactMesh();

			for (int at = 0; at < settings.ImpactCount; at++)
			{
				GameObject piece = new GameObject("Chip");
				piece.transform.SetParent(holder, false);
				piece.transform.position = position;
				piece.transform.localScale = new Vector3(settings.ImpactSize, settings.ImpactSize, settings.ImpactSize);
				piece.transform.localRotation = Quaternion.Euler(at * 47f, at * 73f, at * 29f);

				piece.AddComponent<MeshFilter>().sharedMesh = mesh;
				piece.AddComponent<MeshRenderer>().sharedMaterial = skin;

				// 부채꼴로 흩어짐. 위로 살짝 들려야 바닥에 안 묻힘
				float angle = (at + 0.5f) * Mathf.PI * 2f / settings.ImpactCount;
				Vector3 way = new Vector3(Mathf.Cos(angle), 0.55f, Mathf.Sin(angle)).normalized;

				impacts.Add(new Impact
				{
					Piece = piece.transform,
					Way = way * settings.ImpactSpeed,
					Size = settings.ImpactSize,
				});
			}
		}

		/// <summary>조각을 날리고 줄인다. 다 줄면 치운다</summary>
		private void AdvanceImpacts(float delta)
		{
			for (int at = impacts.Count - 1; at >= 0; at--)
			{
				Impact one = impacts[at];
				one.Age += delta;

				float life = settings.ImpactSeconds > 0f ? Mathf.Clamp01(one.Age / settings.ImpactSeconds) : 1f;

				if (life >= 1f || one.Piece == null)
				{
					if (one.Piece != null)
					{
						Kill(one.Piece.gameObject);
					}

					impacts.RemoveAt(at);
					continue;
				}

				// 처음이 빠르고 끝이 느림. 튀는 느낌은 감속에서
				one.Piece.position += one.Way * (1f - life) * delta;
				one.Piece.Rotate(Vector3.one, 420f * delta, Space.Self);

				float size = one.Size * (1f - life * life);
				one.Piece.localScale = new Vector3(size, size, size);
			}
		}

		private void AdvanceBolts(float delta, BattleEntityPresenter entities)
		{
			for (int at = bolts.Count - 1; at >= 0; at--)
			{
				Bolt bolt = bolts[at];
				bolt.Age += delta;

				float progress = settings.BoltSeconds > 0f ? Mathf.Clamp01(bolt.Age / settings.BoltSeconds) : 1f;
				Vector3 target = entities.TryGetFoeHead(bolt.Target, out Vector3 head)
					? head + new Vector3(0f, -0.6f, 0f)
					: bolt.From + new Vector3(3f, 0f, 0f);

				bolt.Piece.position = Vector3.Lerp(bolt.From, target, progress);

				if (progress >= 1f)
				{
					Kill(bolt.Piece.gameObject);
					bolts.RemoveAt(at);
				}
			}
		}

		private void AdvanceNumbers(float delta)
		{
			Camera eye = Camera.main;

			for (int at = numbers.Count - 1; at >= 0; at--)
			{
				Number number = numbers[at];
				number.Age += delta;
				float life = settings.NumberSeconds > 0f ? Mathf.Clamp01(number.Age / settings.NumberSeconds) : 1f;

				if (life >= 1f)
				{
					number.Text.Hide();
					numberPool.Push(number.Text);
					numbers.RemoveAt(at);
					continue;
				}

				if (eye != null)
				{
					number.WorldPosition += new Vector3(0f, settings.NumberRise * delta, 0f);
					Vector3 screen = eye.WorldToScreenPoint(number.WorldPosition);
					if (screen.z > 0f)
					{
						number.Text.SetScreenPosition(screen);
					}
				}
			}
		}

		private static void Kill(GameObject piece)
		{
			if (Application.isPlaying)
			{
				Object.Destroy(piece);
			}
			else
			{
				Object.DestroyImmediate(piece);
			}
		}
	}
}
