using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;
using Random = System.Random;

namespace WitchMendokusai
{
	// 갱도. 층마다 바위를 다시 놓고, 바위에 도는 빛에 맞춰 침
	public sealed partial class VillageStage
	{
		[Header("Mine")]
		[SerializeField] private float mineRadius = 9f;
		[SerializeField] private float mineNodeGap = 2.6f;
		[SerializeField] private float mineLampRange = 8f;
		[Tooltip("이보다 멀어지면 (끼임 탈출 등) 갱도에서 나온 것으로 침 (m)")]
		[SerializeField] private float mineLeaveDistance = 60f;
		[SerializeField] private float mineLampIntensity = 3f;
		[SerializeField] private Vector3 lanternOffset = new(0.3f, 1.8f, -0.6f);
		[SerializeField] private Color[] mineColors =
		{
			new(0.86f, 0.76f, 0.66f), new(0.92f, 0.72f, 0.58f), new(0.74f, 0.76f, 0.82f), new(0.94f, 0.84f, 0.56f), new(0.78f, 0.72f, 0.96f), new(0.60f, 0.58f, 0.80f),
		};

		private sealed class NodeView
		{
			public GameObject Root;
			public GameObject Body;
			public Transform Glint;
		}

		private Transform mineRoot;
		private Transform mineFloor;
		private readonly List<NodeView> mineNodes = new();
		private GameObject minePassage;
		private Light mineLantern;
		private MineRun run;
		private float glintPhase;

		private void BuildMine()
		{
			mineRoot = new GameObject("Mine").transform;
			mineRoot.SetParent(transform, false);
			mineRoot.localPosition = mineOffset;
			VillageKit shell = new();
			shell.Prism(Art.Soil, new Color(0.74f, 0.66f, 0.72f), new Vector3(0f, -0.5f, 0f), new Vector3(mineRadius * 2.6f, 0.5f, mineRadius * 2.6f), 18);
			const int WALL_COUNT = 26;
			for (int i = 0; i < WALL_COUNT; i++)
			{
				float angle = i / (float)WALL_COUNT * Mathf.PI * 2f;
				float height = 2.4f + Mathf.PerlinNoise(i * 0.8f, 0.4f) * 2f;
				Vector3 foot = new(Mathf.Cos(angle) * (mineRadius + 1.4f), -0.3f, Mathf.Sin(angle) * (mineRadius + 1.4f));
				// 카메라 쪽 (남쪽) 벽은 낮게
				if (Mathf.Sin(angle) < -0.4f)
					height = 0.7f;
				float turn = -angle * Mathf.Rad2Deg + Mathf.PerlinNoise(i * 1.7f, 2.2f) * 50f;
				shell.Prism(Art.Stone, new Color(0.72f, 0.70f, 0.78f), foot, new Vector3(4.2f, height, 3.4f), 7, 0.72f, new Vector3(0f, turn, 0f));
				if (Mathf.Sin(angle) > -0.2f)
					shell.Prism(Art.Stone, new Color(0.5f, 0.48f, 0.58f), foot * 1.22f, new Vector3(5f, height + 2.4f, 4f), 6, 0.6f, new Vector3(0f, turn + 25f, 0f));
				shell.Solid(foot, new Vector3(2.8f, 4f, 2.8f), new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
			}
			shell.Box(Art.Wood, Color.white, new Vector3(0f, 0f, -6.6f), new Vector3(2f, 0.12f, 2f));
			for (int side = -1; side <= 1; side += 2)
				shell.Box(Art.Wood, Color.white, new Vector3(side * 1f, 0f, -5.8f), new Vector3(0.16f, 2.4f, 0.16f));
			shell.Box(Art.Wood, Color.white, new Vector3(0f, 2.3f, -5.8f), new Vector3(2.3f, 0.18f, 0.2f));
			shell.Box(Art.Glow, Color.white, new Vector3(0.8f, 1.7f, -5.95f), new Vector3(0.22f, 0.3f, 0.22f));
			shell.Build("MineShell", mineRoot, Vector3.zero, 0f);
			BuildShade(mineRoot, mineRadius + 5f, 9f, 0.8f);
			AddGlow(mineRoot, new Vector3(0.8f, 2f, -6.2f), mineLampRange, mineLampIntensity);
			mineLantern = AddGlow(mineRoot, Vector3.zero, mineLampRange, mineLampIntensity);
			GameObject floor = new("MineFloor");
			floor.transform.SetParent(mineRoot, false);
			BoxCollider box = floor.AddComponent<BoxCollider>();
			box.center = new Vector3(0f, -0.5f, 0f);
			box.size = new Vector3(mineRadius * 3f, 1f, mineRadius * 3f);
			mineFloor = new GameObject("Nodes").transform;
			mineFloor.SetParent(mineRoot, false);
			HomesteadSpot.Create(mineRoot, new Vector3(0f, 0f, -6.4f), "LiftSpot", () => "승강기", () => "올라가기", LeaveMineByLift);
			VillageKit hole = new();
			hole.Prism(Art.Dark, Color.white, new Vector3(0f, -0.06f, 0f), new Vector3(1.6f, 0.1f, 1.6f), 10);
			hole.Prism(Art.Wood, Color.white, new Vector3(0.5f, 0f, 0.6f), new Vector3(0.12f, 0.8f, 0.12f), 6);
			hole.Prism(Art.Wood, Color.white, new Vector3(-0.5f, 0f, 0.6f), new Vector3(0.12f, 0.8f, 0.12f), 6);
			hole.Box(Art.Glow, Color.white, new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.03f, 0.9f), new Vector3(0f, 45f, 0f));
			minePassage = hole.Build("Passage", mineRoot, new Vector3(0f, 0f, 5.2f), 0f);
			HomesteadSpot.Attach(minePassage, () => "내리막", () => run != null && run.Depth >= Sim.MineMaxDepth ? "더는 없음" : "내려가기", Descend);
			minePassage.SetActive(false);
		}

