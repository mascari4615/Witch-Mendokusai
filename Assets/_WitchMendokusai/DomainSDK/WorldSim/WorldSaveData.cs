using System;

namespace WitchMendokusai
{
	/// <summary>세계가 기억하는 건물 하나 — 껐다 켜도 남을 최소 (TASK-WM-217 단계 5).</summary>
	[Serializable]
	public class BuildingSaveData
	{
		public int x;
		public int y;
		public int z;
		public int w = 1;
		public int l = 1;
		public int buildingId;
	}

	/// <summary>가방 안 한 종류 — 몇 개 (TASK-WM-218).</summary>
	[Serializable]
	public class BagSaveEntry
	{
		public int itemId;
		public int amount;
	}

	/// <summary>
	/// 그 사람이 어디에 있었고 뭘 갖고 있었나 (TASK-WM-218).
	/// 인형 번호가 아니라 <b>신원 번호</b>에 붙는다 — 인형 번호는 접속마다 새로 준다.
	/// </summary>
	[Serializable]
	public class PersonSaveData
	{
		public int identityId;
		public float x;
		public float z;
		public BagSaveEntry[] bag = Array.Empty<BagSaveEntry>();
	}

	/// <summary>
	/// 세계의 기억 (TASK-WM-217 단계 5).
	///
	/// ★ MMO 의 핵심은 「내가 없을 때도 세계가 있다」다. 서버가 꺼지면 지은 게 사라지는 세계는
	///   접속할 이유가 없다 — 그래서 저장은 부가 기능이 아니라 <b>세계의 정의</b>다.
	///
	/// <b>사람도 저장한다 (TASK-WM-218)</b> — 신원이 생겨서다. 인형 번호가 아니라 <b>신원 번호</b>에
	/// 붙여 적는다(인형 번호는 접속마다 새로 주므로, 그걸로 적으면 남의 가방을 물려받는다).
	///
	/// 필드가 <c>public</c> 인 이유 = 서버(System.Text.Json)와 유니티(JsonUtility) 둘 다 이 모양만 읽는다.
	/// </summary>
	[Serializable]
	public class WorldSaveData
	{
		public BuildingSaveData[] buildings = Array.Empty<BuildingSaveData>();

		// 세계의 시각 — 껐다 켰더니 다시 아침이면 그건 이어진 세계가 아니다.
		public int year = 1;
		public int season;
		public int day = 1;
		public int hour = 6;
		public int minute;

		/// <summary>사람들 — 신원별 자리·가방 (TASK-WM-218).</summary>
		public PersonSaveData[] people = Array.Empty<PersonSaveData>();

		/// <summary>세계가 아는 사람들의 신원 장부 (TASK-WM-218).</summary>
		public Identity.WorldIdentityBook identities = new Identity.WorldIdentityBook();

		/// <summary>
		/// 뽑아 간 자리들 (TASK-WM-217) — 언제 다시 자라나.
		/// 자리 자체는 저장하지 않는다(계산으로 늘 같은 자리). 여기 담기는 건 「비어 있는 곳」뿐이다.
		/// </summary>
		public GatherTakenSaveEntry[] gathered = Array.Empty<GatherTakenSaveEntry>();

		/// <summary>세계에 놓인 상자들과 그 안에 든 것 — 넣어 둔 게 사라지면 아무도 안 쓴다.</summary>
		public StorageSaveEntry[] storages = Array.Empty<StorageSaveEntry>();
	}
}
