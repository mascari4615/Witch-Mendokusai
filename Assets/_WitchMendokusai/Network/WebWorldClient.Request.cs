using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// WebWorldClient 의 보내는 부탁 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WebWorldClient.cs 를 본다.
	public sealed partial class WebWorldClient : MonoBehaviour, IWorldLink
	{
		/// <summary>「이쪽으로 가고 싶다」를 보낸다. 얼마나 갈지는 서버가 정한다.</summary>
		public void RequestMove(float x, float z) => Send(JsonUtility.ToJson(new MoveMessage { x = x, z = z }));

		/// <summary>솥을 한 번 젓는다 — 모두가 같은 솥을 젓는다.</summary>
		public void RequestBrewStep(int itemId) => Send(JsonUtility.ToJson(new BrewMessage { itemId = itemId }));

		public void RequestBrewStepAt(int itemId, int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW, itemId = itemId, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestBrewResetAt(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW_RESET, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestBrewCompleteAt(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new CauldronMessage { type = NetMessageType.BREW_COMPLETE, x = cellX, y = cellY, z = cellZ }));
		}

		/// <summary>솥을 비운다.</summary>
		public void RequestBrewReset() => Send(JsonUtility.ToJson(new BrewResetMessage()));

		/// <summary>완성을 달라고 한다 — 줄지는 서버가 정한다(선착순 한 번).</summary>
		public void RequestBrewComplete() => Send(JsonUtility.ToJson(new BrewCompleteMessage()));

		/// <summary>서버가 내준 완성. 한 번 읽으면 비운다(두 번 채점하지 않게).</summary>
		public WorldBrewView TakeCompletedBrew()
		{
			WorldBrewView taken = completed;
			completed = null;
			return taken;
		}

		/// <summary>「이 칸을 부수고 싶다」 — 정말 사라질지는 서버가 정한다.</summary>
		public void RequestRemove(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new RemoveMessage { x = cellX, y = cellY, z = cellZ }));
		}

		/// <summary>「여기에 짓고 싶다」 — 겹치는지는 서버가 본다.</summary>
		public void RequestPlace(int cellX, int cellY, int cellZ, int buildingId)
		{
			PlaceMessage message = new PlaceMessage
			{
				x = cellX,
				y = cellY,
				z = cellZ,
				buildingId = buildingId,
			};

			Send(JsonUtility.ToJson(message));
		}

		/// <summary>「이걸 줍고 싶다」 — 가방에 들어갈지는 서버가 본다.</summary>
		public void RequestGather(int nodeId) => Send(JsonUtility.ToJson(new GatherMessage { nodeId = nodeId }));

		private CraftedMessage crafted;

		/// <summary>나를 이렇게 불러 달라 — 되나 안 되나는 세계가 본다.</summary>
		public void RequestRename(string name)
		{
			Send(JsonUtility.ToJson(new RenameMessage { name = name }));
		}

		private readonly System.Collections.Generic.List<SaidMessage> heard =
			new System.Collections.Generic.List<SaidMessage>();

		private readonly System.Collections.Generic.List<HurtMessage> hurts =
			new System.Collections.Generic.List<HurtMessage>();

		private MoveOnMessage moveOn;

		/// <summary>이렇게 말했다 — 다듬는 것도 자르는 것도 세계가 한다 (TASK-WM-261).</summary>
		public void RequestSay(string line)
		{
			Send(JsonUtility.ToJson(new SayMessage { text = line ?? string.Empty }));
		}

		/// <summary>들린 말 — 한 번 읽으면 비운다(두 번 뜨지 않게).</summary>
		public SaidMessage[] TakeHeard()
		{
			if (heard.Count == 0)
				return System.Array.Empty<SaidMessage>();

			SaidMessage[] taken = heard.ToArray();
			heard.Clear();
			return taken;
		}

		/// <summary>저 사람을 때린다 — 거리·간격·대상은 세계가 본다 (TASK-WM-261).</summary>
		public void RequestStrike(int targetDollId)
		{
			Send(JsonUtility.ToJson(new StrikeMessage { targetId = targetDollId }));
		}

		/// <summary>누가 맞았다 — 한 번 읽으면 비운다.</summary>
		public HurtMessage[] TakeHurts()
		{
			if (hurts.Count == 0)
				return System.Array.Empty<HurtMessage>();

			HurtMessage[] taken = hurts.ToArray();
			hurts.Clear();
			return taken;
		}

		/// <summary>여기부터는 저 세계다 — 한 번 읽으면 비운다(같은 통행증으로 두 번 넘지 않게).</summary>
		public MoveOnMessage TakeMoveOn()
		{
			MoveOnMessage taken = moveOn;
			moveOn = null;
			return taken;
		}

		/// <summary>이 줄대로 만들겠다 — 되나 안 되나는 세계가 정한다.</summary>
		public void RequestCraft(int recipeId)
		{
			Send(JsonUtility.ToJson(new CraftMessage { recipeId = recipeId }));
		}

		/// <summary>세계가 돌려준 제작 결과 — 한 번 읽으면 비운다(두 번 표시되지 않게).</summary>
		public CraftedMessage TakeCraftResult()
		{
			CraftedMessage taken = crafted;
			crafted = null;
			return taken;
		}

		public void RequestChest(int cellX, int cellY, int cellZ)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_ASK, x = cellX, y = cellY, z = cellZ }));
		}

		public void RequestChestPut(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_PUT, x = cellX, y = cellY, z = cellZ, itemId = itemId, amount = amount }));
		}

		public void RequestChestTake(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ChestMessage { type = NetMessageType.CHEST_TAKE, x = cellX, y = cellY, z = cellZ, itemId = itemId, amount = amount }));
		}

		/// <summary>「내 가방 뭐 있냐」고 묻는다 — 다시 들어왔을 때 화면을 채우려면 물어야 한다.</summary>
		public void AskBag() => Send(JsonUtility.ToJson(new BagAskMessage()));

		/// <summary>「이걸 썼다」 — 정말 있었는지는 서버가 본다.</summary>
		public void RequestConsume(int itemId, int amount)
		{
			Send(JsonUtility.ToJson(new ConsumeMessage { itemId = itemId, amount = amount }));
		}
	}
}
