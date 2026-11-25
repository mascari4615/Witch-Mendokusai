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
		private const int CROP_STAGES = 4;

		[Serializable]
		public struct GatherSite
		{
			public string Gather;
			public Vector3 Position;
		}

		[Header("Farm")]
		[SerializeField] private Vector3 farmOrigin = new(-25f, 0f, -9f);
		[SerializeField] private Vector2 plotGap = new(1.9f, 1.9f);
		[SerializeField] private int plotColumns = 6;
		[SerializeField] private float plotWobble = 0.14f;
		[SerializeField] private Vector2 cropHeight = new(0.25f, 0.95f);

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
			new() { Gather = "bush", Position = new Vector3(5f, 0f, 20f) },
			new() { Gather = "herb", Position = new Vector3(-10f, 0f, 17f) },
			new() { Gather = "herb", Position = new Vector3(15f, 0f, -16.5f) },
			new() { Gather = "herb", Position = new Vector3(-18.5f, 0f, 16f) },
			new() { Gather = "mushroompatch", Position = new Vector3(-21f, 0f, 19f) },
			new() { Gather = "mushroompatch", Position = new Vector3(19.5f, 0f, 20f) },
			new() { Gather = "rock", Position = new Vector3(18f, 0f, 12f) },
			new() { Gather = "rock", Position = new Vector3(28f, 0f, 16f) },
			new() { Gather = "rock", Position = new Vector3(20f, 0f, 18f) },
			new() { Gather = "rock", Position = new Vector3(-15f, 0f, -17f) },
		};

		private sealed class PlotView
		{
			public GameObject Spot;
			public GameObject Dry;
			public GameObject Wet;
			public HomesteadSprite Crop;
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
			// 자로 잰 격자로 보이지 않게 조금씩 어긋남
			float offsetX = Mathf.PerlinNoise(index * 1.7f, 0.3f) - 0.5f;
			float offsetZ = Mathf.PerlinNoise(0.7f, index * 1.3f) - 0.5f;
			return farmOrigin + new Vector3(column * plotGap.x + offsetX * plotWobble * 4f, 0f, -row * plotGap.y + offsetZ * plotWobble * 4f);
		}

		private void MarkFarm()
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				Vector3 position = PlotPosition(i);
				ground.AddBlock(new Vector2(position.x, position.z), 0.9f);
			}
		}

		private void BuildFarm()
		{
			for (int i = 0; i < PLOT_MAX; i++)
			{
				int index = i;
				Vector3 position = PlotPosition(i);
				PlotView view = new();
				view.Dry = PlotBody(new Color(1f, 0.92f, 0.82f)).Build("Plot", outdoor, position, index * 47f % 20f - 10f, castShadow: false);
				view.Wet = PlotBody(new Color(0.62f, 0.56f, 0.56f)).Build("PlotWet", outdoor, position, index * 47f % 20f - 10f, castShadow: false);
				view.Crop = HomesteadSprite.Create(outdoor, position + Vector3.up * 0.14f, null, 1f);
				view.Spot = HomesteadSpot.Create(outdoor, position, "PlotSpot", () => PlotName(index), () => PlotRole(index), () => UsePlot(index)).gameObject;
				plots.Add(view);
			}
		}

		// 도톰한 이랑 셋. 네모 칸이 아니라 두둑으로 읽히게
		private static VillageKit PlotBody(Color color)
		{
			VillageKit kit = new();
			kit.Prism(Art.Soil, color * 0.9f, new Vector3(0f, -0.04f, 0f), new Vector3(1.75f, 0.1f, 1.6f), 9, 0.92f);
			for (int i = -1; i <= 1; i++)
				kit.Prism(Art.Soil, color, new Vector3(0f, 0.02f, i * 0.44f), new Vector3(1.4f, 0.13f, 0.36f), 8, 0.7f);
			return kit;
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
					return "심기";
				case PlotPhase.Ripe:
					return "거두기";
				default:
					return Sim.IsWet(plot) ? HomesteadText.Minutes(Sim.MinutesLeft(plot)) : "물 주기";
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
					AskSeed(index);
					break;
				case PlotPhase.Ripe:
					if (Report(Sim.Harvest(index, manager.Season)))
					{
						UiSfx.Play(UiSfxEvent.Purchase);
						StartCoroutine(Pop(plots[index].Crop.transform));
					}
					break;
				default:
					if (Sim.IsWet(plot))
						Say($"{HomesteadText.Minutes(Sim.MinutesLeft(plot))} 뒤에 익습니다.");
					else
						ForArea(index, ToolKind.WateringCan, Sim.Water, HomesteadFailure.AlreadyDone);
					break;
			}
		}

		private void AskSeed(int index)
		{
			manager.Panels.Seeds.Ask(cropId =>
			{
				if (Report(Sim.Plant(index, cropId)))
					manager.Panels.ClosePanel();
			});
			manager.Panels.Show(HomesteadPanelType.Seeds);
		}

		private static IEnumerator Pop(Transform target)
		{
			Vector3 scale = target.localScale;
			const float SECONDS = 0.22f;
			for (float time = 0f; time < SECONDS && target != null; time += Time.deltaTime)
			{
				target.localScale = scale * (1f + Mathf.Sin(time / SECONDS * Mathf.PI) * 0.35f);
				yield return null;
			}
			if (target != null)
				target.localScale = scale;
		}

		private void RefreshFarm()
		{
			for (int i = 0; i < plots.Count; i++)
			{
				PlotView view = plots[i];
				bool open = i < Sim.State.Plots.Count;
				view.Spot.SetActive(open);
				if (open == false)
				{
					view.Dry.SetActive(false);
					view.Wet.SetActive(false);
					view.Crop.gameObject.SetActive(false);
					continue;
				}
				PlotState plot = Sim.State.Plots[i];
				bool wet = plot.Tilled && Sim.IsWet(plot);
				view.Dry.SetActive(plot.Tilled && wet == false);
				view.Wet.SetActive(wet);
				ShowCrop(view, plot);
			}
		}

		private void ShowCrop(PlotView view, PlotState plot)
		{
			bool planted = plot.Crop.Length > 0;
			view.Crop.gameObject.SetActive(planted);
			if (planted == false)
				return;
			float ripeness = Sim.Ripeness(plot);
			bool ripe = Sim.PhaseOf(plot) == PlotPhase.Ripe;
			int stage = Mathf.Min(CROP_STAGES - 1, Mathf.FloorToInt(ripeness * (CROP_STAGES - 1)));
			Sprite sprite = ripe ? Art.IconOf(Catalog.Get<GoodsDef>(Catalog.Get<CropDef>(plot.Crop).Harvest)) : Art.Icon("sprout_" + stage);
			float height = Mathf.Lerp(cropHeight.x, cropHeight.y, ripe ? 0.75f : ripeness * 0.7f);
			if (view.Crop.Renderer.sprite != sprite)
				view.Crop.SetSprite(sprite, height);
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
				Random.State saved = Random.state;
				Random.InitState(i * 91 + 7);
				view.Body = GatherBody(site, i);
				view.Stump = StumpBody(site).Build("Stump", outdoor, site.Position, i * 53f);
				Random.state = saved;
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
					VillageKit rock = new();
					rock.Prism(Art.Stone, Color.white, new Vector3(0f, -0.15f, 0f), new Vector3(1.5f, 1f, 1.3f), 6, 0.6f);
					rock.Prism(Art.Stone, new Color(0.9f, 0.9f, 0.94f), new Vector3(0.6f, -0.1f, -0.3f), new Vector3(0.8f, 0.5f, 0.7f), 5, 0.6f);
					rock.Crystal(Art.Gold, Color.white, new Vector3(-0.2f, 0.5f, -0.4f), new Vector3(0.2f, 0.3f, 0.2f), 5);
					rock.Solid(Vector3.zero, new Vector3(1.2f, 0.8f, 1f));
					return rock.Build("Rock", outdoor, site.Position, index * 33f);
				case "mushroompatch":
					VillageKit caps = new();
					for (int i = 0; i < 4; i++)
					{
						Vector3 foot = new(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
						float size = Random.Range(0.2f, 0.4f);
						caps.Prism(Art.Plaster, Color.white, foot, new Vector3(size * 0.4f, size, size * 0.4f), 7, 0.8f);
						caps.Prism(Art.Cloth, new Color(0.86f, 0.5f, 0.46f), foot + Vector3.up * size * 0.85f, new Vector3(size * 1.3f, size * 0.6f, size * 1.3f), 9, 0.35f);
					}
					return caps.Build("Mushrooms", outdoor, site.Position, 0f);
				default:
					bool isHerb = site.Gather == "herb";
					VillageKit bush = new();
					for (int i = 0; i < 5; i++)
						bush.Prism(Art.Leaf, isHerb ? new Color(0.9f, 1f, 0.95f) : Color.white, new Vector3(Random.Range(-0.4f, 0.4f), -0.05f, Random.Range(-0.4f, 0.4f)), new Vector3(Random.Range(0.5f, 0.8f), Random.Range(0.4f, 0.8f), Random.Range(0.5f, 0.8f)), 7, 0.55f);
					for (int i = 0; i < 6; i++)
						bush.Prism(Art.Cloth, isHerb ? new Color(0.76f, 0.66f, 1f) : new Color(1f, 0.62f, 0.56f), new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.35f, 0.65f), Random.Range(-0.45f, 0.1f)), Vector3.one * 0.1f, 8, 0.6f);
					return bush.Build("Bush", outdoor, site.Position, index * 29f);
			}
		}

		// 거둔 자리에 남는 것. 다시 나는 것을 알 수 있게
		private static VillageKit StumpBody(GatherSite site)
		{
			VillageKit kit = new();
			switch (site.Gather)
			{
				case "tree":
				case "hardtree":
					kit.Prism(Art.Wood, new Color(0.8f, 0.68f, 0.58f), Vector3.zero, new Vector3(0.6f, 0.35f, 0.6f), 8, 0.85f);
					break;
				case "rock":
					for (int i = 0; i < 4; i++)
						kit.Box(Art.Stone, Color.white, new Vector3(Mathf.Cos(i * 1.6f) * 0.4f, -0.05f, Mathf.Sin(i * 1.6f) * 0.4f), Vector3.one * 0.24f, new Vector3(0f, i * 40f, 0f));
					break;
				default:
					for (int i = 0; i < 3; i++)
						kit.Prism(Art.Leaf, new Color(0.9f, 1f, 0.8f), new Vector3(Mathf.Cos(i * 2.1f) * 0.2f, 0f, Mathf.Sin(i * 2.1f) * 0.2f), new Vector3(0.18f, 0.16f, 0.18f), 6, 0.3f);
					break;
			}
			return kit;
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

		private IEnumerator Shake(Transform body)
		{
			Quaternion rest = body.localRotation;
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
