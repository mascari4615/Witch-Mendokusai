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
		private const string WORK = "work";
		private const string WORK_DONE = "done";

		[Header("Unveil")]
		[SerializeField] private float riseSeconds = 0.7f;
		[SerializeField] private float riseOvershoot = 0.12f;

		private readonly Dictionary<string, (string Look, GameObject Body)> siteBodies = new();

		private void BuildSites()
		{
			VillageBodies.Well().Build("Well", outdoor, wellPosition, 0f);
			VillageBodies.Board().Build("Board", outdoor, boardPosition, 0f);
			HomesteadSpot.Create(outdoor, boardPosition + new Vector3(0f, 0f, -0.9f), "BoardSpot", () => "길드 게시판", () => $"의뢰 {Sim.State.Requests.Count}", () => manager.Panels.Show(HomesteadPanelType.Guild));
			HomesteadSpot.Create(outdoor, townHallPosition + townHallDoor, "TownHallSpot", () => Sim.TownHallDef(Sim.State.TownHall).Name, () => "마을 돌보기", () => manager.Panels.Show(HomesteadPanelType.Village));
			HomesteadSpot.Create(outdoor, housePosition + Quaternion.Euler(0f, houseYaw, 0f) * houseDoor, "HouseSpot", () => "집", () => "들어가기", EnterRoom);
			foreach (BuildingSite site in sites)
			{
				BuildingSite captured = site;
				Vector3 door = site.Position + Quaternion.Euler(0f, site.Yaw, 0f) * site.Door;
				HomesteadSpot.Create(outdoor, door, site.Id + "Spot", () => Catalog.Get<BuildingDef>(captured.Id).Name, () => SiteRole(captured.Id), () => UseSite(captured.Id));
			}
			foreach (Vector3 lamp in new[] { new Vector3(-3.4f, 0f, 5f), new Vector3(3.4f, 0f, 5f), new Vector3(-3.6f, 0f, -3f), new Vector3(3.6f, 0f, -3f), new Vector3(-2.2f, 0f, -12f), new Vector3(2.2f, 0f, -17f) })
				VillageBodies.Lamp().Build("Lamp", outdoor, lamp, lamp.x > 0f ? 180f : 0f);
		}

		private string SiteRole(string id)
		{
			WorkState work = Sim.WorkOn(id);
			if (work != null)
				return Sim.IsWorkDone(work) ? "가림막 걷기" : $"공사 중 {HomesteadText.Minutes(work.ReadyAt - Sim.State.Minutes)}";
			if (Sim.IsBuilt(id) == false)
				return "무너진 자리";
			return Sim.IsOpen(id) ? "쓰기" : "맡을 사람을 기다림";
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
					manager.Panels.Show(HomesteadPanelType.Seeds);
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
					manager.Panels.Show(HomesteadPanelType.Village, 1);
					break;
				case "shop":
					manager.Panels.Show(HomesteadPanelType.Store);
					break;
				default:
					Say("아직 준비하는 중입니다.");
					break;
			}
		}

		private void RefreshSites()
		{
			Show("townhall", Sim.State.TownHall.ToString(), townHallPosition, 0f, () => VillageBodies.TownHall(Sim.State.TownHall));
			Show("house", Sim.State.HouseTier.ToString(), housePosition, houseYaw, () => VillageBodies.Home(Sim.State.HouseTier));
			for (int i = 0; i < sites.Length; i++)
			{
				BuildingSite site = sites[i];
				int seed = i * 37 + 5;
				WorkState work = Sim.WorkOn(site.Id);
				int level = Sim.BuildingLevel(site.Id);
				if (work != null && level == 0)
				{
					bool done = Sim.IsWorkDone(work);
					Show(site.Id, done ? WORK_DONE : WORK, site.Position, site.Yaw, () => VillageBodies.Scaffold(site.Size, done));
				}
				else if (level == 0)
				{
					Show(site.Id, RUIN, site.Position, site.Yaw, () => VillageBodies.Ruin(site.Size, seed));
				}
				else
				{
					Show(site.Id, level.ToString(), site.Position, site.Yaw, () => VillageBodies.Built(site.Id, level));
				}
			}
		}

		// 모습이 바뀐 자리만 다시 지음
		private void Show(string id, string look, Vector3 position, float yaw, System.Func<VillageKit> make)
		{
			if (siteBodies.TryGetValue(id, out (string Look, GameObject Body) shown))
			{
				if (shown.Look == look)
					return;
				Destroy(shown.Body);
			}
			GameObject body = make().Build(id, outdoor, position, yaw);
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
