using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 물약 선반의 물약이 다음 던전 한 판에 거는 것. 음식 (MealBuff) 과 겹치지 않는 축. 게임 쪽이 능력치 하나로 옮김
	public enum PotionEffect
	{
		Pickup = 0,
		Cooldown = 1,
		Damage = 2,
		Dodge = 3,
		Gold = 4,
		Revive = 5,
	}

	// 집 솥의 물약 하나. 솥 지도 (DomainSDK.Alchemy) 위 목표 칸 (X, Y, Radius) 에 닿으면 Output 이 나옴
	// - Fallback 은 어느 칸에도 못 닿았을 때 나오는 흐린 물약. 칸이 없고 하나뿐
	// - Effect 와 Value 는 선반에 올려 다음 던전 한 판에 마실 때
	public sealed class BrewDef : HomesteadDef
	{
		public string Output { get; set; } = string.Empty;
		public float X { get; set; }
		public float Y { get; set; }
		public float Radius { get; set; } = 0.5f;
		public bool Fallback { get; set; }
		public PotionEffect Effect { get; set; }
		public int Value { get; set; }
	}

	// 솥에 넣을 수 있는 창고 물건 하나 (Id 가 물건 id). 넣으면 지도 위 표식이 (X, Y) 만큼 움직임
	public sealed class BrewIngredientDef : HomesteadDef
	{
		public float X { get; set; }
		public float Y { get; set; }
	}

	// 솥 지도의 얼룩. 지나간 길이만큼 부작용이 쌓여 하나 더 받기 (명품) 를 놓침. 못 만들게 하지는 않음
	public sealed class BrewHazardDef : HomesteadDef
	{
		public float X { get; set; }
		public float Y { get; set; }
		public float Radius { get; set; }
		public float Severity { get; set; }
	}

	// 처방 노트 한 줄. 처음 닿은 길의 재료와 그때 받은 수. 더 좋은 길 (더 많이, 같으면 더 적은 재료) 을 찾으면 바뀜
	public sealed class BrewNote
	{
		public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
		public int Amount { get; set; } = 1;
	}
}
