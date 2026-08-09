using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	/// <summary>
	/// 모두가 같이 젓는 <b>하나의 솥</b> (TASK-WM-217 단계 4).
	///
	/// ★ 왜 세계가 갖는가: 지금 솥은 FishNet 호스트가 굴린다(CauldronNetworkBridge) —
	///   호스트가 나가면 젓던 게 사라진다. 솥은 세계의 물건이지 어느 한 사람의 물건이 아니다.
	///
	/// 젓는 규칙은 게임과 <b>같은 것</b>(<see cref="BrewEngine"/>)을 쓴다 — 서버가 유니티가 아니어도 같은 답이 나온다.
	/// </summary>
	public sealed class WorldCauldron
	{
		private readonly object gate = new object();
		private readonly List<BrewStep> steps = new List<BrewStep>();
		private BrewState state = BrewState.Start;

		/// <summary>한 번 저을 때마다 오른다 — 창이 「내 화면이 낡았나」를 이 수로 안다.</summary>
		public int Version { get; private set; }

		/// <summary>지금 솥의 상태 — 그 순간의 사본.</summary>
		public BrewState State
		{
			get
			{
				lock (gate)
				{
					return state;
				}
			}
		}

		/// <summary>재료를 한 번 넣고 젓는다. 누가 넣든 <b>같은 솥</b>에 쌓인다.</summary>
		public void AddStep(BrewStep step)
		{
			lock (gate)
			{
				steps.Add(step);
				state = BrewEngine.Apply(state, step);
				Version++;
			}
		}

		/// <summary>
		/// 「완성」을 <b>세계가 한 사람에게만 준다</b> (TASK-WM-217).
		///
		/// ★ 왜 세계인가: 전에는 호스트만 완성 버튼을 눌렀다(이중지급 방지). 호스트가 없는 세계에서는
		///   그 규칙이 「아무도 못 누른다」가 되어 기능이 죽는다. 대신 세계가 <b>선착순 한 번</b>만 내준다 —
		///   같은 순간에 둘이 눌러도 뒤엣사람은 빈 솥을 받는다(자물쇠 안에서 뜨고 비운다).
		/// </summary>
		public bool TryComplete(out BrewState completed)
		{
			lock (gate)
			{
				completed = state;
				if (state.StepCount <= 0)
					return false;

				steps.Clear();
				state = BrewState.Start;
				Version++;
				return true;
			}
		}

		/// <summary>솥을 비운다(같은 솥, 처음부터).</summary>
		public void ResetBrew()
		{
			lock (gate)
			{
				steps.Clear();
				state = BrewState.Start;
				Version++;
			}
		}

		/// <summary>지금까지 저은 길 — 경로선을 그리는 쪽이 읽어 간다.</summary>
		public void ReadSteps(List<BrewStep> buffer)
		{
			if (buffer == null)
				return;

			buffer.Clear();
			lock (gate)
			{
				for (int i = 0; i < steps.Count; i++)
					buffer.Add(steps[i]);
			}
		}
	}
}
