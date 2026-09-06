using System.Collections.Generic;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// VersusAuthority.cs 의 Rematch 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 재대결.
	public sealed partial class VersusAuthority
	{
		/// <summary>「한 판 더」에 손 든 사람 수 — 화면이 「1/2 기다리는 중」을 띄우는 데 쓴다.</summary>
		public int RematchReady
		{
			get
			{
				int ready = 0;

				for (int seat = 0; seat < wantsRematch.Length; seat++)
				{
					if (wantsRematch[seat])
						ready++;
				}

				return ready;
			}
		}

		/// <summary>몇 명이 손을 들어야 새 판이 서나 — 봇 자리는 항상 준비된 것으로 친다.</summary>
		public int RematchNeeded
		{
			get
			{
				int needed = 0;

				for (int seat = 0; seat < isBot.Length; seat++)
				{
					if (isBot[seat] == false)
						needed++;
				}

				return needed < 1 ? 1 : needed;
			}
		}

		private void RequestRematch(int seat)
		{
			if (Match.IsConcluded == false)
				return;

			wantsRematch[seat] = true;
			Broadcast(new VersusRematchStateMessage { ready = RematchReady, needed = RematchNeeded });
		}

		// 손 든 사람이 다 차면 <b>완전히 새 판</b>을 연다 — 카드도 점수도 처음부터.
		// 씨앗을 바꾸는 이유: 같은 씨앗이면 카드 후보 순서가 똑같아 「또 그 카드」가 된다.
		private void TickRematch()
		{
			if (RematchReady < RematchNeeded)
				return;

			for (int seat = 0; seat < wantsRematch.Length; seat++)
			{
				wantsRematch[seat] = false;
				pickedOffer[seat] = -1;
			}

			matchSeed = matchSeed * 31 + 17;
			Match = new VersusMatchCore(rulesForRematch, matchSeed);
			tick = 0;
			StartRound();
		}
	}
}

