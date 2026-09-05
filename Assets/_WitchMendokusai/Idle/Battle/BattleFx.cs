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
			public float BoltSeconds { get; set; }
			public float BoltSize { get; set; }
			/// <summary>볼트 출발점. 아군 머리 기준. 손에서 나가는 느낌</summary>
			public Vector3 BoltMuzzleOffset { get; set; }
			/// <summary>볼트 도착점. 적 머리 기준. 몸통 가운데</summary>
			public Vector3 BoltTargetOffset { get; set; }
			/// <summary>대상이 사라졌을 때 볼트가 앞으로 날아가는 거리</summary>
			public float BoltMissDistance { get; set; }
			public float ShakeSeconds { get; set; }
			public float VolleyShakeScale { get; set; }
			public Vector3 VolleySecondImpactOffset { get; set; }
			public float VolleyTextLift { get; set; }
			public Vector3 SupplyImpactOffset { get; set; }
			public Vector3 AppraiseImpactOffset { get; set; }
			public Color AppraiseImpactColor { get; set; }
			public float ShakeDistance { get; set; }
			public float ShakeFrequencyX { get; set; }
			public float ShakeFrequencyY { get; set; }
			/// <summary>세로 흔들림 몫. 가로 대비</summary>
			public float ShakeShareY { get; set; }
			public float NumberSeconds { get; set; }
			public float NumberRise { get; set; }
			public float NumberSize { get; set; }
			/// <summary>충격 알갱이 크기 (m). 인형 몸통이 0.44 라 그 절반쯤이 읽힌다</summary>
			public float ImpactSize { get; set; }

			public float ImpactSeconds { get; set; }
			public float ImpactSpeed { get; set; }
			public int ImpactCount { get; set; }

			/// <summary>적 색에 흰색을 섞는 몫. 안 섞으면 적 몸에 묻힌다</summary>
			public float ImpactWhiten { get; set; }

			public float ImpactGlow { get; set; }
			/// <summary>조각 초기 회전 걸음. 조각 번호에 곱함</summary>
			public Vector3 ImpactEulerStep { get; set; }
			/// <summary>부채꼴 방향의 위 성분. 0 이면 바닥에 묻힘</summary>
			public float ImpactLift { get; set; }
			public float ImpactSpinDegrees { get; set; }

			public Color BoltColor { get; set; }
			public Color NumberColor { get; set; }
			public Color HurtColor { get; set; }
			public string VolleyText { get; set; }
			public string SupplyText { get; set; }
			public string AppraiseText { get; set; }
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
		private bool textShown = true;

		public BattleFx(Transform holder, Settings settings)
		{
			this.holder = holder;
			this.settings = settings;
		}

		/// <summary>
		/// 피해 숫자를 띄울까. 가게나 연구실을 보는 동안은 안 띄운다 (사용자 2026-09-05)
		///
		/// ★ 전투는 뒤에서 계속 돌지만 화면은 다른 자리를 보고 있음. 그 자리에 전투 숫자가
		///   뜨면 카메라가 옮겨 간 뜻이 사라짐
		/// </summary>
		public void SetTextShown(bool shown)
		{
			textShown = shown;

			if (shown == false)
			{
				for (int index = numbers.Count - 1; index >= 0; index--)
				{
					numbers[index].Text.style.display = DisplayStyle.None;
				}
			}
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
					SpawnBolt(from + settings.BoltMuzzleOffset, hit.FoeIndex);
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

			shakeLeft = Mathf.Max(shakeLeft, settings.ShakeSeconds * settings.VolleyShakeScale);
			SpawnImpact(impact, color);
			SpawnImpact(impact + settings.VolleySecondImpactOffset, settings.BoltColor);
			SpawnNumber(impact + Vector3.up * settings.VolleyTextLift, settings.VolleyText, FloatingTextKind.Critical);
		}

		public void PlaySupply(BattleEntityPresenter entities)
		{
			for (int seat = 0; seat < IdleHeroes.PARTY_SLOTS; seat++)
			{
				if (entities.TryGetAllyHead(seat, out Vector3 head))
				{
					SpawnNumber(head, settings.SupplyText, FloatingTextKind.Buff);
					SpawnImpact(head + settings.SupplyImpactOffset, settings.BoltColor);
				}
			}
		}

		public void PlayAppraise(BattleEntityPresenter entities)
		{
			for (int seat = 0; seat < IdleHeroes.PARTY_SLOTS; seat++)
			{
				if (entities.TryGetAllyHead(seat, out Vector3 head))
				{
					SpawnNumber(head, settings.AppraiseText, FloatingTextKind.Experience);
					SpawnImpact(head + settings.AppraiseImpactOffset, settings.AppraiseImpactColor);
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
				holder.localPosition = BattleMotion.Shake(
					clock, settings.ShakeDistance, left,
					settings.ShakeFrequencyX, settings.ShakeFrequencyY, settings.ShakeShareY);
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
			piece.transform.localScale = Vector3.one * settings.BoltSize;
			BattleVisualFactory.Paint(piece, settings.BoltColor);

			bolts.Add(new Bolt { Piece = piece.transform, From = from, Target = target });
		}

		private void SpawnNumber(Vector3 position, string value, FloatingTextKind kind)
		{
			if (floatingTextRoot == null || textShown == false)
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
			Material skin = BattleVisualFactory.MakeGlowing(bright, settings.ImpactGlow);
			Mesh mesh = BattleVisualFactory.BuildImpactMesh();

			for (int at = 0; at < settings.ImpactCount; at++)
			{
				GameObject piece = new GameObject("Chip");
				piece.transform.SetParent(holder, false);
				piece.transform.position = position;
				piece.transform.localScale = new Vector3(settings.ImpactSize, settings.ImpactSize, settings.ImpactSize);
				piece.transform.localRotation = Quaternion.Euler(settings.ImpactEulerStep * at);

				piece.AddComponent<MeshFilter>().sharedMesh = mesh;
				piece.AddComponent<MeshRenderer>().sharedMaterial = skin;

				// 부채꼴로 흩어짐. 위로 살짝 들려야 바닥에 안 묻힘
				float angle = (at + 0.5f) * Mathf.PI * 2f / settings.ImpactCount;
				Vector3 way = new Vector3(Mathf.Cos(angle), settings.ImpactLift, Mathf.Sin(angle)).normalized;

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
						BattleVisualFactory.Kill(one.Piece.gameObject);
					}

					impacts.RemoveAt(at);
					continue;
				}

				// 처음이 빠르고 끝이 느림. 튀는 느낌은 감속에서
				one.Piece.position += one.Way * (1f - life) * delta;
				one.Piece.Rotate(Vector3.one, settings.ImpactSpinDegrees * delta, Space.Self);

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
					? head + settings.BoltTargetOffset
					: bolt.From + Vector3.right * settings.BoltMissDistance;

				bolt.Piece.position = Vector3.Lerp(bolt.From, target, progress);

				if (progress >= 1f)
				{
					BattleVisualFactory.Kill(bolt.Piece.gameObject);
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
	}
}
