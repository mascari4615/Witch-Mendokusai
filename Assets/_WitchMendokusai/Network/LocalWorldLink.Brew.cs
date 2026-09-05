using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// LocalWorldLink 의 솥과 제조 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 LocalWorldLink.cs 를 본다.
	public sealed partial class LocalWorldLink : IWorldLink
	{
		private WorldBrewView completed;

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
	}
}
