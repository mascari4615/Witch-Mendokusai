using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>전투 스냅샷을 아군과 적 표시 객체로 투영한다.</summary>
	internal sealed class IdleBattleEntityPresenter
	{
		internal sealed class Settings
		{
			public Settings(GameObject dollPrefab, GameObject foePrefab, GameObject bossPrefab, float lungeSeconds, float popSeconds, float positionCatchUp, float foeSpinDegrees, float foeBobHeight, float bossScale, float foeHeight, Color myColor, Color enemyColor, Color rangedEnemyColor, Color bossColor, Color barBackColor, Color allyBarColor, Color reviveBarColor, Color enemyBarColor, Color[] gradeColors)
			{
				DollPrefab = dollPrefab; FoePrefab = foePrefab; BossPrefab = bossPrefab; LungeSeconds = lungeSeconds; PopSeconds = popSeconds; PositionCatchUp = positionCatchUp; FoeSpinDegrees = foeSpinDegrees; FoeBobHeight = foeBobHeight; BossScale = bossScale; FoeHeight = foeHeight; MyColor = myColor; EnemyColor = enemyColor; RangedEnemyColor = rangedEnemyColor; BossColor = bossColor; BarBackColor = barBackColor; AllyBarColor = allyBarColor; ReviveBarColor = reviveBarColor; EnemyBarColor = enemyBarColor; GradeColors = gradeColors;
			}
			public GameObject DollPrefab { get; }
			public GameObject FoePrefab { get; }
			public GameObject BossPrefab { get; }
			public float LungeSeconds { get; }
			public float PopSeconds { get; }
			public float PositionCatchUp { get; }
			public float FoeSpinDegrees { get; }
			public float FoeBobHeight { get; }
			public float BossScale { get; }
			public float FoeHeight { get; }
			public Color MyColor { get; }
			public Color EnemyColor { get; }
			public Color RangedEnemyColor { get; }
			public Color BossColor { get; }
			public Color BarBackColor { get; }
			public Color AllyBarColor { get; }
			public Color ReviveBarColor { get; }
			public Color EnemyBarColor { get; }
			public Color[] GradeColors { get; }
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
			public int Sides;
			public bool Boss;
			public IdleFoeKind Kind;
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
			dolls = new Transform[seats]; dollBarAnchors = new Transform[seats]; dollSkins = new Material[seats]; dollBars = new IdleHealthBar[seats]; attackLeft = new float[seats]; hurtLeft = new float[seats];
			for (int seat = 0; seat < seats; seat++) { dolls[seat] = BuildDoll(seat); dolls[seat].gameObject.SetActive(false); }
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
			if (seat >= 0 && seat < attackLeft.Length) { attackLeft[seat] = settings.LungeSeconds; }
		}

		public void PlayAllyHit(int seat)
		{
			if (seat >= 0 && seat < hurtLeft.Length) { hurtLeft[seat] = settings.PopSeconds * 0.5f; }
		}

		public bool TryGetAllyHead(int seat, out Vector3 position)
		{
			if (seat >= 0 && seat < dolls.Length && dolls[seat].gameObject.activeSelf) { position = dolls[seat].position + new Vector3(0f, 1.3f, 0f); return true; }
			position = Vector3.zero; return false;
		}

		public bool TryGetFoeHead(long index, out Vector3 position)
		{
			Foe foe = Find(index);
			if (foe != null) { position = foe.Piece.position + new Vector3(0f, 0.7f, 0f); return true; }
			return removedHeads.TryGetValue(index, out position);
		}

		public bool TryGetFoeImpact(long index, out Vector3 position, out Color color)
		{
			Foe foe = Find(index);
			if (foe == null) { position = Vector3.zero; color = Color.white; return false; }
			position = foe.Piece.position; color = foe.Boss ? settings.BossColor : settings.EnemyColor; return true;
		}

		public bool TryPickFoe(Vector2 panelPosition, out long foeIndex)
		{
			foeIndex = -1L; Camera eye = Camera.main;
			if (eye == null) { return false; }
			Vector2 screen = new Vector2(panelPosition.x, Screen.height - panelPosition.y); float best = 54f;
			foreach (Foe foe in foes)
			{
				Vector3 point = eye.WorldToScreenPoint(foe.Piece.position);
				if (point.z > 0f)
				{
					float distance = Vector2.Distance(screen, new Vector2(point.x, point.y));
					if (distance < best) { best = distance; foeIndex = foe.Index; }
				}
			}
			return foeIndex >= 0L;
		}

		private Transform BuildDoll(int seat)
		{
			GameObject doll = new GameObject("Doll" + seat); doll.transform.SetParent(worldRoot, false); doll.transform.localRotation = Quaternion.LookRotation(Vector3.right);
			GameObject barAnchor = new GameObject("DollHealthBarAnchor" + seat); barAnchor.transform.SetParent(worldRoot, false); dollBarAnchors[seat] = barAnchor.transform;
			Material skin = IdleBattleVisualFactory.MakeMaterial(seat == 0 ? settings.MyColor : settings.GradeColors[0]); dollSkins[seat] = skin;
			if (settings.DollPrefab != null)
			{
				GameObject made = Object.Instantiate(settings.DollPrefab, doll.transform, false); made.name = "Model";
				foreach (MeshRenderer part in made.GetComponentsInChildren<MeshRenderer>()) { if (part.name == "Body" || part.name == "Head") { part.sharedMaterial = skin; } }
			}
			else
			{
				GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(doll.transform, false); body.transform.localPosition = new Vector3(0f, 0.35f, 0f); body.transform.localScale = new Vector3(0.42f, 0.35f, 0.42f); body.GetComponent<MeshRenderer>().sharedMaterial = skin;
				GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.transform.SetParent(doll.transform, false); head.transform.localPosition = new Vector3(0f, 0.95f, 0f); head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f); head.GetComponent<MeshRenderer>().sharedMaterial = skin;
			}
			dollBars[seat] = IdleHealthBar.Attach(barAnchor.transform, 1.45f, 0.9f, 0.11f, settings.BarBackColor, settings.AllyBarColor);
			return doll.transform;
		}

		private void DressDolls(IdleSnapshot snapshot)
		{
			for (int seat = 0; seat < dolls.Length && seat < snapshot.Seats.Length; seat++)
			{
				IdleSeatView view = snapshot.Seats[seat]; dolls[seat].gameObject.SetActive(view.Taken);
				if (view.Taken == false) { dollBarAnchors[seat].gameObject.SetActive(false); continue; }
				dollBarAnchors[seat].gameObject.SetActive(true); dollBarAnchors[seat].position = dolls[seat].position;
				if (view.HeroId >= 0) { dollSkins[seat].color = settings.GradeColors[Mathf.Clamp((int)view.Grade, 0, settings.GradeColors.Length - 1)]; }
				if (view.Standing) { dollBars[seat].SetFillColor(settings.AllyBarColor); dollBars[seat].SetRatio((float)view.HealthRatio); dolls[seat].localRotation = Quaternion.LookRotation(Vector3.right); dolls[seat].localScale = Vector3.one; }
				else { dollBars[seat].SetFillColor(settings.ReviveBarColor); dollBars[seat].SetRatio((float)view.ReviveRatio); dolls[seat].localRotation = Quaternion.Euler(0f, 0f, -78f); dolls[seat].localScale = new Vector3(1f, 0.75f, 1f); }
			}
		}

		private void DressFoes(IdleSnapshot snapshot, float delta)
		{
			for (int at = foes.Count - 1; at >= 0; at--)
			{
				if (IndexOf(snapshot.Foes, foes[at].Index) < 0) { removedHeads[foes[at].Index] = foes[at].Piece.position + new Vector3(0f, 0.7f, 0f); Kill(foes[at].BarAnchor.gameObject); Kill(foes[at].Piece.gameObject); foes.RemoveAt(at); }
			}
			for (int at = 0; at < snapshot.Foes.Length; at++)
			{
				IdleFoeView view = snapshot.Foes[at]; Foe foe = Find(view.Index);
				if (foe == null) { foe = BuildFoe(view.Index, view.Boss); foes.Add(foe); }
				int sides = Mathf.Max(3, snapshot.MaxTierNow + 2);
				if (foe.Mesh != null && foe.Sides != sides) { foe.Sides = sides; foe.Mesh.sharedMesh = IdleBattleVisualFactory.NgonPrism(sides, 0.62f, 0.95f); }
				if (foe.Boss != view.Boss || foe.Kind != view.Kind) { foe.Boss = view.Boss; foe.Kind = view.Kind; foe.Skin.color = view.Boss ? settings.BossColor : view.Kind == IdleFoeKind.Ranged ? settings.RangedEnemyColor : settings.EnemyColor; }
				foe.Piece.localPosition = Vector3.Lerp(foe.Piece.localPosition, new Vector3((float)view.X, settings.FoeHeight, (float)view.Y), IdleBattleMotion.CatchUp(settings.PositionCatchUp, delta));
				float health = 0.82f + 0.18f * (float)view.HealthRatio; float bulk = view.Boss ? settings.BossScale : 1f; foe.Model.localScale = new Vector3(health * bulk, bulk, health * bulk); foe.BarAnchor.position = foe.Piece.position; foe.Bar.SetVisible(view.Boss == false); if (view.Boss == false) { foe.Bar.SetRatio((float)view.HealthRatio); }
			}
		}

		private Foe BuildFoe(long index, bool boss)
		{
			GameObject piece = new GameObject("Foe" + index); piece.transform.SetParent(worldRoot, false); GameObject model = new GameObject("ModelPivot"); model.transform.SetParent(piece.transform, false); GameObject barAnchor = new GameObject("HealthBarAnchor"); barAnchor.transform.SetParent(worldRoot, false);
			Foe foe = new Foe { Piece = piece.transform, Model = model.transform, BarAnchor = barAnchor.transform, Skin = IdleBattleVisualFactory.MakeMaterial(settings.EnemyColor), Index = index, Sides = -1, Kind = IdleFoeKind.Melee };
			GameObject source = boss && settings.BossPrefab != null ? settings.BossPrefab : settings.FoePrefab;
			if (source != null) { GameObject made = Object.Instantiate(source, model.transform, false); made.name = "Model"; MeshRenderer part = made.GetComponentInChildren<MeshRenderer>(); if (part != null) { part.sharedMaterial = foe.Skin; } foe.Sides = int.MaxValue; }
			else { foe.Mesh = model.AddComponent<MeshFilter>(); MeshRenderer renderer = model.AddComponent<MeshRenderer>(); renderer.sharedMaterial = foe.Skin; }
			foe.Bar = IdleHealthBar.Attach(barAnchor.transform, 0.95f, 0.8f, 0.1f, settings.BarBackColor, settings.EnemyBarColor); return foe;
		}

		private void AdvanceMotion(IdleSnapshot snapshot, float delta)
		{
			clock += delta;
			for (int seat = 0; seat < dolls.Length; seat++)
			{
				attackLeft[seat] = Mathf.Max(0f, attackLeft[seat] - delta); hurtLeft[seat] = Mathf.Max(0f, hurtLeft[seat] - delta);
				float attack = settings.LungeSeconds > 0f ? Mathf.Sin(Mathf.Clamp01(1f - attackLeft[seat] / settings.LungeSeconds) * Mathf.PI) : 0f; float hurt = settings.PopSeconds > 0f ? Mathf.Sin(Mathf.Clamp01(1f - hurtLeft[seat] / (settings.PopSeconds * 0.5f)) * Mathf.PI) : 0f;
				bool walking = seat < snapshot.Fighters.Length && snapshot.Fighters[seat].Moving; float x = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].X : 0f; float y = seat < snapshot.Fighters.Length ? (float)snapshot.Fighters[seat].Y : 0f; float bob = walking ? IdleBattleMotion.WalkBob(clock, seat, 0.1f) : 0f;
				dolls[seat].localPosition = Vector3.Lerp(dolls[seat].localPosition, new Vector3(x + attack * 0.3f - hurt * 0.08f, bob, y), IdleBattleMotion.CatchUp(settings.PositionCatchUp, delta));
			}
			for (int index = 0; index < foes.Count; index++) { Foe foe = foes[index]; foe.Model.Rotate(Vector3.up, settings.FoeSpinDegrees * delta, Space.Self); Vector3 position = foe.Model.localPosition; position.y = IdleBattleMotion.FoeBob(clock, index, settings.FoeBobHeight); foe.Model.localPosition = position; }
		}

		private static int IndexOf(IdleFoeView[] views, long index) { for (int at = 0; at < views.Length; at++) { if (views[at].Index == index) { return at; } } return -1; }
		private Foe Find(long index) { foreach (Foe foe in foes) { if (foe.Index == index) { return foe; } } return null; }
		private static void Kill(GameObject piece) { if (Application.isPlaying) { Object.Destroy(piece); } else { Object.DestroyImmediate(piece); } }
	}
}
