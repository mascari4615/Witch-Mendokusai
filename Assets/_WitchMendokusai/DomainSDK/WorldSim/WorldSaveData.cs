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

	/// <summary>
	/// 세계의 기억 (TASK-WM-217 단계 5).
	///
	/// ★ MMO 의 핵심은 「내가 없을 때도 세계가 있다」다. 서버가 꺼지면 지은 게 사라지는 세계는
	///   접속할 이유가 없다 — 그래서 저장은 부가 기능이 아니라 <b>세계의 정의</b>다.
	///
	/// <b>사람은 저장하지 않는다</b>: 지금 인형 번호는 접속마다 새로 주는 것이라 다음에 켰을 때
	/// 「누구의 가방」인지 이을 수 없다. 사람별 저장은 <b>신원(계정)이 먼저</b>다 — 그 전에 저장하면
	/// 남의 가방을 물려받는 사고가 난다.
	///
	/// 필드가 <c>public</c> 인 이유 = 서버(System.Text.Json)와 유니티(JsonUtility) 둘 다 이 모양만 읽는다.
	/// </summary>
	[Serializable]
	public class WorldSaveData
	{
		public BuildingSaveData[] buildings = Array.Empty<BuildingSaveData>();
	}
}
