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

			// 내 안의 세계도 <b>같은 규칙</b>으로 돈다 (TASK-WM-217) — 목록이 없으면 씨앗으로.
			// 안 꽂으면 혼자 놀 때만 아무것도 못 줍고 못 짓는 세계가 된다(같은 게임이 아니게 된다).
			// 이미 꽂혀 있으면 그대로 둔다 — 되살리기 전에 꽂아 둔 것을 덮으면 안 된다.
			if (world.Gatherables.KindCount == 0)
				world.Gatherables = new WorldGatherables(WorldSeeds.Gatherables());

			if (world.Ingredients.Count == 0)
				world.Ingredients = new WorldIngredients(WorldSeeds.Ingredients());

			if (world.Buildables.Count == 0)
				world.Buildables = BuildingCatalog.Loaded;

			me = world.Join();
		}

		public LocalWorldLink() : this(new WorldSim())
		{
		}

		/// <summary>내 안의 세계 본체 — 저장·불러오기 같은 일이 여기로 온다.</summary>
		public WorldSim World => world;

		public int MyDollId => me.Id;

		/// <summary>내 안의 세계에는 신원 장부가 없다 — 기기가 곧 나다.</summary>
		public int MyIdentityId => 0;

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

						// 이름도 같은 규약으로 실린다 — 혼자 놀 때만 이름표가 비면 그건 같은 게임이 아니다.
						name = snapshot[i].Id == me.Id && string.IsNullOrEmpty(myName) == false
							? myName
							: "손님 " + snapshot[i].Id,
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

		public void RequestPlace(int cellX, int cellY, int cellZ, int buildingId)
		{
			// 크기도 재료도 내 안의 세계가 목록에서 읽는다 — 혼자 놀 때와 같이 놀 때가 갈라지면 안 된다.
			world.Buildables.TryCost(buildingId, out int costItemId, out int costAmount);
			int missing = costAmount > 0 ? world.TryConsume(me.Id, costItemId, costAmount) : 0;
			if (missing > 0)
			{
				if (costAmount - missing > 0)
					world.TryGather(me.Id, ItemCatalog.Find(costItemId), costAmount - missing);

				// 혼자 놀 때도 이유는 말해 준다 — 조용히 실패하면 「고장」으로 읽힌다.
				Net.WorldNoticeBridge.Deliver("재료가 모자란다 — " + costAmount + "개가 든다");
				return;
			}

			if (world.TryPlaceBuilding(new Numerics.Vector3Int(cellX, cellY, cellZ), buildingId, world.Buildables) == false
				&& costAmount > 0)
			{
				world.TryGather(me.Id, ItemCatalog.Find(costItemId), costAmount); // 못 지었으면 재료를 돌려준다
			}
		}

		public void RequestRemove(int cellX, int cellY, int cellZ)
		{
			// 혼자 놀 때도 같은 규칙 — 부수면 재료 절반이 돌아온다.
			if (world.TryRemoveBuilding(new Numerics.Vector3Int(cellX, cellY, cellZ), out int removedBuildingId) == false)
				return;

			world.Buildables.TryCost(removedBuildingId, out int backItemId, out int backAmount);
			int refund = backAmount / 2;
			if (refund > 0)
				world.TryGather(me.Id, ItemCatalog.Find(backItemId), refund);
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

		public void RequestBrewStep(int itemId)
		{
			// 넣을 것이 가방에 <b>실제로 있어야</b> 들어간다 — 혼자여도 빈손으로는 못 젓는다.
			if (world.Ingredients.TryStep(itemId, out DomainSDK.Alchemy.BrewStep step) == false)
				return;

			if (world.TryConsume(me.Id, itemId, 1) != 0)
				return;

			world.Cauldron.AddStep(step);
		}

		public void RequestBrewReset() => world.Cauldron.ResetBrew();

		/// <summary>세계에 서 있는 솥들 — 혼자 놀 때도 자리마다 따로다.</summary>
		public CauldronView[] Cauldrons
		{
			get
			{
				List<Numerics.Vector3Int> cells = world.Cauldrons.Cells();
				List<CauldronView> views = new List<CauldronView>();
				for (int i = 0; i < cells.Count; i++)
				{
					WorldCauldron pot = world.Cauldrons.At(cells[i]);
					if (pot == null)
						continue;

					DomainSDK.Alchemy.BrewState state = pot.State;
					views.Add(new CauldronView
					{
						x = cells[i].x, y = cells[i].y, z = cells[i].z,
						px = state.Position.X, py = state.Position.Y,
						steps = state.StepCount, side = state.AccruedSideEffect,
					});
				}

				return views.ToArray();
			}
		}

		public void RequestBrewStepAt(int itemId, int cellX, int cellY, int cellZ)
		{
			Numerics.Vector3 standing = world.PositionOf(me.Id);
			WorldCauldron pot = world.Cauldrons.Reachable(new Numerics.Vector3Int(cellX, cellY, cellZ), standing.x, standing.z);
			if (pot == null)
				return;

			if (world.Ingredients.TryStep(itemId, out DomainSDK.Alchemy.BrewStep step) == false)
				return;

			if (world.TryConsume(me.Id, itemId, 1) != 0)
				return;

			pot.AddStep(step);
			world.Cauldrons.Touch();
		}

		public void RequestBrewResetAt(int cellX, int cellY, int cellZ)
		{
			Numerics.Vector3 standing = world.PositionOf(me.Id);
			WorldCauldron pot = world.Cauldrons.Reachable(new Numerics.Vector3Int(cellX, cellY, cellZ), standing.x, standing.z);
			if (pot == null)
				return;

			pot.ResetBrew();
			world.Cauldrons.Touch();
		}

		public void RequestBrewCompleteAt(int cellX, int cellY, int cellZ)
		{
			Numerics.Vector3 standing = world.PositionOf(me.Id);
			WorldCauldron pot = world.Cauldrons.Reachable(new Numerics.Vector3Int(cellX, cellY, cellZ), standing.x, standing.z);
			if (pot == null)
				return;

			// 받을 자리부터 — 완성은 되돌릴 수 없다.
			BrewCompletion peek = RecipeBook.Loaded.Judge(pot.State);
			if (peek.Empty == false && world.CanReceive(me.Id, ItemCatalog.Find(peek.ResultItemId), peek.Amount) == false)
				return;

			if (pot.TryComplete(RecipeBook.Loaded, out BrewCompletion taken) == false)
				return;

			if (taken.Empty == false)
				world.TryGather(me.Id, ItemCatalog.Find(taken.ResultItemId), taken.Amount);

			world.Cauldrons.Touch();
			completed = new WorldBrewView
			{
				x = taken.State.Position.X, y = taken.State.Position.Y,
				steps = taken.State.StepCount, side = taken.State.AccruedSideEffect,
				itemId = taken.ResultItemId, amount = taken.Amount,
				grade = (int)taken.Grade, recipe = taken.RecipeName,
			};
		}

		public void RequestBrewComplete()
		{
			// 혼자여도 규칙은 같다 — 세계가 내주고(마도서 판정), 빈 솥이면 아무 일도 없다.
			// ★ 받을 자리부터 본다: 완성은 되돌릴 수 없다(만들었는데 사라지면 안 된다).
			BrewCompletion peek = RecipeBook.Loaded.Judge(world.Cauldron.State);
			if (peek.Empty == false && world.CanReceive(me.Id, ItemCatalog.Find(peek.ResultItemId), peek.Amount) == false)
				return;

			if (world.Cauldron.TryComplete(RecipeBook.Loaded, out BrewCompletion taken) == false)
				return;

			if (taken.Empty == false)
				world.TryGather(me.Id, ItemCatalog.Find(taken.ResultItemId), taken.Amount);

			completed = new WorldBrewView
			{
				x = taken.State.Position.X,
				y = taken.State.Position.Y,
				steps = taken.State.StepCount,
				side = taken.State.AccruedSideEffect,
				itemId = taken.ResultItemId,
				amount = taken.Amount,
				grade = (int)taken.Grade,
				recipe = taken.RecipeName,
			};
		}

		public WorldBrewView TakeCompletedBrew()
		{
			WorldBrewView taken = completed;
			completed = null;
			return taken;
		}

		public GatherableView[] Gatherables
		{
			get
			{
				List<GatherableNode> alive = world.Gatherables.Alive(world.Calendar.TotalMinutes());
				GatherableView[] views = new GatherableView[alive.Count];
				for (int i = 0; i < alive.Count; i++)
				{
					views[i] = new GatherableView
					{
						id = alive[i].Id,
						x = alive[i].X,
						z = alive[i].Z,
						itemId = alive[i].ItemId,
						amount = alive[i].Amount,
					};
				}

				return views;
			}
		}

		public void RequestGather(int nodeId)
		{
			// 손이 닿아야 줍힌다 — 혼자 놀 때도 같은 규칙이다(아니면 두 세계가 갈라진다).
			Numerics.Vector3 standing = world.PositionOf(me.Id);
			if (world.Gatherables.TryTake(nodeId, standing.x, standing.z, world.Calendar.TotalMinutes(),
				out int itemId, out int amount) == false)
			{
				Net.WorldNoticeBridge.Deliver("손이 안 닿는다");
				return;
			}

			// 가방이 꽉 차서 못 받으면 도로 세운다 — 혼자 놀 때도 같은 규칙이다.
			int leftover = world.TryGather(me.Id, ItemCatalog.Find(itemId), amount);
			if (leftover >= amount)
				world.Gatherables.Restore(nodeId);
		}

		public void RequestConsume(int itemId, int amount)
		{
			world.TryConsume(me.Id, itemId, amount);
		}

		/// <summary>마지막으로 들여다본 상자 — 혼자 놀 때도 같은 규약이다.</summary>
		public ChestView Chest { get; private set; }

		/// <summary>
		/// 내 안의 세계가 쓰는 마도서 — <b>같은 것</b>을 화면에 준다 (TASK-WM-217).
		/// 혼자 놀 때만 화면이 다른 목표를 그리면, 혼자/같이가 또 갈라진다.
		/// </summary>
		/// <summary>내 안의 세계가 아는 지을 것 — <b>같은 목록</b>을 화면에 준다 (TASK-WM-217).</summary>
		public BuildCatalogEntryView[] BuildCatalog
		{
			get
			{
				System.Collections.Generic.IReadOnlyList<BuildingCatalogEntry> catalog = world?.Buildables?.All;
				if (catalog == null)
					return System.Array.Empty<BuildCatalogEntryView>();

				BuildCatalogEntryView[] view = new BuildCatalogEntryView[catalog.Count];
				for (int i = 0; i < catalog.Count; i++)
				{
					BuildingCatalogEntry entry = catalog[i];
					view[i] = new BuildCatalogEntryView
					{
						buildingId = entry.id, name = entry.name, w = entry.w, l = entry.l,
						costItemId = entry.costItemId, costAmount = entry.costAmount,
					};
				}

				return view;
			}
		}

		/// <summary>
		/// 내 안의 세계가 아는 제작표 — <b>같은 규칙</b>으로 판정한다 (TASK-WM-217).
		/// 혼자 놀 때만 창이 굴리면 혼자/같이가 또 갈라진다.
		/// </summary>
		public CraftBookEntryView[] CraftBook
		{
			get
			{
				System.Collections.Generic.IReadOnlyList<CraftRecipeEntry> recipes = CraftBookOf.Loaded.Recipes;
				CraftBookEntryView[] view = new CraftBookEntryView[recipes.Count];
				for (int i = 0; i < recipes.Count; i++)
				{
					CraftRecipeEntry recipe = recipes[i];
					CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();

					int[] itemIds = new int[items.Length];
					int[] amounts = new int[items.Length];
					for (int need = 0; need < items.Length; need++)
					{
						itemIds[need] = items[need].itemId;
						amounts[need] = items[need].amount;
					}

					view[i] = new CraftBookEntryView
					{
						recipeId = recipe.id, name = recipe.name,
						resultItemId = recipe.resultItemId, resultAmount = recipe.resultAmount,
						percentage = recipe.percentage <= 0f ? 100f : recipe.percentage,
						itemIds = itemIds, amounts = amounts,
					};
				}

				return view;
			}
		}

		private string myName = string.Empty;

		/// <summary>
		/// 내 안의 세계에서도 이름을 정한다 (TASK-WM-218) — 겹칠 남이 없으니 길이만 본다.
		/// 혼자 놀 때만 이름이 안 되면 혼자/같이가 또 갈라진다.
		/// </summary>
		public void RequestRename(string name)
		{
			string trimmed = name == null ? string.Empty : name.Trim();
			if (trimmed.Length < Identity.WorldIdentityRegistry.MIN_NAME
				|| trimmed.Length > Identity.WorldIdentityRegistry.MAX_NAME)
			{
				WorldNoticeBridge.Deliver(trimmed.Length < Identity.WorldIdentityRegistry.MIN_NAME
					? "이름이 너무 짧다"
					: "이름이 너무 길다");

				return;
			}

			myName = trimmed;
		}

		private CraftedMessage crafted;

		/// <summary>
		/// 혼자 놀아도 <b>세계가 판정한다</b> (TASK-WM-217) — 재료를 쓰고, 주사위를 굴리고, 넣어 준다.
		/// 실패해도 재료는 든다(그게 주사위를 굴리는 값이다).
		/// </summary>
		public void RequestCraft(int recipeId)
		{
			CraftResult judged = CraftBookOf.Loaded.Judge(
				recipeId,
				itemId => world.BagCount(me.Id, itemId),
				UnityEngine.Random.Range(0f, 100f));

			if (judged.Attempted == false)
			{
				crafted = Result(judged);
				return;
			}

			// 받을 자리부터 본다 — 만들고 나서 못 받으면 재료만 사라진다.
			if (judged.Succeeded
				&& world.CanReceive(me.Id, ItemCatalog.Find(judged.ResultItemId), judged.ResultAmount) == false)
			{
				crafted = new CraftedMessage
				{
					recipeId = recipeId, attempted = false, succeeded = false,
					denied = "가방이 꽉 찼다 — 비우고 다시 오면 재료는 그대로다",
				};

				return;
			}

			CraftRecipeEntry recipe = CraftBookOf.Loaded.Find(recipeId);
			CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i] == null || items[i].amount <= 0)
					continue;

				world.TryConsume(me.Id, items[i].itemId, items[i].amount);
			}

			if (judged.Succeeded)
				world.TryGather(me.Id, ItemCatalog.Find(judged.ResultItemId), judged.ResultAmount);

			crafted = Result(judged);
		}

		public CraftedMessage TakeCraftResult()
		{
			CraftedMessage taken = crafted;
			crafted = null;
			return taken;
		}

		private static CraftedMessage Result(CraftResult judged)
		{
			return new CraftedMessage
			{
				recipeId = judged.RecipeId, attempted = judged.Attempted, succeeded = judged.Succeeded,
				itemId = judged.ResultItemId, amount = judged.ResultAmount, denied = judged.Denied ?? string.Empty,
			};
		}

		private readonly System.Collections.Generic.List<SaidMessage> heard =
			new System.Collections.Generic.List<SaidMessage>();

		private readonly System.Collections.Generic.List<HurtMessage> hurts =
			new System.Collections.Generic.List<HurtMessage>();

		/// <summary>
		/// 혼자 노는 세계에서도 말은 한다 (TASK-WM-261) — 들을 남이 없으니 <b>나에게</b> 돌아온다.
		///
		/// ★ 왜 그냥 버리지 않나: 혼자와 같이가 갈리면 그게 곧 두 벌의 게임이다.
		///   말이 화면에 뜨는 <b>길</b>은 두 경우가 같아야 한다 — 다듬는 규칙(SaidLine)도 같은 것을 쓴다.
		/// </summary>
		public void RequestSay(string line)
		{
			string clean = SaidLine.Clean(line);
			if (clean == null)
				return;

			heard.Add(new SaidMessage { dollId = me.Id, name = myName ?? string.Empty, text = clean });
		}

		public SaidMessage[] TakeHeard()
		{
			if (heard.Count == 0)
				return System.Array.Empty<SaidMessage>();

			SaidMessage[] taken = heard.ToArray();
			heard.Clear();
			return taken;
		}

		/// <summary>혼자 노는 세계에서 때린다 — 판정은 <b>같은 규칙</b>(WorldSim.TryStrike)이 본다.</summary>
		public void RequestStrike(int targetDollId)
		{
			if (world.TryStrike(me.Id, targetDollId, System.Environment.TickCount64,
				out int healthLeft, out bool wentDown) != StrikeRule.Denial.None)
			{
				return;
			}

			hurts.Add(new HurtMessage { dollId = targetDollId, by = me.Id, health = healthLeft, down = wentDown });
		}

		public HurtMessage[] TakeHurts()
		{
			if (hurts.Count == 0)
				return System.Array.Empty<HurtMessage>();

			HurtMessage[] taken = hurts.ToArray();
			hurts.Clear();
			return taken;
		}

		/// <summary>내 안의 세계는 하나뿐이라 국경이 없다 — 넘어갈 곳이 없으니 늘 null.</summary>
		public MoveOnMessage TakeMoveOn() => null;

		/// <summary>혼자 노는 세계도 이름을 안다 — 게임 자산이 정본이라 여기선 빈 목록으로 둔다.</summary>
		public CatalogEntry[] ItemNames => System.Array.Empty<CatalogEntry>();

		public SpellbookPage[] Spellbook
		{
			get
			{
				System.Collections.Generic.IReadOnlyList<RecipeCatalogEntry> pages = RecipeBook.Loaded.Pages;
				SpellbookPage[] view = new SpellbookPage[pages.Count];
				for (int i = 0; i < pages.Count; i++)
				{
					RecipeCatalogEntry page = pages[i];
					view[i] = new SpellbookPage
					{
						id = page.id, name = page.name, x = page.targetX, y = page.targetY,
						radius = page.radius, itemId = page.resultItemId, amount = page.amount,
					};
				}

				return view;
			}
		}

		public void RequestChest(int cellX, int cellY, int cellZ)
		{
			Chest = Look(new Numerics.Vector3Int(cellX, cellY, cellZ));
		}

		public void RequestChestPut(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Numerics.Vector3Int cell = new Numerics.Vector3Int(cellX, cellY, cellZ);
			Numerics.Vector3 standing = world.PositionOf(me.Id);

			// 가방에서 먼저 뺀다 — 넣다 남으면 도로 돌려준다(사라지는 물건은 없다).
			int missing = world.TryConsume(me.Id, itemId, amount);
			int moving = amount - missing;
			if (moving > 0)
			{
				int leftover = world.Storages.Put(cell, ItemCatalog.Find(itemId), moving, standing.x, standing.z);
				if (leftover > 0)
					world.TryGather(me.Id, ItemCatalog.Find(itemId), leftover);
			}

			Chest = Look(cell);
		}

		public void RequestChestTake(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Numerics.Vector3Int cell = new Numerics.Vector3Int(cellX, cellY, cellZ);
			Numerics.Vector3 standing = world.PositionOf(me.Id);

			int taken = world.Storages.Take(cell, itemId, amount, standing.x, standing.z);
			if (taken > 0)
			{
				int leftover = world.TryGather(me.Id, ItemCatalog.Find(itemId), taken);
				if (leftover > 0)
					world.Storages.Put(cell, ItemCatalog.Find(itemId), leftover, standing.x, standing.z);
			}

			Chest = Look(cell);
		}

		private ChestView Look(Numerics.Vector3Int cell)
		{
			List<BagSaveEntry> contents = world.Storages.Contents(cell);
			BagEntry[] items = new BagEntry[contents.Count];
			for (int i = 0; i < contents.Count; i++)
				items[i] = new BagEntry { itemId = contents[i].itemId, amount = contents[i].amount };

			return new ChestView { x = cell.x, y = cell.y, z = cell.z, items = items };
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

		/// <summary>목록 그대로 — 상자 안을 되살릴 때 「이 번호가 무엇인가」를 알아야 한다.</summary>
		public static WorldItemCatalog Loaded => Ensure();

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

	/// <summary>
	/// 내 안의 세계가 아는 <b>지을 것</b> 목록 (TASK-WM-217) — 아이템 목록과 같은 방식.
	/// 뽑아 둔 <c>Resources/buildings.json</c> 이 있으면 그것을, 없으면 씨앗으로.
	/// </summary>
	public static class BuildingCatalog
	{
		private const string RESOURCE_NAME = "buildings";

		private static WorldBuildingCatalog catalog;

		public static WorldBuildingCatalog Loaded => catalog ?? (catalog = Load());

		private static WorldBuildingCatalog Load()
		{
			TextAsset asset = Resources.Load<TextAsset>(RESOURCE_NAME);
			if (asset == null)
			{
				Debug.LogWarning("[buildings] Resources/buildings.json 이 없다 — WM > 아이템 목록 뽑기 를 한 번 돌릴 것(씨앗으로 돈다).");
				return new WorldBuildingCatalog(WorldSeeds.Buildings());
			}

			WorldBuildingCatalog fromAsset = new WorldBuildingCatalog(JsonUtility.FromJson<BuildingCatalogData>(asset.text));
			return fromAsset.Count > 0 ? fromAsset : new WorldBuildingCatalog(WorldSeeds.Buildings());
		}
	}

	/// <summary>
	/// 내 안의 세계가 든 마도서 (TASK-WM-217) — 완성이 무엇을 주는지의 정본.
	/// 아직 뽑는 도구가 없으므로 씨앗으로 돈다(서버와 <b>같은</b> 씨앗이라 갈라지지 않는다).
	/// </summary>
	/// <summary>내 안의 세계가 든 제작표 — 씨앗으로 돈다(진짜 자산은 뽑아서 꽂는다).</summary>
	public static class CraftBookOf
	{
		private static WorldCraftBook book;

		public static WorldCraftBook Loaded => book ?? (book = new WorldCraftBook(WorldSeeds.Crafts()));
	}

	public static class RecipeBook
	{
		private static WorldRecipeBook book;

		public static WorldRecipeBook Loaded => book ?? (book = new WorldRecipeBook(WorldSeeds.Recipes()));
	}
}
