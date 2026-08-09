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

		public void AddStep(BrewStep step)
		{
			link?.RequestBrewStep(step.Direction.X, step.Direction.Y, step.Grind);
		}

		public void ResetBrew()
		{
			link?.RequestBrewReset();
		}

		public void RequestCompletion() => link?.RequestBrewComplete();

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