		private void OpenLift()
		{
			manager.Panels.Mine.Ask(EnterMine);
			manager.Panels.Show(HomesteadPanelType.Mine);
		}

		private void EnterMine(int depth)
		{
			manager.Panels.ClosePanel();
			MovePlayer(mineOffset + new Vector3(0f, 0.1f, -4.6f), () =>
			{
				run = Sim.EnterMine(depth);
				manager.Hud.ShowMine(true);
				LayFloor();
			});
		}

		private void LeaveMineByLift()
		{
			Vector3 door = DoorPoint("quarry", 0.8f);
			MovePlayer(door + Vector3.up * 0.1f, LeaveMine);
			manager.Save();
		}

		private void LeaveMine()
		{
			run = null;
			if (manager != null && manager.Hud != null)
				manager.Hud.ShowMine(false);
			if (minePassage != null)
				minePassage.SetActive(false);
		}

		// 층의 바위를 새로 놓음. 자리는 층 번호로 정해져 같은 층은 같은 꼴
		private void LayFloor()
		{
			foreach (NodeView view in mineNodes)
				Destroy(view.Root);
			mineNodes.Clear();
			Random random = new(run.Depth * 7919 + 13);
			List<Vector2> placed = new();
			for (int i = 0; i < run.Nodes.Count; i++)
			{
				Vector2 point = Vector2.zero;
				for (int attempt = 0; attempt < 60; attempt++)
				{
					float angle = (float)random.NextDouble() * Mathf.PI * 2f;
					float radius = Mathf.Sqrt((float)random.NextDouble()) * (mineRadius - 2f);
					point = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
					bool clear = point.y > -3.4f && Vector2.Distance(point, new Vector2(0f, 5.2f)) > 2f && placed.Exists(other => Vector2.Distance(other, point) < mineNodeGap) == false;
					if (clear)
						break;
				}
				placed.Add(point);
				mineNodes.Add(MakeNode(i, new Vector3(point.x, 0f, point.y), (float)random.NextDouble() * 360f));
			}
			RefreshMine();
		}

		private NodeView MakeNode(int index, Vector3 position, float yaw)
		{
			MineNode node = run.Nodes[index];
			NodeView view = new() { Root = new GameObject("Node") };
			view.Root.transform.SetParent(mineFloor, false);
			view.Root.transform.localPosition = position;
			int band = Mathf.Clamp(Catalog.MineBands.IndexOf(run.Band), 0, mineColors.Length - 1);
			view.Body = NodeBody(node.Def, mineColors[band]).Build("Body", view.Root.transform, Vector3.zero, yaw);
			VillageKit glint = new();
			glint.Crystal(Art.Glow, Color.white, new Vector3(0f, -0.15f, 0f), new Vector3(0.22f, 0.3f, 0.22f), 4);
			view.Glint = glint.Build("Glint", view.Root.transform, new Vector3(0f, 0.8f, -0.6f), 0f, castShadow: false).transform;
			int captured = index;
			HomesteadSpot.Attach(view.Root, () => run.Nodes[captured].Def.Name, () => NodeRole(captured), () => StrikeNode(captured));
			return view;
		}

