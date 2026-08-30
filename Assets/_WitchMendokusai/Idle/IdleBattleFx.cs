using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 사건의 볼트, 피해 숫자, 충격, 화면 흔들림
	///
	/// ★ 근거는 사진의 <see cref="IdleHit"/> 뿐. 연출이 스스로 박자를 세면 숫자와 체력이 어긋남
	/// ★ 한 프레임의 타격은 대상별로 합쳐 숫자 하나로. 초당 수십 타면 낱개는 판독 불가
	/// </summary>
	internal sealed class IdleBattleFx
	{
		internal sealed class Settings
		{
			public float BoltSeconds { get; set; } = 0.18f;
			public float ShakeSeconds { get; set; } = 0.08f;
			public float ShakeDistance { get; set; } = 0.025f;
			public float NumberSeconds { get; set; } = 0.8f;
			public float NumberRise { get; set; } = 1.2f;
			public float NumberSize { get; set; } = 0.12f;
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
			public TextMesh Text;
			public float Age;
		}

		private readonly Transform holder;
		private readonly Settings settings;
		private readonly List<Bolt> bolts = new List<Bolt>();
		private readonly List<Number> numbers = new List<Number>();
		private readonly Dictionary<long, double> dealtByFoe = new Dictionary<long, double>();
		private readonly Dictionary<int, double> takenBySeat = new Dictionary<int, double>();
		private float shakeLeft;
		private float clock;

		public IdleBattleFx(Transform holder, Settings settings)
		{
			this.holder = holder;
			this.settings = settings;
		}

		/// <summary>이번 프레임의 타격을 연출로. 대상별 합산 숫자 하나, 자리당 볼트 하나</summary>
		public void Consume(IdleHit[] hits, IdleBattleEntityPresenter entities)
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
					SpawnNumber(head, Numerics.BigNumberText.Format(pair.Value), settings.NumberColor);
				}

				if (entities.TryGetFoeImpact(pair.Key, out Vector3 impact, out Color color))
				{
					shakeLeft = Mathf.Max(shakeLeft, settings.ShakeSeconds);
					SpawnImpact(impact, color);
				}
			}

			foreach (KeyValuePair<int, double> pair in takenBySeat)
			{
				if (entities.TryGetAllyHead(pair.Key, out Vector3 head))
				{
					SpawnNumber(head, "-" + Numerics.BigNumberText.Format(pair.Value), settings.HurtColor);
				}
			}
		}

		public void Advance(float delta, IdleBattleEntityPresenter entities)
		{
			clock += delta;

			if (shakeLeft > 0f)
			{
				shakeLeft -= delta;
				float left = settings.ShakeSeconds > 0f ? Mathf.Clamp01(shakeLeft / settings.ShakeSeconds) : 0f;
				holder.localPosition = IdleBattleMotion.Shake(clock, settings.ShakeDistance, left);
			}
			else
			{
				holder.localPosition = Vector3.zero;
			}

			AdvanceBolts(delta, entities);
			AdvanceNumbers(delta);
		}

		private void SpawnBolt(Vector3 from, long target)
		{
			GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			piece.name = "Bolt";
			piece.transform.SetParent(holder, false);
			piece.transform.position = from;
			piece.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
			IdleBattleVisualFactory.Paint(piece, settings.BoltColor);

			bolts.Add(new Bolt { Piece = piece.transform, From = from, Target = target });
		}

		private void SpawnNumber(Vector3 position, string value, Color color)
		{
			GameObject piece = new GameObject("Damage");
			piece.transform.SetParent(holder, false);
			piece.transform.position = position;

			TextMesh text = piece.AddComponent<TextMesh>();
			text.text = value;
			text.fontSize = 48;
			text.characterSize = settings.NumberSize;
			text.anchor = TextAnchor.MiddleCenter;
			text.alignment = TextAlignment.Center;
			text.color = color;
			text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
			piece.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;

			numbers.Add(new Number { Text = text });
		}

		/// <summary>맞은 자리에 알갱이 넷. 도형과 같은 색이라 누가 맞았는지 읽힌다</summary>
		private void SpawnImpact(Vector3 position, Color color)
		{
			GameObject piece = new GameObject("Impact");
			piece.transform.SetParent(holder, false);
			piece.transform.position = position;

			ParticleSystem particles = piece.AddComponent<ParticleSystem>();
			ParticleSystem.MainModule main = particles.main;
			main.duration = 0.16f;
			main.startLifetime = 0.16f;
			main.startSpeed = 1.2f;
			main.startSize = 0.045f;
			main.startColor = color;
			main.maxParticles = 6;

			ParticleSystem.EmissionModule emission = particles.emission;
			emission.rateOverTime = 0f;
			emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 4) });

			ParticleSystem.ShapeModule shape = particles.shape;
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = 0.05f;

			ParticleSystemRenderer renderer = piece.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Mesh;
			renderer.mesh = IdleBattleVisualFactory.BuildImpactMesh();
			renderer.sharedMaterial = IdleBattleVisualFactory.MakeMaterial(color);

			particles.Play();
			Object.Destroy(piece, 0.35f);
		}

		private void AdvanceBolts(float delta, IdleBattleEntityPresenter entities)
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
					Kill(number.Text.gameObject);
					numbers.RemoveAt(at);
					continue;
				}

				number.Text.transform.position += new Vector3(0f, settings.NumberRise * delta, 0f);

				Color color = number.Text.color;
				number.Text.color = new Color(color.r, color.g, color.b, 1f - life * life);

				if (eye != null)
				{
					number.Text.transform.rotation =
						Quaternion.LookRotation(number.Text.transform.position - eye.transform.position);
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
