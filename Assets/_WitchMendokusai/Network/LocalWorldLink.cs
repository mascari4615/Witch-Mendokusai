using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 내 안의 세계로 이어진 줄 (TASK-WM-217).
	///
	/// 「혼자 하기」가 이것이다 — 하지만 <b>별도 모드가 아니다.</b> 멀리 있는 세계에 못 붙었을 때
	/// 게임이 자기 안에서 같은 세계(<see cref="WorldSim"/>)를 돌리고 거기 붙는다.
	/// 게임 쪽 코드는 어느 쪽에 붙었는지 묻지 않는다(<see cref="IWorldLink"/>).
	///
	/// 서버 프로세스를 따로 띄우지 않으므로 배포에 실행파일이 늘지 않는다.
	/// </summary>
	public sealed class LocalWorldLink : IWorldLink
	{
		private readonly WorldSim world;
		private readonly WorldDoll me;

		private WorldBrewView completed;

		public LocalWorldLink(WorldSim world)
		{
			this.world = world;
			me = world.Join();
		}

		public LocalWorldLink() : this(new WorldSim())
		{
		}

		/// <summary>내 안의 세계 본체 — 저장·불러오기 같은 일이 여기로 온다.</summary>
		public WorldSim World => world;

		public int MyDollId => me.Id;

		public bool IsLinked => true;

		public WorldDollView[] Dolls
		{
			get
			{
				WorldDoll[] snapshot = world.Snapshot();
				WorldDollView[] views = new WorldDollView[snapshot.Length];
				for (int i = 0; i < snapshot.Length; i++)
				{
					views[i] = new WorldDollView
					{
						id = snapshot[i].Id,
						x = snapshot[i].Position.x,
						z = snapshot[i].Position.z,
					};
				}

				return views;
			}
		}

		public BuildingView[] Buildings
		{
			get
			{
				PlacedBuilding[] placed = world.Buildings();
				BuildingView[] views = new BuildingView[placed.Length];
				for (int i = 0; i < placed.Length; i++)
				{
					views[i] = new BuildingView
					{
						x = placed[i].Pivot.x,
						y = placed[i].Pivot.y,
						z = placed[i].Pivot.z,
						w = placed[i].Size.x,
						l = placed[i].Size.y,
						buildingId = placed[i].BuildingId,
					};
				}

				return views;
			}
		}

		/// <summary>내 안의 세계도 시각을 준다 — 같은 규약이라 화면은 어느 쪽인지 몰라도 된다.</summary>
		public WorldTimeView Time => new WorldTimeView
		{
			year = world.Calendar.Year,
			season = world.Calendar.Season,
			day = world.Calendar.Day,
			hour = world.Calendar.Hour,
			minute = world.Calendar.Minute,
		};

		public void RequestMove(float x, float z)
		{
			world.TryMove(me.Id, new Numerics.Vector3(x, 0f, z));
		}

		public void RequestPlace(int cellX, int cellY, int cellZ, int width, int length, int buildingId)
		{
			world.TryPlaceBuilding(
				new Numerics.Vector3Int(cellX, cellY, cellZ),
				new Numerics.Vector2Int(width, length),
				buildingId);
		}

		public void RequestRemove(int cellX, int cellY, int cellZ)
		{
			world.TryRemoveBuilding(new Numerics.Vector3Int(cellX, cellY, cellZ));
		}

		/// <summary>내 안의 세계에서도 솥은 세계의 것이다 — 같은 규약으로 내어 준다.</summary>
		public WorldBrewView Brew
		{
			get
			{
				List<DomainSDK.Alchemy.BrewStep> steps = new List<DomainSDK.Alchemy.BrewStep>();
				world.Cauldron.ReadSteps(steps);

				BrewStepView[] path = new BrewStepView[steps.Count];
				for (int i = 0; i < steps.Count; i++)
				{
					path[i] = new BrewStepView
					{
						dx = steps[i].Direction.X,
						dy = steps[i].Direction.Y,
						grind = steps[i].Grind,
					};
				}

				DomainSDK.Alchemy.BrewState state = world.Cauldron.State;
				return new WorldBrewView
				{
					x = state.Position.X,
					y = state.Position.Y,
					steps = state.StepCount,
					side = state.AccruedSideEffect,
					path = path,
				};
			}
		}

		public void RequestBrewStep(float dx, float dy, float grind)
		{
			world.Cauldron.AddStep(new DomainSDK.Alchemy.BrewStep
			{
				Direction = new DomainSDK.Alchemy.BrewVector(dx, dy),
				Grind = grind,
			});
		}

		public void RequestBrewReset() => world.Cauldron.ResetBrew();

		public void RequestBrewComplete()
		{
			// 혼자여도 규칙은 같다 — 세계가 내주고, 빈 솥이면 아무 일도 없다.
			if (world.Cauldron.TryComplete(out DomainSDK.Alchemy.BrewState taken) == false)
				return;

			completed = new WorldBrewView
			{
				x = taken.Position.X,
				y = taken.Position.Y,
				steps = taken.StepCount,
				side = taken.AccruedSideEffect,
			};
		}

		public WorldBrewView TakeCompletedBrew()
		{
			WorldBrewView taken = completed;
			completed = null;
			return taken;
		}

		public void RequestGather(int itemId, int amount)
		{
			world.TryGather(me.Id, ItemCatalog.Find(itemId), amount);
		}

		public void RequestConsume(int itemId, int amount)
		{
			world.TryConsume(me.Id, itemId, amount);
		}

		/// <summary>내 가방 — 화면이 읽어 간다.</summary>
		public int BagCount(int itemId) => world.BagCount(me.Id, itemId);
	}

	/// <summary>
	/// 내 안의 세계가 아는 아이템 목록 (TASK-WM-217).
	///
	/// <b>정본은 게임 자산</b>이고, 에디터에서 뽑아 둔 목록(<c>Resources/items.json</c>)을 읽는다 —
	/// 서버가 읽는 것과 <b>같은 파일 모양</b>이라 혼자 놀 때와 같이 놀 때가 갈라지지 않는다.
	/// 목록이 아직 없으면(뽑기 전) 아무 것도 모르는 세계가 된다 — 조용히 씨앗으로 때우면
	/// 「왜 이 아이템만 안 들어가지」로 나중에 나타난다.
	/// </summary>
	public static class ItemCatalog
	{
		private const string RESOURCE_NAME = "items";

		private static WorldItemCatalog catalog;

		public static IItemData Find(int itemId) => Ensure().Find(itemId);

		private static WorldItemCatalog Ensure()
		{
			if (catalog != null)
				return catalog;

			TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
			if (asset == null)
			{
				Debug.LogWarning("[items] Resources/items.json 이 없다 — WM > 아이템 목록 뽑기 를 한 번 돌릴 것.");
				catalog = new WorldItemCatalog(null);
				return catalog;
			}

			catalog = new WorldItemCatalog(JsonUtility.FromJson<ItemCatalogData>(asset.text));
			return catalog;
		}
	}
}
