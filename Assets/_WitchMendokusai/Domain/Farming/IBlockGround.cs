using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai
{
	// 밭이 땅에게 묻고 시키는 것 전부 (TASK-WM-410) — 「여기 무슨 블록이냐 / 이걸로 바꿔라 / 여기 세워라」.
	//
	// ★ 왜 인터페이스인가: 이게 없으면 밭의 규칙(갈고·심고·거두는 순서와 대가)을 확인하려면
	//   <b>복셀 청크가 실제로 떠 있어야</b> 한다. 그러면 규칙이 틀렸는지 세계가 안 떴는지 구분이 안 되고,
	//   그 상태로 초록을 받으면 「안 봤음」을 「이상 없음」으로 읽게 된다(WM-282 관문 규율).
	//   땅을 이 좁은 구멍으로 좁혀 두면 규칙만 따로 재현·검증할 수 있다.
	public interface IBlockGround
	{
		/// <summary>그 자리 블록의 영구 이름 — 없으면 null(허공·미로딩 청크).</summary>
		string BlockNameAt(FarmCoord coord);

		/// <summary>그 자리 블록을 이 이름의 블록으로 바꾼다.</summary>
		void SetBlock(FarmCoord coord, string identifier);

		/// <summary>그 자리에 보이는 몸(엔티티)을 세운다. 세울 수 없으면 false.</summary>
		bool SpawnEntity(FarmCoord coord, EntityData entity);
	}
}
