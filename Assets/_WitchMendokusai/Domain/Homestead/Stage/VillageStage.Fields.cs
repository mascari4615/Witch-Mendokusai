using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 밭과 들. 칸마다 다가가 갈기, 심기, 물, 거두기. 들의 자리는 치는 횟수만큼
	public sealed partial class VillageStage
	{
		private const int PLOT_MAX = 18;
		private const string CROP_KEY = "Crop_";
		private const string CROP_SPROUT = "CropSprout";
		private const string FIELD_EDGE = "FieldEdge";

		[Serializable]
		public struct GatherSite
		{
			public string Gather;
			public Vector3 Position;
		}

		[Header("Farm")]
		[SerializeField] private Vector3 farmOrigin = new(-23.5f, 0f, -6.5f);
		[SerializeField] private Vector2 plotGap = new(1f, 1f);
		[SerializeField] private int plotColumns = 6;
		[Tooltip("새싹, 어린 것, 다 큰 것으로 바뀌는 자란 정도")]
		[SerializeField] private Vector2 cropSteps = new(0.25f, 0.62f);
		[SerializeField] private float fieldClear = 1f;
		[SerializeField] private float fieldEdgeOut = 0.62f;
		[SerializeField] private Vector3 scarecrowPosition = new(-24.8f, 0f, -9.8f);
		[SerializeField] private float scarecrowYaw = 30f;

		[Header("Wild")]
		[SerializeField] private float hitShakeSeconds = 0.18f;
		[SerializeField] private float hitShakeAngle = 5f;
		[SerializeField] private GatherSite[] gatherSites =
		{
			new() { Gather = "tree", Position = new Vector3(-6f, 0f, -17f) },
			new() { Gather = "tree", Position = new Vector3(7f, 0f, -18f) },
			new() { Gather = "tree", Position = new Vector3(-12f, 0f, -20f) },
			new() { Gather = "tree", Position = new Vector3(12.5f, 0f, -20f) },
			new() { Gather = "tree", Position = new Vector3(-27f, 0f, 3f) },
			new() { Gather = "tree", Position = new Vector3(27f, 0f, 8.5f) },
			new() { Gather = "tree", Position = new Vector3(4f, 0f, 28f) },
			new() { Gather = "hardtree", Position = new Vector3(-24f, 0f, 24f) },
			new() { Gather = "hardtree", Position = new Vector3(24f, 0f, 24.5f) },
			new() { Gather = "bush", Position = new Vector3(-3.6f, 0f, -14f) },
			new() { Gather = "bush", Position = new Vector3(4.4f, 0f, -13f) },
			new() { Gather = "bush", Position = new Vector3(-17f, 0f, 2f) },
			new() { Gather = "bush", Position = new Vector3(17f, 0f, 8.5f) },
			new() { Gather = "bush", Position = new Vector3(5f, 0f, 21f) },
			new() { Gather = "herb", Position = new Vector3(-11f, 0f, 15f) },
			new() { Gather = "herb", Position = new Vector3(15f, 0f, -16.5f) },
			new() { Gather = "herb", Position = new Vector3(-18.5f, 0f, 16f) },
			new() { Gather = "mushroompatch", Position = new Vector3(-21f, 0f, 19f) },
			new() { Gather = "mushroompatch", Position = new Vector3(19.5f, 0f, 20f) },
			new() { Gather = "rock", Position = new Vector3(18f, 0f, 12f) },
			new() { Gather = "rock", Position = new Vector3(28f, 0f, 16f) },
			new() { Gather = "rock", Position = new Vector3(20f, 0f, 20.6f) },
			new() { Gather = "rock", Position = new Vector3(-15f, 0f, -17f) },
		};

		private sealed class PlotView
		{
			public GameObject Spot;
			public GameObject Overgrown;
			public GameObject Wild;
			public GameObject Dry;
			public GameObject Wet;
			public Transform CropRoot;
			public GameObject Crop;
			public string CropKey = string.Empty;
		}

		private sealed class GatherView
		{
			public GatherDef Def;
			public string SpotId;
			public GameObject Body;
			public GameObject Stump;
			public GameObject Spot;
			public int Hits;
		}

		private readonly List<PlotView> plots = new();
		private readonly List<GatherView> gathers = new();

		// ---- 밭

		private Vector3 PlotPosition(int index)
		{
			int column = index % plotColumns;
			int row = index / plotColumns;
			return farmOrigin + new Vector3(column * plotGap.x, 0f, -row * plotGap.y);
		}

		private void MarkFarm()
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				Vector3 position = PlotPosition(i);
				ground.AddBlock(new Vector2(position.x, position.z), fieldClear);
			}
		}

		private void BuildFarm()
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				int index = i;
				Vector3 position = PlotPosition(i);
				PlotView view = new();
				view.Overgrown = Models.Build("PlotOver", outdoor, position, index % 2 * 180f);
				view.Wild = Models.Build("PlotWild", outdoor, position, index % 2 * 180f);
				view.Dry = Models.Build("PlotDry", outdoor, position, 0f);
				view.Wet = Models.Build("PlotWet", outdoor, position, 0f);
				view.CropRoot = new GameObject("Crop").transform;
				view.CropRoot.SetParent(outdoor, false);
				view.CropRoot.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, index % 2 * 180f, 0f));
				view.Spot = HomesteadSpot.Create(outdoor, position, "PlotSpot", () => PlotName(index), () => PlotRole(index), () => UsePlot(index)).gameObject;
				plots.Add(view);
			}
			BuildFieldEdge();
		}

		// 밭 둘레의 낮은 통나무. 길이 오는 북쪽은 비움
		private void BuildFieldEdge()
		{
			int rows = Mathf.CeilToInt(PLOT_MAX / (float)plotColumns);
			for (int column = 0; column < plotColumns; column++)
				Models.Build(FIELD_EDGE, outdoor, PlotPosition((rows - 1) * plotColumns + column) + Vector3.back * fieldEdgeOut, 0f);
			for (int row = 0; row < rows; row++)
			{
				Models.Build(FIELD_EDGE, outdoor, PlotPosition(row * plotColumns) + Vector3.left * fieldEdgeOut, 90f);
				Models.Build(FIELD_EDGE, outdoor, PlotPosition(row * plotColumns + plotColumns - 1) + Vector3.right * fieldEdgeOut, -90f);
			}
			Models.Build("Scarecrow", outdoor, scarecrowPosition, scarecrowYaw);
		}

		private string PlotName(int index)
		{
			PlotState plot = Sim.State.Plots[index];
			return plot.Crop.Length > 0 ? Catalog.Get<CropDef>(plot.Crop).Name : plot.Tilled ? "갈아 둔 밭" : "묵은 밭";
		}

		private string PlotRole(int index)
		{
			PlotState plot = Sim.State.Plots[index];
			if (plot.Tilled == false)
				return "갈기";
			switch (Sim.PhaseOf(plot))
			{
				case PlotPhase.Empty:
					return CanSowHeld ? $"{Catalog.Get<CropDef>(heldCrop).Name} 심기" : "씨앗 고르기";
				case PlotPhase.Ripe:
					return "거두기";
				default:
					if (Sim.CanWater(plot))
						return Sim.IsWet(plot) ? $"{HomesteadText.Minutes(Sim.MinutesLeft(plot))}, 물 더 주기" : "물 주기";
					return HomesteadText.Minutes(Sim.MinutesLeft(plot));
			}
		}

		// 도구가 좋으면 이어진 칸까지 한 번에. 처음 칸이 되면 나머지 거절은 조용히
		private void ForArea(int index, ToolKind tool, Func<int, HomesteadResult> run, HomesteadFailure quiet)
		{
			HomesteadResult first = run(index);
			if (Report(first) == false)
				return;
			int left = Sim.ToolPower(tool) - 1;
			for (int step = 1; left > 0 && index + step < Sim.State.Plots.Count; step++)
			{
				HomesteadResult result = run(index + step);
				if (result.Ok)
					left--;
				else if (result.Failure != quiet)
					break;
			}
			CombatSfx.Play(CombatSfxEvent.WeaponHitLight, plots[index].Spot.transform.position);
		}

		private void UsePlot(int index)
		{
			PlotState plot = Sim.State.Plots[index];
			if (plot.Tilled == false)
			{
				ForArea(index, ToolKind.Hoe, Sim.Till, HomesteadFailure.AlreadyDone);
				return;
			}
			switch (Sim.PhaseOf(plot))
			{
				case PlotPhase.Empty:
					if (CanSowHeld)
						Sow(index, heldCrop);
					else
						AskSeed(index);
					break;
				case PlotPhase.Ripe:
					if (Report(Sim.Harvest(index, manager.Season)))
					{
						UiSfx.Play(UiSfxEvent.Purchase);
						StartCoroutine(Pop(plots[index].Wet.activeSelf ? plots[index].Wet.transform : plots[index].Dry.transform));
					}
					break;
				default:
					if (Sim.CanWater(plot) == false)
						Say($"{HomesteadText.Minutes(Sim.MinutesLeft(plot))} 뒤에 익습니다.");
					else
						ForArea(index, ToolKind.WateringCan, Sim.Water, HomesteadFailure.AlreadyDone);
					break;
			}
		}

		// 한 번 고른 씨앗은 손에 남아 다음 칸에 바로 심음. 바꾸려면 텃밭 창고에서
		private string heldCrop = string.Empty;

		private bool CanSowHeld => heldCrop.Length > 0 && Sim.Count(Catalog.Get<CropDef>(heldCrop).Seed) > 0;

		private void Sow(int index, string cropId)
		{
			if (Report(Sim.Plant(index, cropId)) == false)
				return;
			UiSfx.Play(UiSfxEvent.Confirm);
			StartCoroutine(Pop(plots[index].Dry.transform));
		}

		private void AskSeed(int index)
		{
			manager.Panels.Seeds.Ask(cropId =>
			{
				heldCrop = cropId;
				manager.Panels.ClosePanel();
				Sow(index, cropId);
			});
			manager.Panels.Show(HomesteadPanelType.Seeds);
		}

		private void ChooseSeed()
		{
			manager.Panels.Seeds.Ask(cropId =>
			{
				heldCrop = cropId;
				manager.Panels.ClosePanel();
				Say($"{Catalog.Get<CropDef>(cropId).Name} 씨앗을 듭니다.");
			});
			manager.Panels.Show(HomesteadPanelType.Seeds);
		}

		private readonly Dictionary<Transform, Vector3> popBases = new();

		private IEnumerator Pop(Transform target)
		{
			if (popBases.TryGetValue(target, out Vector3 scale) == false)
			{
				scale = target.localScale;
				popBases[target] = scale;
			}
			const float SECONDS = 0.22f;
			for (float time = 0f; time < SECONDS && target != null; time += Time.deltaTime)
			{
				target.localScale = scale * (1f + Mathf.Sin(time / SECONDS * Mathf.PI) * 0.35f);
				yield return null;
			}
			if (target != null)
				target.localScale = scale;
			popBases.Remove(target);
		}

		private void RefreshFarm()
		{
			for (int i = 0; i < plots.Count; i++)
			{
				PlotView view = plots[i];
				bool open = i < Sim.State.Plots.Count;
				view.Spot.SetActive(open);
				view.Overgrown.SetActive(open == false);
				view.Wild.SetActive(open && Sim.State.Plots[i].Tilled == false);
				if (open == false)
				{
					view.Dry.SetActive(false);
					view.Wet.SetActive(false);
					ShowCropModel(view, string.Empty);
					continue;
				}
				PlotState plot = Sim.State.Plots[i];
				bool wet = plot.Tilled && Sim.IsWet(plot);
				view.Dry.SetActive(plot.Tilled && wet == false);
				view.Wet.SetActive(wet);
				ShowCrop(view, plot);
			}
		}

		private void ShowCrop(PlotView view, PlotState plot) =>
			ShowCropModel(view, plot.Crop.Length == 0 ? string.Empty : CropModel(plot));

		// 다시 열리는 작물은 거둔 뒤에도 다 큰 모습
		private string CropModel(PlotState plot)
		{
			if (Sim.PhaseOf(plot) == PlotPhase.Ripe)
				return CROP_KEY + plot.Crop + "_c";
			float ripeness = Sim.Ripeness(plot);
			if (plot.Harvests > 0 || ripeness >= cropSteps.y)
				return CROP_KEY + plot.Crop + "_b";
			return ripeness >= cropSteps.x ? CROP_KEY + plot.Crop + "_a" : CROP_SPROUT;
		}

		private void ShowCropModel(PlotView view, string key)
		{
			if (view.CropKey == key)
				return;
			view.CropKey = key;
			if (view.Crop != null)
				Destroy(view.Crop);
			view.Crop = null;
			if (key.Length == 0)
				return;
			view.Crop = Models.Build(key, view.CropRoot, Vector3.zero, 0f);
			if (view.CropRoot.gameObject.activeInHierarchy)
				StartCoroutine(Pop(view.Crop.transform));
		}

		// ---- 들

		private void MarkWild()
		{
			foreach (GatherSite site in gatherSites)
				ground.AddBlock(new Vector2(site.Position.x, site.Position.z), 0.5f);
		}

		private void BuildWild()
		{
			for (int i = 0; i < gatherSites.Length; i++)
			{
				GatherSite site = gatherSites[i];
				GatherView view = new() { Def = Catalog.Get<GatherDef>(site.Gather), SpotId = $"{site.Gather}:{i}" };
				view.Body = GatherBody(site, i);
				view.Stump = Models.Build(StumpModel(site), outdoor, site.Position, i * 53f);
				GatherView captured = view;
				view.Spot = HomesteadSpot.Create(outdoor, site.Position + new Vector3(0f, 0f, -0.5f), "GatherSpot", () => captured.Def.Name, () => GatherRole(captured), () => UseGather(captured)).gameObject;
				gathers.Add(view);
			}
		}

		private GameObject GatherBody(GatherSite site, int index)
		{
			switch (site.Gather)
			{
				case "tree":
					return ground.SpawnTree(index, outdoor, site.Position, index * 67f, 0.62f);
				case "hardtree":
					return ground.SpawnTree(index + 3, outdoor, site.Position, index * 41f, 1f);
				case "rock":
					return Models.Build("Rock", outdoor, site.Position, index * 33f);
				case "mushroompatch":
					return Models.Build("Mushrooms", outdoor, site.Position, index * 71f);
				case "herb":
					return Models.Build("Herb", outdoor, site.Position, index * 29f);
				default:
					return Models.Build("Bush", outdoor, site.Position, index * 29f);
			}
		}

		// 수확한 자리에 남는 것. 다시 나는 것을 알 수 있게
		private static string StumpModel(GatherSite site)
		{
			switch (site.Gather)
			{
				case "tree":
				case "hardtree":
					return "Stump";
				case "rock":
					return "Rubble";
				default:
					return "Cut";
			}
		}

		private string GatherRole(GatherView view)
		{
			if (Sim.CanGather(view.Def) == false)
				return $"{HomesteadText.ToolName(view.Def.Tool)}가 약함";
			int hits = Sim.GatherHits(view.Def);
			return hits > 1 ? $"{view.Hits} / {hits}" : HomesteadText.ToolName(view.Def.Tool);
		}

		private void UseGather(GatherView view)
		{
			StartCoroutine(Shake(view.Body.transform));
			CombatSfx.Play(CombatSfxEvent.WeaponHitLight, view.Body.transform.position);
			if (Sim.CanGather(view.Def) == false)
			{
				Say($"{HomesteadText.ToolName(view.Def.Tool)}가 약해 흠집만 났습니다.");
				return;
			}
			view.Hits++;
			if (view.Hits < Sim.GatherHits(view.Def))
				return;
			view.Hits = 0;
			if (Report(Sim.Gather(view.SpotId, view.Def.Id)))
				UiSfx.Play(UiSfxEvent.Purchase);
		}

		// 흔들리는 중에 또 치면 처음 자세가 기준
		private readonly Dictionary<Transform, Quaternion> restPoses = new();

		private IEnumerator Shake(Transform body)
		{
			if (restPoses.TryGetValue(body, out Quaternion rest) == false)
			{
				rest = body.localRotation;
				restPoses[body] = rest;
			}
			for (float time = 0f; time < hitShakeSeconds && body != null; time += Time.deltaTime)
			{
				float ratio = 1f - time / hitShakeSeconds;
				body.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 70f) * hitShakeAngle * ratio);
				yield return null;
			}
			if (body != null)
				body.localRotation = rest;
		}

		private void RefreshWild()
		{
			foreach (GatherView view in gathers)
			{
				bool ready = Sim.IsGatherReady(view.SpotId);
				view.Body.SetActive(ready);
				view.Spot.SetActive(ready);
				view.Stump.SetActive(ready == false);
			}
		}
	}
}
