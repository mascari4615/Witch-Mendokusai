using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 건물 자리. 폐허, 가림막, 선 건물 셋 중 하나를 규칙 상태대로
	public sealed partial class VillageStage
	{
		private const string RUIN = "ruin";
		private const int GUILD_PEOPLE = 1;
		private const int GUILD_JOBS = 2;
		private const int VILLAGE_TOOLS = 1;
		private const string WORK = "work";
		private const string WORK_DONE = "done";

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
		private readonly List<Light> lampLights = new();

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

		private static readonly string[] RUIN_MODELS = { "RuinA", "RuinB" };

		// 벽이 없는 자리. 폐허와 공사 모습도 집 모양이 아님
		private static readonly HashSet<string> YARD_SITES = new() { "dock", "quarry", "training", "trial" };

		private VillageModels Models => VillageModels.Instance;

		private void BuildSites()
		{
			Models.BuildOnGrid("Well", outdoor, wellPosition, 0f);
			Models.BuildOnGrid("Board", outdoor, boardPosition, 0f);
			HomesteadSpot.Create(outdoor, boardPosition + new Vector3(0f, 0f, -0.9f), "BoardSpot", () => "길드 게시판", () => $"의뢰 {Sim.State.Requests.Count}", () => manager.Panels.Show(HomesteadPanelType.Guild));
			HomesteadSpot.Create(outdoor, townHallPosition + townHallDoor, "TownHallSpot", () => Sim.TownHallDef(Sim.State.TownHall).Name, () => "마을 돌보기", () => manager.Panels.Show(HomesteadPanelType.Village));
			HomesteadSpot.Create(outdoor, housePosition + Quaternion.Euler(0f, houseYaw, 0f) * houseDoor, "HouseSpot", () => "집", () => "들어가기", EnterRoom);
			foreach (BuildingSite site in sites)
			{
				BuildingSite captured = site;
				Vector3 door = site.Position + Quaternion.Euler(0f, site.Yaw, 0f) * site.Door;
				HomesteadSpot.Create(outdoor, door, site.Id + "Spot", () => Catalog.Get<BuildingDef>(captured.Id).Name, () => SiteRole(captured.Id), () => UseSite(captured.Id));
			}
			BuildDressing();
			foreach (Vector3 lamp in new[] { new Vector3(-2f, 0f, 8f), new Vector3(2f, 0f, 8f), new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(-2f, 0f, -10f), new Vector3(2f, 0f, -10f) })
			{
				GameObject body = Models.BuildOnGrid("Lamp", outdoor, lamp, lamp.x > 0f ? 180f : 0f);
				lampLights.Add(AddGlow(body.transform, lampLightOffset, lampRange, lampIntensity));
			}
		}

		// 돌담, 돌길, 울타리, 통. 한 칸짜리를 칸 수만큼 이어 놓음
		private void BuildDressing()
		{
			foreach ((string key, int x, int z, int count, bool isAlongZ) in dressing)
			{
				for (int i = 0; i < count; i++)
				{
					Vector3 cell = new(x + 0.5f + (isAlongZ ? 0 : i), 0f, z + 0.5f + (isAlongZ ? i : 0));
					bool hasTwin = key == "StoneWall" || key == "StonePath";
					Models.BuildOnGrid(hasTwin && i % 2 == 1 ? key + "B" : key, outdoor, cell, isAlongZ && key != "StonePath" ? 90f : 0f);
				}
			}
		}

		private static readonly (string Key, int X, int Z, int Count, bool IsAlongZ)[] dressing =
		{
			("StoneWall", -23, 7, 5, false), ("StoneWall", -23, 8, 8, true), ("Fence", -11, 8, 2, false), ("Fence", -11, 14, 2, false),
			("StonePath", -1, -5, 16, true), ("StonePath", 0, -5, 16, true), ("StonePath", -15, 6, 2, true), ("StonePath", -8, 6, 2, true), ("StonePath", 8, -6, 2, true),
			("Props", -11, 12, 1, false), ("Props", 12, 12, 1, false), ("Fence", -5, -12, 4, false), ("Fence", 1, -12, 4, false),
			("StoneWall", 2, 12, 2, false), ("StoneWall", -5, 12, 2, false),
		};

		private bool IsNight => manager.Hour >= lampOnHour || manager.Hour < lampOffHour;

		private void RefreshLamps()
		{
			foreach (Light lamp in lampLights)
				lamp.enabled = IsNight;
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
				case "museum":
					return "기증과 손질";
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
					manager.Panels.Show(HomesteadPanelType.Store, StorePage.TAB_CRAFT);
					break;
				case "museum":
					manager.Panels.Show(HomesteadPanelType.Museum);
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
			Show("townhall", Sim.State.TownHall.ToString(), () => TownHallBody(Sim.State.TownHall));
			Show("house", Sim.State.HouseTier.ToString(), () => Models.BuildOnGrid("HouseMended", outdoor, housePosition, houseYaw));
			for (int i = 0; i < sites.Length; i++)
			{
				BuildingSite site = sites[i];
				bool isYard = YARD_SITES.Contains(site.Id);
				string ruin = isYard ? "RuinYard" : RUIN_MODELS[i % RUIN_MODELS.Length];
				WorkState work = Sim.WorkOn(site.Id);
				int level = Sim.BuildingLevel(site.Id);
				if (work != null && level == 0)
				{
					bool done = Sim.IsWorkDone(work);
					string body = (isYard ? "YardWork" : "Work") + (done ? "Done" : string.Empty);
					Show(site.Id, done ? WORK_DONE : WORK, () => Models.BuildOnGrid(body, outdoor, site.Position, site.Yaw));
				}
				else if (level == 0)
				{
					Show(site.Id, RUIN, () => Models.BuildOnGrid(ruin, outdoor, site.Position, site.Yaw));
				}
				else
				{
					Show(site.Id, level.ToString(), () => Models.BuildOnGrid(BUILT_MODELS[site.Id], outdoor, site.Position, site.Yaw));
				}
			}
		}

		private GameObject TownHallBody(int tier) =>
			Models.BuildOnGrid(HALL_MODELS[Mathf.Min(tier, HALL_MODELS.Length) - 1], outdoor, townHallPosition, 0f);

		// 모습이 바뀐 자리만 다시 지음
		private void Show(string id, string look, System.Func<GameObject> make)
		{
			if (siteBodies.TryGetValue(id, out (string Look, GameObject Body) shown))
			{
				if (shown.Look == look)
					return;
				Destroy(shown.Body);
			}
			GameObject body = make();
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
