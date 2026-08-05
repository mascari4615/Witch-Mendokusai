using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「저기로 가고 싶다」를 「지금 이 자리에선 이쪽으로」로 바꿔주는 길 안내(TASK-WM-194).
	///
	/// ★ 왜 인터페이스인가: 투기장은 빈 판이라 직선으로 걸으면 되고, 개척은 암반이 있어 돌아가야 한다.
	///   TacticDriver 안에 지형 지식을 박으면 투기장이 개척의 사정을 알게 된다. 안내자를 안 꽂으면
	///   기존 직선 이동 그대로(투기장 무변경), 꽂으면 돌아간다 — 행동 차이가 *주입*으로만 갈린다.
	/// </summary>
	public interface ITacticNavigator
	{
		/// <summary>
		/// from 에서 to 로 가려 할 때 실제로 밟아야 할 방향(정규화, y=0).
		/// 안내할 수 없으면(길이 없거나 판 밖) false — 호출자는 기존 직선 이동으로 되돌아간다.
		/// </summary>
		/// <param name="lane">
		/// 이 개체의 고유 값(0~1). 같은 거리의 길이 여럿일 때 *누가 어느 길을 밟을지*를 가른다.
		/// ★ 왜 밖에서 주나: 안내자가 위치로 뽑으면 걸을 때마다 값이 바뀌어 매 걸음 다른 길을 골라
		///   덜덜 떤다. 값은 개체가 태어날 때 정해져 끝까지 안 바뀌어야 한다.
		/// </param>
		bool TryGetSteering(Vector3 from, Vector3 to, float lane, out Vector3 direction);
	}
}
