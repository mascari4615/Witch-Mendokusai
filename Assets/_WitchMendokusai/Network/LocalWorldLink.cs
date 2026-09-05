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
	public sealed partial class LocalWorldLink : IWorldLink
	{
		private readonly WorldSim world;
		private readonly WorldDoll me;

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
			// `Environment.TickCount64` 는 .NET Standard **2.1** 것이다. 이 프로젝트의 API 수준은
			// 2.0(`apiCompatibilityLevel: 6`) 이라 에디터에서는 넘어가도 **플레이어 빌드에서 컴파일이
			// 깨진다** — 실제로 이 한 줄 때문에 야간 빌드가 나흘간 죽었고, 그 빌드를 먹는 런타임
			// 관문(부팅·2인 동기)도 같이 빨갛게 멈춰 있었다.
			// 여기 시각은 차이(쿨다운·되감기)를 재는 데만 쓰이므로 유닉스 밀리초로 충분하다.
			if (world.TryStrike(me.Id, targetDollId, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
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
	}
}
