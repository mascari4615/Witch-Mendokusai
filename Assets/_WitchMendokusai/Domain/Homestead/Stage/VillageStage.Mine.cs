using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;
using Random = System.Random;

namespace WitchMendokusai
{
	// 갱도. 층마다 바위를 다시 놓고, 바위에 도는 빛에 맞춰 침. 벽과 둥근 천장을 닫은 동굴
	public sealed partial class VillageStage
	{
		private const string MINE_KEY = "Mine_";
		private const int MINE_LAMPS = 5;
		private const int MINE_WALLS = 26;
		private const int SHELL_TINT_PARTS = 2;
		private const float MINE_ROOF = 9f;
		private static readonly int BASE_COLOR = Shader.PropertyToID("_BaseColor");

		[Header("Mine")]
		[SerializeField] private float mineDomeRadius = 30f;
		[SerializeField, Range(0f, 1f)] private float mineShade = 0.35f;
		[SerializeField] private float mineLampAngle = 0.52f;
		[SerializeField] private float mineLampRadius = 9.2f;
		[SerializeField] private float mineLampHeight = 2.4f;
		[SerializeField] private Vector3 liftPosition = new(0f, 0f, -6.8f);
		[SerializeField] private Vector3 holePosition = new(0f, 0f, 5.2f);
		[SerializeField] private float holeGlowRange = 4f;
		[SerializeField] private float holeGlowIntensity = 2.4f;
		[SerializeField] private float glintHeight = 1.05f;
		[SerializeField] private float glintTurn = 120f;
		[Tooltip("빛이 가장 밝을 때, 그 밖의 가장 작을 때와 커지는 폭")]
		[SerializeField] private Vector3 glintScale = new(1.6f, 0.35f, 0.6f);
		[SerializeField] private Vector3 mineWallSize = new(3f, 4f, 1f);
		[Tooltip("천장 콜라이더 높이. 모델의 둥근 지붕 아래")]
		[SerializeField] private float mineRoofHeight = 6.6f;
		[Tooltip("내려와 서는 자리. 승강기 앞, 동굴 가운데 쪽")]
		[SerializeField] private Vector3 mineArrival = new(0f, 0.1f, -4f);
		[SerializeField] private Vector3 liftGlow = new(1f, 2f, 0f);
		[SerializeField] private Vector3 liftSpot = new(0f, 0f, 0.4f);
		[SerializeField] private Vector3 holeGlow = new(0.7f, 1.2f, 0.4f);
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
		private MeshRenderer mineShell;
		private MaterialPropertyBlock shellBlock;
		private bool isInsideMine;
		private MineRun run;
		private float glintPhase;

		private void BuildMine()
		{
			mineRoot = new GameObject("Mine").transform;
			mineRoot.SetParent(transform, false);
			mineRoot.localPosition = mineOffset;
			VillageLights.BuildDome(mineRoot, mineDomeRadius, MINE_ROOF, shadeColor * mineShade);
			GameObject shell = Models.Build("MineShell", mineRoot, Vector3.zero, 0f);
			VillageInterior.Solidify(shell);
			mineShell = shell.GetComponent<MeshRenderer>();
			for (int i = 0; i < MINE_LAMPS; i++)
			{
				float angle = mineLampAngle + i / (float)MINE_LAMPS * Mathf.PI * 2f;
				AddGlow(mineRoot, new Vector3(Mathf.Cos(angle) * mineLampRadius, mineLampHeight, Mathf.Sin(angle) * mineLampRadius), mineLampRange, mineLampIntensity);
			}
			for (int i = 0; i < MINE_WALLS; i++)
			{
				float angle = i / (float)MINE_WALLS * Mathf.PI * 2f;
				GameObject wall = new("Wall");
				wall.transform.SetParent(mineRoot, false);
				wall.transform.SetLocalPositionAndRotation(new Vector3(Mathf.Cos(angle) * (mineRadius + mineWallSize.z), mineWallSize.y * 0.5f, Mathf.Sin(angle) * (mineRadius + mineWallSize.z)), Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f));
				wall.AddComponent<BoxCollider>().size = mineWallSize;
			}
			Models.Build("MineLift", mineRoot, liftPosition, 0f);
			AddGlow(mineRoot, liftPosition + liftGlow, mineLampRange, mineLampIntensity);
			mineLantern = AddGlow(mineRoot, Vector3.zero, mineLampRange, mineLampIntensity);
			GameObject roof = new("MineRoof");
			roof.transform.SetParent(mineRoot, false);
			roof.AddComponent<BoxCollider>().size = new Vector3(mineRadius * 3f, mineWallSize.z, mineRadius * 3f);
			roof.transform.localPosition = Vector3.up * mineRoofHeight;
			// 동굴 벽과 천장이 시야를 막으면 카메라를 안으로 당김
			mineRoot.gameObject.AddComponent<GroundSurface>();
			GameObject floor = new("MineFloor");
			floor.transform.SetParent(mineRoot, false);
			BoxCollider box = floor.AddComponent<BoxCollider>();
			box.center = new Vector3(0f, -0.5f, 0f);
			box.size = new Vector3(mineRadius * 3f, 1f, mineRadius * 3f);
			mineFloor = new GameObject("Nodes").transform;
			mineFloor.SetParent(mineRoot, false);
			HomesteadSpot.Create(mineRoot, liftPosition + liftSpot, "LiftSpot", () => "승강기", () => "올라가기", LeaveMineByLift, () => "올라가기");
			minePassage = Models.Build("MineHole", mineRoot, holePosition, 0f);
			AddGlow(minePassage.transform, holeGlow, holeGlowRange, holeGlowIntensity);
			HomesteadSpot.Attach(minePassage, () => "내리막", () => run != null && run.Depth >= Sim.MineMaxDepth ? "더는 없음" : run != null && run.Lantern <= 0 ? "등불이 다함" : "내려가기", Descend, () => "내려가기");
			minePassage.SetActive(false);
		}