		private static VillageKit NodeBody(MineNodeDef def, Color rockColor)
		{
			VillageKit kit = new();
			kit.Prism(Art.Stone, rockColor, new Vector3(0f, -0.15f, 0f), new Vector3(1.5f, 1f, 1.3f), 6, 0.62f);
			kit.Prism(Art.Stone, rockColor * 0.9f, new Vector3(0.5f, -0.1f, 0.3f), new Vector3(0.9f, 0.6f, 0.8f), 5, 0.6f);
			Color vein = VeinColor(def.Id, out bool glows);
			Material material = glows ? Art.GlowPurple : Art.Gold;
			for (int i = 0; i < 4; i++)
			{
				float angle = i * 1.7f + 0.4f;
				kit.Crystal(material, vein, new Vector3(Mathf.Cos(angle) * 0.42f, 0.25f + (i % 2) * 0.25f, Mathf.Sin(angle) * 0.36f), new Vector3(0.26f, 0.42f, 0.26f), 5, new Vector3(Mathf.Sin(angle) * 35f, 0f, -Mathf.Cos(angle) * 35f));
			}
			kit.Solid(Vector3.zero, new Vector3(1.1f, 0.9f, 1f));
			return kit;
		}

		private static Color VeinColor(string nodeId, out bool glows)
		{
			glows = nodeId == "n_amethyst" || nodeId == "n_gem" || nodeId == "n_quartz" || nodeId == "n_rainbow";
			switch (nodeId)
			{
				case "n_coal":
					return new Color(0.24f, 0.22f, 0.26f);
				case "n_copper":
					return new Color(1.1f, 0.62f, 0.44f);
				case "n_iron":
					return new Color(0.9f, 0.92f, 1.2f);
				case "n_gold":
					return new Color(1.2f, 1.1f, 0.7f);
				case "n_obsidian":
					return new Color(0.34f, 0.26f, 0.5f);
				case "n_quartz":
					return new Color(0.8f, 0.95f, 1f);
				case "n_gem":
					return new Color(0.6f, 0.9f, 1f);
				case "n_rainbow":
					return new Color(1f, 0.8f, 1f);
				case "n_amethyst":
					return new Color(0.9f, 0.7f, 1f);
				default:
					return new Color(0.8f, 0.78f, 0.76f);
			}
		}

		private string NodeRole(int index)
		{
			MineNode node = run.Nodes[index];
			return node.Def.Tier > Sim.ToolTier(ToolKind.Pickaxe) ? "곡괭이가 약함" : $"{node.HitsLeft} / {node.Def.Hits}";
		}

		private void StrikeNode(int index)
		{
			if (run == null)
				return;
			MineStrike strike = run.Strike(index, glintPhase);
			NodeView view = mineNodes[index];
			StartCoroutine(Shake(view.Body.transform));
			if (Report(strike.Result) == false)
				return;
			CombatSfx.Play(strike.WasGlint ? CombatSfxEvent.CriticalHit : CombatSfxEvent.MonsterHit, view.Root.transform.position);
			if (strike.WasGlint)
				uiManager.PopText("정확히", TextType.Critical, view.Root.transform.position + Vector3.up);
			if (strike.Broke)
				UiSfx.Play(UiSfxEvent.Purchase);
			if (strike.FoundPassage)
			{
				UiSfx.Play(UiSfxEvent.Chest);
				Say("내리막을 찾았습니다.");
			}
			RefreshMine();
		}

		private void Descend()
		{
			if (run == null)
				return;
			if (run.Depth >= Sim.MineMaxDepth)
			{
				Say("여기가 가장 깊은 곳입니다.");
				return;
			}
			MovePlayer(mineOffset + new Vector3(0f, 0.1f, -4.6f), () =>
			{
				run.Descend();
				LayFloor();
			});
		}

		private void RefreshMine()
		{
			for (int i = 0; i < mineNodes.Count; i++)
				mineNodes[i].Root.SetActive(run.Nodes[i].IsBroken == false);
			minePassage.SetActive(run.PassageOpen);
			manager.Hud.SetMine(run);
		}

		// 균열 빛이 커졌다 작아짐. 가장 클 때 치면 세게
		private void StepMine()
		{
			if (run == null)
				return;
			if ((playerProvider.CurrentObject.transform.position - mineRoot.position).sqrMagnitude > mineLeaveDistance * mineLeaveDistance && isMoving == false)
			{
				LeaveMine();
				return;
			}
			mineLantern.transform.position = playerProvider.CurrentObject.transform.position + lanternOffset;
			glintPhase = Mathf.Repeat(Time.time / Catalog.Tuning.GlintSeconds, 1f);
			bool sharp = run.IsGlint(glintPhase);
			float scale = sharp ? 1.6f : 0.35f + glintPhase * 0.6f;
			foreach (NodeView view in mineNodes)
			{
				if (view.Root.activeSelf)
					view.Glint.localScale = Vector3.one * scale;
			}
		}
	}
}
