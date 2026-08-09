using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임의 가마솥이 <b>세계로 이어진 줄</b>을 타게 하는 어댑터 (TASK-WM-217 단계 4).
	///
	/// 건설 채널(<see cref="WorldLinkBuildChannel"/>)과 같은 수법 — 게임은 이미
	/// 「공유 가마솥 채널」이라는 구멍으로만 말한다. FishNet 이 채우던 그 구멍을 줄이 채우면
	/// <b>게임 쪽 코드는 그대로</b>고 통로만 갈린다.
	/// </summary>
	public sealed class WorldLinkBrewChannel : ISharedBrewChannel
	{
		private readonly IWorldLink link;

		// 마도서는 들어올 때 한 번 오고 잘 안 바뀐다 — 매 프레임 새 목록을 짓지 않는다.
		private SpellbookPage[] lastPages;
		private RecipeCatalogEntry[] pageCache = System.Array.Empty<RecipeCatalogEntry>();

		public WorldLinkBrewChannel(IWorldLink link)
		{
			this.link = link;
		}

		public bool IsActive => link != null && link.IsLinked;

		/// <summary>
		/// 「완성」 보상을 누가 줄 것인가 — <b>아무도 아니다.</b>
		///
		/// 호스트가 없는 세계라 「내가 host 인가」라는 물음 자체가 없어졌다. 이중지급을 막는 일은
		/// 이제 세계(서버)가 해야 한다 — 그전까지 이 창은 스스로 보상을 주지 않는다(적게 주는 쪽이 안전).
		/// ⚠ 서버가 보상을 판정하게 하는 것 = 후속 증분.
		/// </summary>
		public bool IsServerPeer => false;

		public void AddIngredient(int itemId) => link?.RequestBrewStep(itemId);

		/// <summary>내가 선 자리에서 가장 가까운 솥에 넣는다 — 없으면 false(짓거나 다가가야 한다).</summary>
		public bool TryUseNearbyCauldron(int itemId)
		{
			if (link == null)
				return false;

			CauldronView[] pots = link.Cauldrons;
			if (pots == null || pots.Length == 0)
				return false;

			float meX = 0f;
			float meZ = 0f;
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				meX = dolls[i].x;
				meZ = dolls[i].z;
				break;
			}

			CauldronView nearest = null;
			float best = float.MaxValue;
			for (int i = 0; i < pots.Length; i++)
			{
				float dx = pots[i].x - meX;
				float dz = pots[i].z - meZ;
				float distance = dx * dx + dz * dz;
				if (distance >= best)
					continue;

				best = distance;
				nearest = pots[i];
			}

			// 손이 닿는지는 세계가 다시 본다 — 여기서는 「가까운 것」만 고른다.
			if (nearest == null)
				return false;

			link.RequestBrewStepAt(itemId, nearest.x, nearest.y, nearest.z);
			return true;
		}

		public void AddStep(BrewStep step)
		{
			// ⚠ 이제 방향이 아니라 <b>재료</b>를 보낸다 (TASK-WM-217) — 여기로 오는 옛 호출은 뜻을 잃었다.
			//   게임 UI 가 재료 번호를 넘기도록 고칠 때까지, 방향만 아는 호출은 아무 일도 안 한다.
		}

		/// <summary>세계의 마도서 — 화면이 그리는 목표가 여기서 온다 (TASK-WM-217).</summary>
		public System.Collections.Generic.IReadOnlyList<RecipeCatalogEntry> Spellbook
		{
			get
			{
				SpellbookPage[] pages = link?.Spellbook;
				if (pages == null)
					return System.Array.Empty<RecipeCatalogEntry>();

				if (ReferenceEquals(pages, lastPages))
					return pageCache;

				RecipeCatalogEntry[] book = new RecipeCatalogEntry[pages.Length];
				for (int i = 0; i < pages.Length; i++)
				{
					SpellbookPage page = pages[i];
					book[i] = new RecipeCatalogEntry
					{
						id = page.id, name = page.name, targetX = page.x, targetY = page.y,
						radius = page.radius, resultItemId = page.itemId, amount = page.amount,
					};
				}

				lastPages = pages;
				pageCache = book;
				return pageCache;
			}
		}

		public void ResetBrew()
		{
			link?.RequestBrewReset();
		}

		public void RequestCompletion() => link?.RequestBrewComplete();

		/// <summary>세계가 정한 완성 — 무엇이 몇 개, 어느 등급으로 나왔나(가방에는 이미 들어가 있다).</summary>
		public bool TryTakeCompletionResult(out BrewCompletion completion)
		{
			WorldBrewView given = link?.TakeCompletedBrew();
			if (given == null)
			{
				completion = default;
				return false;
			}

			completion = new BrewCompletion
			{
				State = new BrewState
				{
					Position = new BrewVector(given.x, given.y),
					StepCount = given.steps,
					AccruedSideEffect = given.side,
				},
				RecipeName = given.recipe ?? string.Empty,
				Grade = (BrewGrade)given.grade,
				ResultItemId = given.itemId,
				Amount = given.amount,
			};
			return true;
		}

		public bool TryTakeCompletion(out BrewState taken)
		{
			WorldBrewView given = link?.TakeCompletedBrew();
			if (given == null)
			{
				taken = default;
				return false;
			}

			taken = new BrewState
			{
				Position = new BrewVector(given.x, given.y),
				StepCount = given.steps,
				AccruedSideEffect = given.side,
			};
			return true;
		}

		public bool TryGetState(out BrewVector position, out int stepCount, out float accruedSideEffect)
		{
			WorldBrewView brew = link?.Brew;
			if (brew == null)
			{
				position = BrewVector.Zero;
				stepCount = 0;
				accruedSideEffect = 0f;
				return false;
			}

			position = new BrewVector(brew.x, brew.y);
			stepCount = brew.steps;
			accruedSideEffect = brew.side;
			return true;
		}

		public void ReadSteps(List<BrewStep> buffer)
		{
			if (buffer == null)
				return;

			buffer.Clear();

			WorldBrewView brew = link?.Brew;
			if (brew?.path == null)
				return;

			for (int i = 0; i < brew.path.Length; i++)
			{
				BrewStepView step = brew.path[i];
				if (step == null)
					continue;

				buffer.Add(new BrewStep
				{
					Direction = new BrewVector(step.dx, step.dy),
					Grind = step.grind,
				});
			}
		}
	}
}
