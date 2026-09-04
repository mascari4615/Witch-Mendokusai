using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	/// <summary>적, 체력바, 도형과 보스 껍질 동작 표시</summary>
	internal sealed class BattleFoePresenter
	{
		private sealed class Foe
		{
			public Transform Piece;
			public Transform Model;
			public Transform BarAnchor;
			public MeshFilter Mesh;
			public Material Skin;
			public HealthBar Bar;
			public long Index;
			public bool Boss;
			public IdleFoeKind Kind;
			public Geometry.Shape ShapeUsed;
			public bool HasShape;
			public bool BossShapeUsed;
			public bool SpikedShapeUsed;
			public int StageUsed = -1;
			public float FlashLeft;
			public bool Entering;
			public Color RestColor = Color.white;
			public Transform Shell;
			public readonly List<Transform> Shards = new List<Transform>();
		}

		private readonly Transform worldRoot;
		private readonly BattleEntityPresenter.Settings settings;
		private readonly List<Foe> foes = new List<Foe>();
		private readonly Dictionary<long, Vector3> removedHeads = new Dictionary<long, Vector3>();
		private float clock;

		public BattleFoePresenter(Transform worldRoot, BattleEntityPresenter.Settings settings)
		{
			this.worldRoot = worldRoot;
			this.settings = settings;
		}

		public void Render(IdleSnapshot snapshot, float delta)
		{
			removedHeads.Clear();
			Dress(snapshot, delta);
			AdvanceMotion(delta);
		}

		public void PlayHit(long index)
		{
			Foe foe = Find(index);

			if (foe != null)
			{
				foe.FlashLeft = settings.FoeFlashSeconds;
			}
		}

		public bool TryGetHead(long index, out Vector3 position)
		{
			Foe foe = Find(index);

			if (foe != null)
			{
				position = foe.Piece.position + Vector3.up * settings.FoeHeadHeight;
				return true;
			}

			return removedHeads.TryGetValue(index, out position);
		}

		public bool TryGetImpact(long index, out Vector3 position, out Color color)
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

		public bool TryPick(Vector2 panelPosition, out long foeIndex)
		{
			foeIndex = -1L;
			Camera eye = Camera.main;

			if (eye == null)
			{
				return false;
			}

			Vector2 screen = new Vector2(panelPosition.x, Screen.height - panelPosition.y);
			float best = settings.FoePickRadius;

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

		private void Dress(IdleSnapshot snapshot, float delta)
		{
			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (IndexOf(snapshot.Foes, foes[at].Index) >= 0)
				{
					continue;
				}

				removedHeads[foes[at].Index] =
					foes[at].Piece.position + Vector3.up * settings.FoeHeadHeight;
				Kill(foes[at].BarAnchor.gameObject);
				Kill(foes[at].Piece.gameObject);
				foes.RemoveAt(at);
			}

			Geometry.Shape shape = Geometry.ShapeOfStage(snapshot.Stage, settings.ShapeStagesPerStep);
			int stage = snapshot.Stage;

			for (int at = 0; at < snapshot.Foes.Length; at++)
			{
				IdleFoeView view = snapshot.Foes[at];
				Foe foe = Find(view.Index);
				bool entering = foe == null;

				if (entering)
				{
					foe = Build(view.Index, view.Boss);
					foe.Entering = true;
					foes.Add(foe);
				}

				Geometry.Shape mine = view.Boss ? NextShape(shape) : shape;
				Reshape(foe, mine, view.Boss, snapshot.Stage);

				if (foe.Boss != view.Boss || foe.Kind != view.Kind || foe.StageUsed != stage)
				{
					foe.Boss = view.Boss;
					foe.Kind = view.Kind;
					foe.StageUsed = stage;
					Repaint(foe, stage);
				}

				float lift = settings.FoeHeight * (view.Boss
					? 1f
					: (view.Kind == IdleFoeKind.Ranged ? settings.RangedHeightShare : settings.MeleeHeightShare));

				Vector3 wanted = new Vector3((float)view.X, lift, (float)view.Y);
				if (entering)
				{
					foe.Piece.localPosition = wanted + Vector3.right * settings.FoeEntranceDistance;
				}

				if (foe.Entering)
				{
					foe.Piece.localPosition = Vector3.MoveTowards(
						foe.Piece.localPosition, wanted, settings.FoeEntranceSpeed * delta);
					foe.Entering = Vector3.Distance(foe.Piece.localPosition, wanted) >
						settings.FoeEntranceThreshold;
				}
				else
				{
					foe.Piece.localPosition = Vector3.Lerp(
						foe.Piece.localPosition, wanted,
						BattleMotion.CatchUp(settings.PositionCatchUp, delta));
				}

				float health = Mathf.Lerp(
					settings.FoeMinHealthScale,
					1f,
					(float)view.HealthRatio);
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

		private void Repaint(Foe foe, int stage)
		{
			Color basis = foe.Boss
				? settings.BossColor
				: (foe.Kind == IdleFoeKind.Ranged ? settings.RangedEnemyColor : settings.EnemyColor);
			Color made = Deepen(basis, stage);
			foe.RestColor = made;

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

		private Color Deepen(Color basis, int stage)
		{
			int span = settings.ColorDepthStage > 1 ? settings.ColorDepthStage - 1 : 1;
			float depth = Mathf.Clamp01((stage - 1) / (float)span);

			Color.RGBToHSV(basis, out float hue, out float saturation, out float value);
			saturation = Mathf.Clamp01(saturation + depth * settings.DepthSaturation);
			value = Mathf.Clamp01(value - depth * settings.DepthDarken);
			return Color.HSVToRGB(hue, saturation, value);
		}

		private static Geometry.Shape NextShape(Geometry.Shape shape)
		{
			int next = (int)shape + 1;
			return (Geometry.Shape)(next >= Geometry.SHAPE_COUNT ? Geometry.SHAPE_COUNT - 1 : next);
		}

		private void Reshape(Foe foe, Geometry.Shape shape, bool boss, int stage)
		{
			if (foe.Mesh == null)
			{
				return;
			}

			bool spiked = boss && stage >= settings.BossSpikeFromStage;
			if (foe.HasShape && foe.ShapeUsed == shape && foe.BossShapeUsed == boss &&
				foe.SpikedShapeUsed == spiked)
			{
				return;
			}

			foe.HasShape = true;
			foe.ShapeUsed = shape;
			foe.BossShapeUsed = boss;
			foe.SpikedShapeUsed = spiked;
			foe.Mesh.sharedMesh = spiked
				? Geometry.Stellate(shape, settings.FoeRadius, settings.BossSpikeInset)
				: Geometry.Build(shape, settings.FoeRadius);

			if (boss)
			{
				BuildShell(foe, shape);
			}
		}

		private void BuildShell(Foe foe, Geometry.Shape shape)
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
			int faces = Geometry.FaceCountOf(shape);
			int count = Mathf.Clamp(settings.BossShardCount, 1, faces);

			for (int at = 0; at < count; at++)
			{
				GameObject shard = new GameObject("Shard" + at);
				shard.transform.SetParent(foe.Shell, false);

				MeshFilter mesh = shard.AddComponent<MeshFilter>();
				mesh.sharedMesh = Geometry.FaceShard(
					shape,
					settings.FoeRadius * settings.BossShardRadiusScale,
					at * Mathf.Max(1, faces / count),
					settings.BossShardThickness);
				MeshRenderer renderer = shard.AddComponent<MeshRenderer>();
				renderer.sharedMaterial = foe.Skin;

				float angle = at * Mathf.PI * 2f / count;
				float lift = ((at % 3) - 1) * settings.BossShardLift;
				shard.transform.localPosition = new Vector3(Mathf.Cos(angle), lift, Mathf.Sin(angle));
				shard.transform.localRotation = Quaternion.Euler(settings.BossShardEulerStep * at);
				foe.Shards.Add(shard.transform);
			}
		}

		private void SpreadShell(Foe foe, IdleFoeView view, float delta)
		{
			if (foe.Shell == null || foe.Shards.Count == 0)
			{
				return;
			}

			float health = Mathf.Clamp01((float)view.HealthRatio);
			float hurt = 1f - health;
			float radius = settings.BossShellRadius + settings.BossShellSpread * hurt;
			int alive = Mathf.Clamp(Mathf.CeilToInt(foe.Shards.Count * health), 1, foe.Shards.Count);

			for (int at = 0; at < foe.Shards.Count; at++)
			{
				Transform shard = foe.Shards[at];
				bool onShell = at < alive;

				if (shard.gameObject.activeSelf != onShell)
				{
					shard.gameObject.SetActive(onShell);
				}

				if (onShell == false)
				{
					continue;
				}

				float angle = at * Mathf.PI * 2f / alive;
				float lift = ((at % 3) - 1) * settings.BossShardLift;
				Vector3 want = new Vector3(Mathf.Cos(angle), lift, Mathf.Sin(angle)) * radius;
				shard.localPosition = Vector3.Lerp(shard.localPosition, want,
					BattleMotion.CatchUp(settings.PositionCatchUp * settings.BossShellCatchUpShare, delta));
				shard.Rotate(
					Vector3.up,
					settings.BossShellSpinDegrees * settings.BossShardSpinShare * delta,
					Space.Self);
			}

			foe.Shell.Rotate(Vector3.up, settings.BossShellSpinDegrees * delta, Space.Self);
		}

		private Foe Build(long index, bool boss)
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
				Skin = BattleVisualFactory.MakeMaterial(settings.EnemyColor),
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

			foe.Bar = HealthBar.Attach(barAnchor.transform,
				settings.FoeBarHeight, settings.FoeBarWidth, settings.FoeBarThickness,
				settings.BarBackColor, settings.EnemyBarColor);
			return foe;
		}

		private void AdvanceMotion(float delta)
		{
			clock += delta;

			for (int index = 0; index < foes.Count; index++)
			{
				Foe foe = foes[index];
				foe.Model.Rotate(Vector3.up, settings.FoeSpinDegrees * delta, Space.Self);
				AdvanceFlash(foe, delta);

				Vector3 position = foe.Model.localPosition;
				position.y = BattleMotion.FoeBob(clock, index, settings.FoeBobHeight);
				foe.Model.localPosition = position;
			}
		}

		private void AdvanceFlash(Foe foe, float delta)
		{
			if (foe.FlashLeft <= 0f)
			{
				return;
			}

			foe.FlashLeft -= delta;
			float share = Mathf.Clamp01(foe.FlashLeft / settings.FoeFlashSeconds);
			Color made = Color.Lerp(foe.RestColor, Color.white, share * settings.FoeFlashWhiten);

			foe.Skin.color = made;
			if (foe.Skin.HasProperty("_BaseColor"))
			{
				foe.Skin.SetColor("_BaseColor", made);
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
