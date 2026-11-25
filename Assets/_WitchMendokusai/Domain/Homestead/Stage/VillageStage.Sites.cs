using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 건물 자리. 폐허, 가림막, 선 건물 셋 중 하나를 규칙 상태대로
	public sealed partial class VillageStage
	{
		private const int GUILD_PEOPLE = 1;
		private const int GUILD_JOBS = 2;
		private const int VILLAGE_TOOLS = 1;

		[Header("Unveil")]
		[SerializeField] private float riseSeconds = 0.7f;
		[SerializeField] private float riseOvershoot = 0.12f;

		[Header("Lamps")]
		[SerializeField] private int lampOnHour = 17;
		[SerializeField] private int lampOffHour = 6;
		[SerializeField] private float lampRange = 7f;
		[SerializeField] private float lampIntensity = 2.4f;
		[SerializeField] private Vector3 lampLightOffset = new(0.4f, 1.6f, 0f);

		private readonly Dictionary<string, (string Look, GameObject Body)> siteBodies = new();

		private static readonly Dictionary<string, string> BUILT_MODELS = new()
		{
			{ "farm", "Shed" },
			{ "workshop", "Workshop" },
			{ "dock", "Dock" },
			{ "quarry", "Quarry" },
			{ "museum", "Museum" },
			{ "ranch", "Barn" },
			{ "tailor", "Tailor" },
			{ "training", "Training" },
			{ "shop", "Shop" },
			{ "smithy", "Smithy" },
			{ "atelier", "HouseBlue" },
			{ "trial", "Trial" },
		};

		// 회관 단. 마지막 모습은 그 위 단도 같이 씀
		private static readonly string[] HALL_MODELS = { "Tent", "Hall2", "Hall3", "Hall4" };

		private static readonly string[] RUIN_MODELS = { "RuinA", "RuinB", "RuinC", "RuinD", "RuinE", "RuinF", "RuinG" };

		// 벽이 없는 자리. 폐허와 공사 모습도 집 모양이 아님
		private static readonly HashSet<string> YARD_SITES = new() { "dock", "quarry", "training", "trial" };

		[Header("Doors")]
		[SerializeField] private float doorAhead = 0.45f;

		private readonly Dictionary<string, GameObject> spotObjects = new();
		private readonly Dictionary<int, (string Look, GameObject Body)> decorBodies = new();

		private VillageModels Models => VillageModels.Instance;

		// ---- 놓인 자리

		private Vector3 CenterOf(PlacedPiece piece) => CenterOf(Sim.PlaceableOf(piece), piece.X, piece.Z, piece.Turn);

		private static Vector3 CenterOf(PlaceableDef placeable, int x, int z, int turn) =>
			new(x + VillageRules.WidthOf(placeable, turn) * 0.5f, 0f, z + VillageRules.DepthOf(placeable, turn) * 0.5f);

		private static Quaternion FacingOf(int turn) => Quaternion.Euler(0f, turn * 90f, 0f);

		// 앞 (문 쪽) 으로 나온 자리
		private Vector3 DoorPoint(string id, float ahead = 0f, float side = 0f)
		{
			PlacedPiece piece = Sim.PieceOf(id);
			return CenterOf(piece) + FacingOf(piece.Turn) * new Vector3(side, 0f, -Sim.PlaceableOf(piece).Depth * 0.5f - doorAhead - ahead);
		}

		private void BuildSites()
		{
			AddSpot("board", "길드 게시판", () => $"의뢰 {Sim.State.Requests.Count}", () => manager.Panels.Show(HomesteadPanelType.Guild));
			AddSpot(HomesteadSim.HALL, null, () => "마을 돌보기", () => manager.Panels.Show(HomesteadPanelType.Village));
			foreach (BuildingDef building in Catalog.Buildings)
			{
				string id = building.Id;
				AddSpot(id, building.Name, () => SiteRole(id), () => UseSite(id));
			}
			RefreshLayout();
		}

		// 다 지은 나루는 끝에 서서 낚음
		private Vector3 SpotPoint(string id) => id == "dock" && Sim.IsBuilt(id) ? DoorPoint(id, dockLength - 2.4f) : DoorPoint(id);

		private void AddSpot(string id, string spotName, System.Func<string> role, System.Action use)
		{
			System.Func<string> nameSource = spotName != null ? () => spotName : () => Sim.TownHallDef(Sim.State.TownHall).Name;
			spotObjects[id] = HomesteadSpot.Create(outdoor, SpotPoint(id), id + "Spot", nameSource, role, use).gameObject;
		}

		private void OnLayoutChanged() => RefreshLayout();

		private void RefreshLayout()
		{
			foreach (KeyValuePair<string, GameObject> pair in spotObjects)
				pair.Value.transform.localPosition = SpotPoint(pair.Key);
			RefreshDecor();
			RefreshGroundCells();
			RefreshSites();
		}

		private void RefreshDecor()
		{
			HashSet<int> seen = new();
			foreach (PlacedPiece piece in Sim.State.Village)
			{
				PlaceableDef placeable = Sim.PlaceableOf(piece);
				if (placeable.Models.Count == 0)
					continue;
				seen.Add(piece.Uid);
				string look = LookOf(piece, piece.Id);
				if (decorBodies.TryGetValue(piece.Uid, out (string Look, GameObject Body) shown) && shown.Look == look)
					continue;
				if (shown.Body != null)
					Destroy(shown.Body);
				GameObject body = Models.Build(placeable.Models[Mathf.Abs(piece.X * 7 + piece.Z * 13) % placeable.Models.Count], outdoor, CenterOf(piece), piece.Turn * 90f);
				if (piece.Id == "lamp")
					AddGlow(body.transform, lampLightOffset, lampRange, lampIntensity);
				decorBodies[piece.Uid] = (look, body);
			}
			foreach (int uid in new List<int>(decorBodies.Keys))
			{
				if (seen.Contains(uid))
					continue;
				Destroy(decorBodies[uid].Body);
				decorBodies.Remove(uid);
			}
			RefreshLamps();
		}

		private static string LookOf(PlacedPiece piece, string look) => $"{look}@{piece.X},{piece.Z},{piece.Turn}";

		private void RefreshGroundCells()
		{
			HashSet<Vector2Int> dirt = new();
			HashSet<Vector2Int> bare = new();
			foreach (PlacedPiece piece in Sim.State.Village)
			{
				PlaceableDef placeable = Sim.PlaceableOf(piece);
				HashSet<Vector2Int> cells = piece.Id == "path_dirt" ? dirt : bare;
				for (int x = 0; x < VillageRules.WidthOf(placeable, piece.Turn); x++)
				{
					for (int z = 0; z < VillageRules.DepthOf(placeable, piece.Turn); z++)
						cells.Add(new Vector2Int(piece.X + x, piece.Z + z));
				}
			}
			ground.SetCells(dirt, bare);
		}

		private bool IsNight => manager.Hour >= lampOnHour || manager.Hour < lampOffHour;

		private void RefreshLamps()
		{
			foreach ((string _, GameObject body) in decorBodies.Values)
			{
				Light lamp = body.GetComponentInChildren<Light>();
				if (lamp != null)
					lamp.enabled = IsNight;
			}
		}

		private string SiteRole(string id)
		{
			WorkState work = Sim.WorkOn(id);
			if (work != null)
				return Sim.IsWorkDone(work) ? "가림막 걷기" : $"공사 중 {HomesteadText.Minutes(work.ReadyAt - Sim.State.Minutes)}";
			if (Sim.IsBuilt(id) == false)
				return "무너진 자리";
			return Sim.IsOpen(id) ? VerbOf(id) : "맡을 사람을 기다림";
		}

		private static string VerbOf(string id)
		{
			switch (id)
			{
				case "farm":
					return "씨앗 고르기";
				case "workshop":
					return "만들기";
				case "dock":
					return "낚시";
				case "quarry":
					return "내려가기";
				case "tailor":
					return "옷 고르기";
				case "ranch":
					return "동물 돌보기";
				case "training":
					return "강화";
				case "smithy":
					return "도구 벼리기";
				case "shop":
					return "사고팔기";
				case "atelier":
					return "인형에게 맡기기";
				case "museum":
					return "들어가기";
				default:
					return "둘러보기";
			}
		}

		private void UseSite(string id)
		{
			WorkState work = Sim.WorkOn(id);
			if (work != null && Sim.IsWorkDone(work))
			{
				Sim.Unveil(id);
				manager.Save();
				return;
			}
			if (Sim.IsBuilt(id) == false)
			{
				manager.Panels.Show(HomesteadPanelType.Village);
				return;
			}
			if (Sim.IsOpen(id) == false)
			{
				Say("맡을 사람이 아직 없습니다.");
				return;
			}
			GreetResident(id);
			Sim.UseFacility(id);
			OpenFacility(id);
		}

		private void GreetResident(string id)
		{
			string resident = Catalog.Get<BuildingDef>(id).Resident;
			if (resident.Length > 0 && Sim.ResidentOf(resident).Greeted == false)
				Sim.Greet(resident);
		}

		// 정한 다섯 건물은 기존 창으로. 나머지는 생활 화면
		private void OpenFacility(string id)
		{
			switch (id)
			{
				case "farm":
					ChooseSeed();
					break;
				case "workshop":
					manager.Panels.Show(HomesteadPanelType.Workshop);
					break;
				case "museum":
					EnterMuseum();
					break;
				case "tailor":
					manager.Panels.Show(HomesteadPanelType.Wardrobe);
					break;
				case "ranch":
					manager.Panels.Show(HomesteadPanelType.Ranch);
					break;
				case "quarry":
					OpenLift();
					break;
				case "dock":
					StartFishing("pond");
					break;
				case "training":
					uiManager.NPC.SetPanel(NPCPanelType.Upgrade);
					break;
				case "smithy":
					manager.Panels.Show(HomesteadPanelType.Village, VILLAGE_TOOLS);
					break;
				case "shop":
					manager.Panels.Show(HomesteadPanelType.Store);
					break;
				case "atelier":
					manager.Panels.Show(HomesteadPanelType.Guild, GUILD_JOBS);
					break;
				default:
					Say("아직 준비하는 중입니다.");
					break;
			}
		}

		private void RefreshSites()
		{
			RefreshLamps();
			spotObjects["dock"].transform.localPosition = SpotPoint("dock");
			Show(HomesteadSim.HALL, HALL_MODELS[Mathf.Min(Sim.State.TownHall, HALL_MODELS.Length) - 1]);
			Show("well", "Well");
			Show("board", "Board");
			for (int i = 0; i < Catalog.Buildings.Count; i++)
			{
				string id = Catalog.Buildings[i].Id;
				WorkState work = Sim.WorkOn(id);
				bool isYard = YARD_SITES.Contains(id);
				if (Sim.IsBuilt(id))
					Show(id, BUILT_MODELS[id]);
				else if (work != null)
					Show(id, (isYard ? "YardWork" : "Work") + (Sim.IsWorkDone(work) ? "Done" : string.Empty));
				else
					Show(id, isYard ? "RuinYard" : RUIN_MODELS[i % RUIN_MODELS.Length]);
			}
		}

		// 모습이 바뀐 자리만 다시 지음
		private void Show(string id, string model)
		{
			PlacedPiece piece = Sim.PieceOf(id);
			string look = LookOf(piece, model);
			if (siteBodies.TryGetValue(id, out (string Look, GameObject Body) shown))
			{
				if (shown.Look == look)
					return;
				Destroy(shown.Body);
			}
			GameObject body = Models.Build(model, outdoor, CenterOf(piece), piece.Turn * 90f);
			// 카메라 가림 해소 (CinemachineComponentDeoccluder) 가 벽으로 보게
			body.AddComponent<GroundSurface>();
			body.SetActive(piece.Uid != heldUid);
			siteBodies[id] = (look, body);
			if (risingId == id)
			{
				risingId = null;
				StartCoroutine(Rise(body.transform));
			}
		}

		// 가림막을 걷은 자리에서 건물이 솟음
		private string risingId;

		private void OnUnveiled(string id)
		{
			risingId = id;
			UiSfx.Play(UiSfxEvent.Evolve);
			manager.Hud.Notice(Catalog.Get<BuildingDef>(id).Name, Catalog.Get<BuildingDef>(id).Levels[Sim.BuildingLevel(id) - 1].Effect, Art.Icon(id));
		}

		private IEnumerator Rise(Transform body)
		{
			for (float time = 0f; time < riseSeconds && body != null; time += Time.deltaTime)
			{
				float ratio = time / riseSeconds;
				float eased = 1f - Mathf.Pow(1f - ratio, 3f);
				float height = eased * (1f + riseOvershoot * Mathf.Sin(ratio * Mathf.PI));
				float width = 1f + riseOvershoot * Mathf.Sin(ratio * Mathf.PI * 2f) * (1f - ratio);
				body.localScale = new Vector3(width, Mathf.Max(0.02f, height), width);
				yield return null;
			}
			if (body != null)
				body.localScale = Vector3.one;
		}
	}
}
