using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 사진을 아군과 적 표시 객체로 투영
	///
	/// ★ 도형은 구역이 정한다 (visual.md 2). 잡몹은 정다면체 계단, 보스는 코어와 껍질 조각
	/// ★ 값 생성 금지. 사진에 있는 것만
	/// </summary>
	internal sealed class IdleBattleEntityPresenter
	{
		/// <summary>무대가 넘기는 값. 인스펙터 손잡이 그대로</summary>
		internal sealed class Settings
		{
			public GameObject DollPrefab { get; set; }
			public GameObject FoePrefab { get; set; }
			public GameObject BossPrefab { get; set; }
			public float LungeSeconds { get; set; } = 0.22f;
			public float PopSeconds { get; set; } = 0.3f;
			public float PositionCatchUp { get; set; } = 14f;
			public float FoeSpinDegrees { get; set; } = 12f;
			public float FoeBobHeight { get; set; } = 0.025f;
			public float BossScale { get; set; } = 1.9f;
			public float FoeHeight { get; set; } = 0.62f;
			public float FoeRadius { get; set; } = 0.62f;

			/// <summary>몇 구역마다 도형이 한 계단 오르나 (visual.md 2)</summary>
			public int ShapeStagesPerStep { get; set; } = 5;

			/// <summary>보스가 띄우는 껍질 조각 수</summary>
			public int BossShardCount { get; set; } = 6;

			/// <summary>껍질이 코어에서 떨어진 거리. 반지름 배수</summary>
			public float BossShellRadius { get; set; } = 1.35f;

			/// <summary>체력이 빌수록 껍질이 이만큼 더 벌어진다</summary>
			public float BossShellSpread { get; set; } = 0.9f;

			/// <summary>껍질이 도는 속도. 코어와 반대</summary>
			public float BossShellSpinDegrees { get; set; } = -26f;

			/// <summary>이 구역부터 보스 코어에 뿔을 세운다 (별 만들기)</summary>
			public int BossSpikeFromStage { get; set; } = 21;

			/// <summary>이 구역에서 채도가 최대. 그 너머는 더 안 짙어진다 (visual.md 6)</summary>
			public int ColorDepthStage { get; set; } = 40;

			/// <summary>깊이가 채도에 더하는 몫</summary>
			public float DepthSaturation { get; set; } = 0.35f;

			/// <summary>깊이가 밝기에서 빼는 몫</summary>
			public float DepthDarken { get; set; } = 0.18f;

			/// <summary>보스 발광 세기. 0 이면 안 빛난다</summary>
			public float BossGlow { get; set; } = 0.55f;

			/// <summary>근접은 낮게, 원거리는 높게 뜬다 (visual.md 3). 높이 배수</summary>
			public float MeleeHeightShare { get; set; } = 0.85f;
			public float RangedHeightShare { get; set; } = 1.3f;

			public Color MyColor { get; set; }
			public Color EnemyColor { get; set; }
			public Color RangedEnemyColor { get; set; }
			public Color BossColor { get; set; }
			public Color BarBackColor { get; set; }
			public Color AllyBarColor { get; set; }
			public Color ReviveBarColor { get; set; }
			public Color EnemyBarColor { get; set; }
			public Color[] GradeColors { get; set; }
		}

		private sealed class Foe
		{
			public Transform Piece;
			public Transform Model;
			public Transform BarAnchor;
			public MeshFilter Mesh;
			public Material Skin;
			public IdleHealthBar Bar;
			public long Index;
			public bool Boss;
			public IdleFoeKind Kind;

			/// <summary>지금 쓰고 있는 도형. 바뀔 때만 메시를 다시 만든다</summary>
			public int ShapeUsed = -1;

			/// <summary>색을 칠한 구역. 깊이가 바뀌면 다시 칠한다</summary>
			public int StageUsed = -1;

			/// <summary>보스 껍질. 코어를 감싸고 역방향으로 돈다</summary>
			public Transform Shell;
			public readonly List<Transform> Shards = new List<Transform>();
		}

		private readonly Transform worldRoot;
		private readonly Settings settings;
		private readonly List<Foe> foes = new List<Foe>();
		private readonly Dictionary<long, Vector3> removedHeads = new Dictionary<long, Vector3>();
		private readonly Transform[] dolls;
		private readonly Transform[] dollBarAnchors;
		private readonly Material[] dollSkins;
		private readonly IdleHealthBar[] dollBars;
		private readonly float[] attackLeft;
		private readonly float[] hurtLeft;
		private float clock;

		public IdleBattleEntityPresenter(Transform worldRoot, Settings settings)
		{
			this.worldRoot = worldRoot;
			this.settings = settings;

			int seats = IdleSquad.SEAT_COUNT;
			dolls = new Transform[seats];
			dollBarAnchors = new Transform[seats];
			dollSkins = new Material[seats];
			dollBars = new IdleHealthBar[seats];
			attackLeft = new float[seats];
			hurtLeft = new float[seats];

			for (int seat = 0; seat < seats; seat++)
			{
				dolls[seat] = BuildDoll(seat);
				dolls[seat].gameObject.SetActive(false);
			}
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			removedHeads.Clear();
			DressDolls(snapshot);
			DressFoes(snapshot, delta);
			AdvanceMotion(snapshot, delta);
		}

		public void PlayAllyAttack(int seat)
		{
			if (seat >= 0 && seat < attackLeft.Length)
			{
				attackLeft[seat] = settings.LungeSeconds;
			}
		}

		public void PlayAllyHit(int seat)
		{
			if (seat >= 0 && seat < hurtLeft.Length)
			{
				hurtLeft[seat] = settings.PopSeconds * 0.5f;
			}
		}

		public bool TryGetAllyHead(int seat, out Vector3 position)
		{
			position = Vector3.zero;

			if (seat < 0 || seat >= dolls.Length || dolls[seat] == null)
			{
				return false;
			}

			position = dolls[seat].position + new Vector3(0f, 1.3f, 0f);
			return true;
		}

		/// <summary>적 머리 위. 사라진 적이면 마지막 자리 (막타 숫자가 설 곳)</summary>
		public bool TryGetFoeHead(long index, out Vector3 position)
		{
			Foe foe = Find(index);

			if (foe != null)
			{
				position = foe.Piece.position + new Vector3(0f, 0.7f, 0f);
				return true;
			}

			return removedHeads.TryGetValue(index, out position);
		}

		/// <summary>맞은 자리와 그 적의 색. 충격 연출이 쓴다</summary>
		public bool TryGetFoeImpact(long index, out Vector3 position, out Color color)
		{
			Foe foe = Find(index);

			if (foe == null)
			{
				position = Vector3.zero;
				color = Color.white;
				return false;
			}

			position = foe.Piece.position;
			color = foe.Boss ? settings.BossColor : settings.EnemyColor;
			return true;
		}

		/// <summary>화면에서 찍은 자리에 가장 가까운 적. 카드 목표 지정용</summary>
		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex)
		{
			foeIndex = -1L;
			Camera eye = Camera.main;

			if (eye == null)
			{
				return false;
			}

			Vector2 screen = new Vector2(panelPosition.x, Screen.height - panelPosition.y);
			float best = 54f;

			foreach (Foe foe in foes)
			{
				Vector3 point = eye.WorldToScreenPoint(foe.Piece.position);
				if (point.z <= 0f)
				{
					continue;
				}

				float distance = Vector2.Distance(screen, new Vector2(point.x, point.y));
				if (distance < best)
				{
					best = distance;
					foeIndex = foe.Index;
				}
			}

			return foeIndex >= 0L;
		}

		private Transform BuildDoll(int seat)
		{
			GameObject doll = new GameObject("Doll" + seat);
			doll.transform.SetParent(worldRoot, false);
			doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);

			GameObject barAnchor = new GameObject("AllyHealthBarAnchor" + seat);
			barAnchor.transform.SetParent(worldRoot, false);
			dollBarAnchors[seat] = barAnchor.transform;

			Material skin = IdleBattleVisualFactory.MakeMaterial(
				seat == 0 ? settings.MyColor : settings.GradeColors[0]);
			dollSkins[seat] = skin;

			if (settings.DollPrefab != null)
			{
				GameObject made = Object.Instantiate(settings.DollPrefab, doll.transform, false);
				made.name = "Model";
				foreach (MeshRenderer part in made.GetComponentsInChildren<MeshRenderer>())
				{
					if (part.name == "Body" || part.name == "Head")
					{
						part.sharedMaterial = skin;
					}
				}
			}
			else
			{
				// 아군도 기하 언어. 둥근 쪽이라 각진 적과 대비 (visual.md 5)
				Mesh round = IdleGeometry.Build(IdleGeometry.Shape.SphereOnce, 0.5f);

				GameObject body = new GameObject("Body");
				body.transform.SetParent(doll.transform, false);
				body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
				body.transform.localScale = new Vector3(0.44f, 0.9f, 0.44f);
				body.AddComponent<MeshFilter>().sharedMesh = round;
				body.AddComponent<MeshRenderer>().sharedMaterial = skin;

				GameObject head = new GameObject("Head");
				head.transform.SetParent(doll.transform, false);
				head.transform.localPosition = new Vector3(0f, 1f, 0f);
				head.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
				head.AddComponent<MeshFilter>().sharedMesh = round;
				head.AddComponent<MeshRenderer>().sharedMaterial = skin;
			}

			dollBars[seat] = IdleHealthBar.Attach(barAnchor.transform, 1.45f, 0.9f, 0.11f,
				settings.BarBackColor, settings.AllyBarColor);
			return doll.transform;
		}

		private void DressDolls(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < dolls.Length && seat < snapshot.Seats.Length; seat++)
			{
				IdleSeatView view = snapshot.Seats[seat];

				if (dolls[seat].gameObject.activeSelf != view.Taken)
				{
					dolls[seat].gameObject.SetActive(view.Taken);
					dollBars[seat].SetVisible(view.Taken);
				}

				if (view.Taken == false)
				{
					continue;
				}

				if (view.HeroId >= 0)
				{
					int grade = Mathf.Clamp((int)view.Grade, 0, settings.GradeColors.Length - 1);
					dollSkins[seat].color = settings.GradeColors[grade];
				}

				if (view.Standing)
				{
					dollBars[seat].SetFillColor(settings.AllyBarColor);
					dollBars[seat].SetRatio((float)view.HealthRatio);
					dolls[seat].localRotation = Quaternion.LookRotation(Vector3.right);
					dolls[seat].localScale = Vector3.one;
				}
				else
				{
					dollBars[seat].SetFillColor(settings.ReviveBarColor);
					dollBars[seat].SetRatio((float)view.ReviveRatio);
					dolls[seat].localRotation = Quaternion.Euler(0f, 0f, -78f);
					dolls[seat].localScale = new Vector3(1f, 0.75f, 1f);
				}

				dollBarAnchors[seat].position = dolls[seat].position;
			}
		}

		private void DressFoes(IdleSnapshot snapshot, float delta)
		{
			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (IndexOf(snapshot.Foes, foes[at].Index) >= 0)
				{
					continue;
				}

				removedHeads[foes[at].Index] = foes[at].Piece.position + new Vector3(0f, 0.7f, 0f);
				Kill(foes[at].BarAnchor.gameObject);
				Kill(foes[at].Piece.gameObject);
				foes.RemoveAt(at);
			}

			IdleGeometry.Shape shape = IdleGeometry.ShapeOfStage(snapshot.Stage, settings.ShapeStagesPerStep);
			int stage = snapshot.Stage;

			for (int at = 0; at < snapshot.Foes.Length; at++)
			{
				IdleFoeView view = snapshot.Foes[at];
				Foe foe = Find(view.Index);

				if (foe == null)
				{
					foe = BuildFoe(view.Index, view.Boss);
					foes.Add(foe);
				}

				// 보스는 한 계단 위 도형. 같은 판의 잡몹과 구별
				IdleGeometry.Shape mine = view.Boss ? NextShape(shape) : shape;
				Reshape(foe, mine, view.Boss, snapshot.Stage);

				if (foe.Boss != view.Boss || foe.Kind != view.Kind || foe.StageUsed != stage)
				{
					foe.Boss = view.Boss;
					foe.Kind = view.Kind;
					foe.StageUsed = stage;
					Repaint(foe, stage);
				}

				// 근접은 낮게, 원거리는 높게. 도형이 같아도 종류가 읽힌다 (visual.md 3)
				float lift = settings.FoeHeight * (view.Boss
					? 1f
					: (view.Kind == IdleFoeKind.Ranged ? settings.RangedHeightShare : settings.MeleeHeightShare));

				foe.Piece.localPosition = Vector3.Lerp(
					foe.Piece.localPosition,
					new Vector3((float)view.X, lift, (float)view.Y),
					IdleBattleMotion.CatchUp(settings.PositionCatchUp, delta));

				float health = 0.82f + 0.18f * (float)view.HealthRatio;
				float bulk = view.Boss ? settings.BossScale : 1f;
				foe.Model.localScale = new Vector3(health * bulk, bulk, health * bulk);
				foe.BarAnchor.position = foe.Piece.position;
				foe.Bar.SetVisible(view.Boss == false);

				if (view.Boss == false)
				{
					foe.Bar.SetRatio((float)view.HealthRatio);
				}

				SpreadShell(foe, view, delta);
			}
		}

		/// <summary>
		/// 색을 다시. 종류색을 바탕으로 구역이 깊을수록 짙어진다 (visual.md 6)
		///
		/// ★ 보스만 발광. 잡몹까지 빛나면 위험 판독 불가
		/// </summary>
		private void Repaint(Foe foe, int stage)
		{
			Color basis = foe.Boss
				? settings.BossColor
				: (foe.Kind == IdleFoeKind.Ranged ? settings.RangedEnemyColor : settings.EnemyColor);

			Color made = Deepen(basis, stage);

			if (foe.Boss && settings.BossGlow > 0f)
			{
				foe.Skin.color = made;
				if (foe.Skin.HasProperty("_BaseColor"))
				{
					foe.Skin.SetColor("_BaseColor", made);
				}

				foe.Skin.EnableKeyword("_EMISSION");
				foe.Skin.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
				if (foe.Skin.HasProperty("_EmissionColor"))
				{
					foe.Skin.SetColor("_EmissionColor", made * settings.BossGlow);
				}

				return;
			}

			foe.Skin.color = made;
			if (foe.Skin.HasProperty("_BaseColor"))
			{
				foe.Skin.SetColor("_BaseColor", made);
			}
		}

		/// <summary>구역이 깊을수록 채도는 오르고 밝기는 내린다. 상한에서 멎는다</summary>
		private Color Deepen(Color basis, int stage)
		{
			int span = settings.ColorDepthStage > 1 ? settings.ColorDepthStage - 1 : 1;
			float depth = Mathf.Clamp01((stage - 1) / (float)span);

			Color.RGBToHSV(basis, out float hue, out float saturation, out float value);
			saturation = Mathf.Clamp01(saturation + depth * settings.DepthSaturation);
			value = Mathf.Clamp01(value - depth * settings.DepthDarken);
			return Color.HSVToRGB(hue, saturation, value);
		}

		/// <summary>한 계단 위 도형. 끝에 닿으면 그대로</summary>
		private static IdleGeometry.Shape NextShape(IdleGeometry.Shape shape)
		{
			int next = (int)shape + 1;
			return (IdleGeometry.Shape)(next >= IdleGeometry.SHAPE_COUNT ? IdleGeometry.SHAPE_COUNT - 1 : next);
		}

		/// <summary>도형이 바뀌었으면 메시를 다시. 안 바뀌었으면 아무것도 안 한다</summary>
		private void Reshape(Foe foe, IdleGeometry.Shape shape, bool boss, int stage)
		{
			if (foe.Mesh == null)
			{
				return;
			}

			int want = (int)shape + (boss ? 100 : 0) + (boss && stage >= settings.BossSpikeFromStage ? 1000 : 0);
			if (foe.ShapeUsed == want)
			{
				return;
			}

			foe.ShapeUsed = want;

			foe.Mesh.sharedMesh = boss && stage >= settings.BossSpikeFromStage
				? IdleGeometry.Stellate(shape, settings.FoeRadius, 0.45f)
				: IdleGeometry.Build(shape, settings.FoeRadius);

			if (boss)
			{
				BuildShell(foe, shape);
			}
		}

		/// <summary>보스 껍질. 코어 도형의 면을 뜯어낸 판이 코어를 감싼다 (visual.md 4)</summary>
		private void BuildShell(Foe foe, IdleGeometry.Shape shape)
		{
			if (foe.Shell == null)
			{
				GameObject shell = new GameObject("Shell");
				shell.transform.SetParent(foe.Model, false);
				foe.Shell = shell.transform;
			}

			foreach (Transform shard in foe.Shards)
			{
				if (shard != null)
				{
					Kill(shard.gameObject);
				}
			}

			foe.Shards.Clear();

			int faces = IdleGeometry.FaceCountOf(shape);
			int count = Mathf.Clamp(settings.BossShardCount, 1, faces);

			for (int at = 0; at < count; at++)
			{
				GameObject shard = new GameObject("Shard" + at);
				shard.transform.SetParent(foe.Shell, false);

				MeshFilter mesh = shard.AddComponent<MeshFilter>();
				mesh.sharedMesh = IdleGeometry.FaceShard(shape, settings.FoeRadius * 0.5f,
					at * Mathf.Max(1, faces / count), 0.06f);

				MeshRenderer renderer = shard.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = foe.Skin;

				// 조각을 코어 둘레에 고르게. 위아래로도 흔들어 판 하나가 안 되게
				float angle = at * Mathf.PI * 2f / count;
				float lift = ((at % 3) - 1) * 0.3f;
				shard.transform.localPosition = new Vector3(Mathf.Cos(angle), lift, Mathf.Sin(angle));
				shard.transform.localRotation = Quaternion.Euler(at * 37f, at * 61f, at * 23f);

				foe.Shards.Add(shard.transform);
			}
		}

		/// <summary>체력이 빌수록 껍질이 벌어진다. 다 벌어지면 코어가 드러난다</summary>
		private void SpreadShell(Foe foe, IdleFoeView view, float delta)
		{
			if (foe.Shell == null || foe.Shards.Count == 0)
			{
				return;
			}

			float hurt = 1f - Mathf.Clamp01((float)view.HealthRatio);
			float radius = settings.BossShellRadius + settings.BossShellSpread * hurt;

			for (int at = 0; at < foe.Shards.Count; at++)
			{
				Transform shard = foe.Shards[at];
				float angle = at * Mathf.PI * 2f / foe.Shards.Count;
				float lift = ((at % 3) - 1) * 0.3f;
				Vector3 want = new Vector3(Mathf.Cos(angle), lift, Mathf.Sin(angle)) * radius;
				shard.localPosition = Vector3.Lerp(shard.localPosition, want,
					IdleBattleMotion.CatchUp(settings.PositionCatchUp * 0.4f, delta));
				shard.Rotate(Vector3.up, settings.BossShellSpinDegrees * 0.5f * delta, Space.Self);
			}

			foe.Shell.Rotate(Vector3.up, settings.BossShellSpinDegrees * delta, Space.Self);
		}

		private Foe BuildFoe(long index, bool boss)
		{
			GameObject piece = new GameObject("Foe" + index);
			piece.transform.SetParent(worldRoot, false);

			GameObject model = new GameObject("ModelPivot");
			model.transform.SetParent(piece.transform, false);

			GameObject barAnchor = new GameObject("HealthBarAnchor");
			barAnchor.transform.SetParent(worldRoot, false);

			Foe foe = new Foe
			{
				Piece = piece.transform,
				Model = model.transform,
				BarAnchor = barAnchor.transform,
				Skin = IdleBattleVisualFactory.MakeMaterial(settings.EnemyColor),
				Index = index,
				Kind = IdleFoeKind.Melee,
			};

			GameObject source = boss && settings.BossPrefab != null ? settings.BossPrefab : settings.FoePrefab;

			if (source != null)
			{
				GameObject made = Object.Instantiate(source, model.transform, false);
				made.name = "Model";
				MeshRenderer part = made.GetComponentInChildren<MeshRenderer>();
				if (part != null)
				{
					part.sharedMaterial = foe.Skin;
				}
			}
			else
			{
				foe.Mesh = model.AddComponent<MeshFilter>();
				MeshRenderer renderer = model.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = foe.Skin;
			}

			foe.Bar = IdleHealthBar.Attach(barAnchor.transform, 0.95f, 0.8f, 0.1f,
				settings.BarBackColor, settings.EnemyBarColor);
			return foe;
		}

		private void AdvanceMotion(IdleSnapshot snapshot, float delta)
		{
			clock += delta;

			for (int seat = 0; seat < dolls.Length; seat++)
			{
				attackLeft[seat] = Mathf.Max(0f, attackLeft[seat] - delta);
				hurtLeft[seat] = Mathf.Max(0f, hurtLeft[seat] - delta);

				float attack = settings.LungeSeconds > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - attackLeft[seat] / settings.LungeSeconds) * Mathf.PI)
					: 0f;
				float hurt = settings.PopSeconds > 0f
					? Mathf.Sin(Mathf.Clamp01(1f - hurtLeft[seat] / (settings.PopSeconds * 0.5f)) * Mathf.PI)
					: 0f;

				bool walking = seat < snapshot.Fighters.Length && snapshot.Fighters[seat].Moving;
				float x = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].X : 0f;
				float y = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].Y : 0f;
				float bob = walking ? IdleBattleMotion.WalkBob(clock, seat, 0.1f) : 0f;

				dolls[seat].localPosition = Vector3.Lerp(
					dolls[seat].localPosition,
					new Vector3(x + attack * 0.3f - hurt * 0.08f, bob, y),
					IdleBattleMotion.CatchUp(settings.PositionCatchUp, delta));
			}

			for (int index = 0; index < foes.Count; index++)
			{
				Foe foe = foes[index];
				foe.Model.Rotate(Vector3.up, settings.FoeSpinDegrees * delta, Space.Self);

				Vector3 position = foe.Model.localPosition;
				position.y = IdleBattleMotion.FoeBob(clock, index, settings.FoeBobHeight);
				foe.Model.localPosition = position;
			}
		}

		private static int IndexOf(IdleFoeView[] views, long index)
		{
			for (int at = 0; at < views.Length; at++)
			{
				if (views[at].Index == index)
				{
					return at;
				}
			}

			return -1;
		}

		private Foe Find(long index)
		{
			foreach (Foe foe in foes)
			{
				if (foe.Index == index)
				{
					return foe;
				}
			}

			return null;
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
