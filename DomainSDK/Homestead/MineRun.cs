using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	public sealed class MineNode
	{
		public MineNodeDef Def { get; internal set; }
		public int HitsLeft { get; internal set; }
		public bool HidesPassage { get; internal set; }
		public bool IsBroken => HitsLeft <= 0;
	}

	public readonly struct MineStrike
	{
		public readonly HomesteadResult Result;
		public readonly bool WasGlint;
		public readonly bool Broke;
		public readonly bool FoundPassage;

		public MineStrike(HomesteadResult result, bool wasGlint, bool broke, bool foundPassage)
		{
			Result = result;
			WasGlint = wasGlint;
			Broke = broke;
			FoundPassage = foundPassage;
		}
	}

	// 한 번 내려가 올라올 때까지. 등불이 다하면 더 못 캐고 올라올 뿐, 캔 것은 이미 창고에
	public sealed class MineRun
	{
		private readonly HomesteadSim sim;
		private readonly List<MineNode> nodes = new List<MineNode>();

		public int Depth { get; private set; }
		public int Lantern { get; private set; }
		public int LanternMax { get; }
		public bool PassageOpen { get; private set; }
		public IReadOnlyList<MineNode> Nodes => nodes;
		public MineBandDef Band { get; private set; }

		internal MineRun(HomesteadSim sim, int lantern)
		{
			this.sim = sim;
			Lantern = lantern;
			LanternMax = lantern;
		}

		internal void Enter(int depth)
		{
			Depth = depth;
			Band = sim.BandAt(depth);
			PassageOpen = false;
			nodes.Clear();
			for (int i = 0; i < Band.Nodes; i++)
			{
				MineNodeDef def = sim.RollMineNode(Band);
				nodes.Add(new MineNode { Def = def, HitsLeft = def.Hits });
			}
			PickPassage();
			sim.ReachDepth(depth);
		}

		// 내리막은 지금 도구로 깰 수 있는 바위에만 숨음. 막다른 층이 생기지 않게
		// 깰 것이 하나도 없는 층은 내리막이 열린 채 시작
		private void PickPassage()
		{
			List<MineNode> breakable = nodes.FindAll(node => node.Def.Tier <= sim.ToolTier(ToolKind.Pickaxe));
			if (breakable.Count == 0)
			{
				PassageOpen = true;
				return;
			}
			int hidden = Math.Max(1, (int)Math.Round(breakable.Count * sim.Catalog.Tuning.PassageShare));
			for (int i = 0; i < hidden; i++)
			{
				int pick = sim.RandomIndex(breakable.Count);
				breakable[pick].HidesPassage = true;
				breakable.RemoveAt(pick);
				if (breakable.Count == 0)
				{
					break;
				}
			}
		}

		// phase 는 균열 빛이 도는 0..1. 빛나는 구간에 치면 곡괭이 힘의 몇 배 (설계: 세 배쯤)
		public bool IsGlint(float phase) => phase >= 1f - sim.Catalog.Tuning.GlintWindow;

		public MineStrike Strike(int index, float phase)
		{
			MineNode node = nodes[index];
			if (node.IsBroken)
			{
				return new MineStrike(HomesteadResult.Fail(HomesteadFailure.AlreadyDone), false, false, false);
			}
			if (Lantern <= 0)
			{
				return new MineStrike(HomesteadResult.Fail(HomesteadFailure.LanternOut), false, false, false);
			}
			// 약한 도구는 흠집만. 등불도 안 닳음
			if (sim.ToolTier(ToolKind.Pickaxe) < node.Def.Tier)
			{
				return new MineStrike(HomesteadResult.Fail(HomesteadFailure.ToolTooWeak, ToolKind.Pickaxe.ToString()), false, false, false);
			}

			Lantern--;
			bool glint = IsGlint(phase);
			int power = sim.ToolPower(ToolKind.Pickaxe);
			if (glint)
			{
				power = (int)Math.Round(power * sim.Catalog.Tuning.GlintMultiplier);
			}
			node.HitsLeft = Math.Max(0, node.HitsLeft - power);
			if (node.IsBroken == false)
			{
				return new MineStrike(HomesteadResult.Success, glint, false, false);
			}

			bool found = node.HidesPassage && PassageOpen == false;
			if (found)
			{
				PassageOpen = true;
			}
			sim.BreakMineNode(node.Def, Band);
			return new MineStrike(HomesteadResult.Success, glint, true, found);
		}

		// 등불이 다하면 올라올 뿐. 마지막 한 번에 연 내리막이어도 더 내려가지 않음
		public HomesteadResult CanDescend()
		{
			if (PassageOpen == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			if (Depth >= sim.MineMaxDepth)
			{
				return HomesteadResult.Fail(HomesteadFailure.AtMaxLevel);
			}
			if (Lantern <= 0)
			{
				return HomesteadResult.Fail(HomesteadFailure.LanternOut);
			}
			return HomesteadResult.Success;
		}

		public HomesteadResult Descend()
		{
			HomesteadResult result = CanDescend();
			if (result.Ok)
			{
				Enter(Depth + 1);
			}
			return result;
		}
	}
}
