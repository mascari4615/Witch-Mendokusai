using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「이 몸은 지금 저 서식지 소속이다」를 몸 자체에 붙여 두는 표 (TASK-WM-194).
	///
	/// ★ 왜 목록만으로는 안 되는가: 마수가 죽으면 그 몸은 풀로 돌아가 *다른 곳에서 다른 마수로*
	///   되살아난다. 서식지가 목록으로만 식구를 들고 있으면, 되살아난 그 몸이 테두리에서 막
	///   나온 파도 마수인데도 서식지가 집으로 끌어당긴다 — 실측에서 「집에서 95~123 (목줄 20)」이
	///   그것이었다. 「죽었으면 뺀다」로는 못 막는다: 죽고 *되살아난 뒤에* 검사하면 그 몸은
	///   멀쩡히 살아 있어서 계속 식구로 보인다.
	///
	/// ★ 그래서 소속을 몸에 붙이고, **풀로 돌려주는 그 한 곳에서** 뗀다. 반납 지점은 하나뿐이라
	///   빠뜨릴 자리가 없다 — 목록을 여기저기서 지우려 들면 반드시 한 곳을 빠뜨린다.
	/// </summary>
	public sealed class TowerDefenseLairMember : MonoBehaviour
	{
		/// <summary> 소속 서식지 번호. 음수 = 소속 없음(풀로 돌아간 뒤). </summary>
		public int LairId { get; private set; } = -1;

		public void Join(int lairId)
		{
			LairId = lairId;
		}

		/// <summary> 풀로 돌아갈 때 — 다음 마수는 남의 집에 매이면 안 된다. </summary>
		public void Leave()
		{
			LairId = -1;
		}
	}
}