		// 깊이 구간마다 바위 빛깔이 다름. 돌과 흙 조각만
		private void TintShell()
		{
			shellBlock ??= new MaterialPropertyBlock();
			shellBlock.SetColor(BASE_COLOR, mineColors[Mathf.Clamp(Catalog.MineBands.IndexOf(run.Band), 0, mineColors.Length - 1)]);
			for (int i = 0; i < SHELL_TINT_PARTS; i++)
				mineShell.SetPropertyBlock(shellBlock, i);
		}

		private void OpenLift()
		{
			manager.Panels.Mine.Ask(EnterMine);
			manager.Panels.Show(HomesteadPanelType.Mine);
		}

		private void EnterMine(int depth)
		{
			manager.Panels.ClosePanel();
			MovePlayer(mineOffset + mineArrival, () =>
			{
				// 옮기는 사이 마을이 꺼졌으면 (LeaveMine 이 이미 지나감) 들어가지 않음. 실내 신호가 남지 않게
				if (isActiveAndEnabled == false)
					return;
				run = Sim.EnterMine(depth);
				IndoorScope.Enter(this);
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
			isInsideMine = false;
			IndoorScope.Leave(this);
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
					bool clear = point.y > -3.4f && Vector2.Distance(point, new Vector2(holePosition.x, holePosition.z)) > 2f && placed.Exists(other => Vector2.Distance(other, point) < mineNodeGap) == false;
					if (clear)
						break;
				}
				placed.Add(point);
				mineNodes.Add(MakeNode(i, new Vector3(point.x, 0f, point.y), (float)random.NextDouble() * 360f));
			}
			TintShell();
			RefreshMine();
		}

		private NodeView MakeNode(int index, Vector3 position, float yaw)
		{
			MineNode node = run.Nodes[index];
			NodeView view = new() { Root = new GameObject("Node") };
			view.Root.transform.SetParent(mineFloor, false);
			view.Root.transform.localPosition = position;
			view.Body = Models.Build(MINE_KEY + node.Def.Id, view.Root.transform, Vector3.zero, yaw);
			view.Glint = Models.Build("MineGlint", view.Root.transform, Vector3.up * glintHeight, 0f).transform;
			int captured = index;
			HomesteadSpot.Attach(view.Root, () => run.Nodes[captured].Def.Name, () => NodeRole(captured), () => StrikeNode(captured), () => "깨기");
			return view;
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
			if (Report(run.CanDescend()) == false)
				return;
			MovePlayer(mineOffset + mineArrival, () =>
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
			// 옮긴 첫 프레임은 보간 때문에 아직 옛 자리. 안에 있는 것을 한 번 본 뒤에만 나간 것으로 침
			bool isNear = (playerProvider.CurrentObject.transform.position - mineRoot.position).sqrMagnitude < mineLeaveDistance * mineLeaveDistance;
			isInsideMine |= isNear;
			if (isInsideMine && isNear == false && isMoving == false)
			{
				LeaveMine();
				return;
			}
			mineLantern.transform.position = playerProvider.CurrentObject.transform.position + lanternOffset;
			glintPhase = Mathf.Repeat(Time.time / Catalog.Tuning.GlintSeconds, 1f);
			bool sharp = run.IsGlint(glintPhase);
			float scale = sharp ? glintScale.x : glintScale.y + glintPhase * glintScale.z;
			foreach (NodeView view in mineNodes)
			{
				if (view.Root.activeSelf == false)
					continue;
				view.Glint.localScale = Vector3.one * scale;
				view.Glint.localRotation = Quaternion.Euler(0f, Time.time * glintTurn, 0f);
			}
		}
	}
}
