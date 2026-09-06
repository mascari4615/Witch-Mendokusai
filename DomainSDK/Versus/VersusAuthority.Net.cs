using System.Collections.Generic;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// VersusAuthority.cs 의 Net 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 받기와 보내기.
	public sealed partial class VersusAuthority
	{
		private void ReceiveAll()
		{
			for (int seat = 0; seat < transports.Length; seat++)
			{
				IVersusTransport transport = transports[seat];

				if (transport == null)
					continue;

				transport.Drain(incoming);

				for (int index = 0; index < incoming.Count; index++)
					Receive(seat, incoming[index]);
			}
		}

		private void Receive(int seat, string message)
		{
			string type = codec.TypeOf(message);

			if (type == VersusMessageType.INPUT)
			{
				VersusInputMessage input = codec.Decode<VersusInputMessage>(message);

				if (input == null)
					return;

				frames[seat] = new VersusInputFrame
				{
					Move = new Vector2(input.moveX, input.moveY),
					Aim = new Vector2(input.aimX, input.aimY),
					Fire = input.fire,
					Dash = input.dash,
				};
				return;
			}

			if (type == VersusMessageType.PICK)
			{
				VersusPickMessage pick = codec.Decode<VersusPickMessage>(message);

				if (pick != null)
					pickedOffer[seat] = pick.index;

				return;
			}

			if (type == VersusMessageType.REMATCH)
				RequestRematch(seat);
		}

		// 두 사람이 이번 틱에 한 것을 적어 둔다 — 상대에게 보내 줘야 그쪽이 되감아 다시 굴릴 수 있다.
		private void RecordInputs()
		{
			for (int seat = 0; seat < inputLog.Length; seat++)
			{
				inputLog[seat].Add(new VersusRemoteInput
				{
					tick = roundTick,
					moveX = frames[seat].Move.x,
					moveY = frames[seat].Move.y,
					aimX = frames[seat].Aim.x,
					aimY = frames[seat].Aim.y,
					fire = frames[seat].Fire,
					dash = frames[seat].Dash,
				});
			}
		}

		// 스냅샷은 <b>각자에게 따로</b> 간다 — 창마다 「상대」가 다르기 때문이다.
		private void SendSnapshots()
		{
			VersusRoundSnapshot snapshot = Round.Capture(roundTick);

			for (int seat = 0; seat < transports.Length; seat++)
			{
				if (transports[seat] == null || transports[seat].IsOpen == false)
					continue;

				int opponent = 1 - seat;
				List<VersusRemoteInput> since = new List<VersusRemoteInput>();

				for (int index = 0; index < inputLog[opponent].Count; index++)
				{
					if (inputLog[opponent][index].tick > lastSnapshotTick)
						since.Add(inputLog[opponent][index]);
				}

				SendTo(seat, new VersusSnapshotMessage
				{
					snapshot = snapshot,
					opponentInputs = since.ToArray(),
					scoreA = Match.ScoreOf(0),
					scoreB = Match.ScoreOf(1),
				});
			}

			lastSnapshotTick = roundTick;

			// 보낸 것은 버린다 — 한 판 내내 들고 있을 이유가 없다.
			inputLog[0].Clear();
			inputLog[1].Clear();
		}

		private void BroadcastState()
		{
			Round.CollectShots(shotBuffer);

			VersusBodyMessage[] fighters = new VersusBodyMessage[MatchConstants.VERSUS_PLAYER_COUNT];
			for (int seat = 0; seat < fighters.Length; seat++)
			{
				Vector2 position = Round.PositionOf(seat);
				fighters[seat] = new VersusBodyMessage
				{
					x = position.x,
					y = position.y,
					r = Round.RadiusOf(seat),
					owner = seat,
					alive = Round.IsAlive(seat),
				};
			}

			VersusBodyMessage[] shots = new VersusBodyMessage[shotBuffer.Count];
			for (int index = 0; index < shots.Length; index++)
			{
				shots[index] = new VersusBodyMessage
				{
					x = shotBuffer[index].Position.x,
					y = shotBuffer[index].Position.y,
					r = shotBuffer[index].Radius,
					owner = shotBuffer[index].Owner,
					alive = true,
				};
			}

			Broadcast(new VersusStateMessage
			{
				tick = tick,
				fighters = fighters,
				shots = shots,
				scoreA = Match.ScoreOf(0),
				scoreB = Match.ScoreOf(1),
			});
		}

		private void Broadcast(object message)
		{
			string text = null;

			for (int seat = 0; seat < transports.Length; seat++)
			{
				if (transports[seat] == null || transports[seat].IsOpen == false)
					continue;

				text = text ?? codec.Encode(message);
				transports[seat].Send(text);
			}
		}

		private void SendTo(int seat, object message)
		{
			if (transports[seat] == null || transports[seat].IsOpen == false)
				return;

			transports[seat].Send(codec.Encode(message));
		}
	}
}

