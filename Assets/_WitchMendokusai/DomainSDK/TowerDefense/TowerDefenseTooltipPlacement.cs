using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>
	/// 커서를 따라다니는 설명 상자를 *어디에 놓을지* 정하는 계산 (TASK-WM-194).
	///
	/// ★ 왜 따로 꺼냈나: 툴팁은 커서가 있어야 뜨는데 확인 도구에는 커서가 없다. 그래서 이 배치는
	///   「사람이 마우스를 끝까지 끌어봐야 아는 것」으로 영영 남아 있었다. 정작 물어야 할 것
	///   — *가장자리에서 화면 밖으로 새지 않는가* — 는 순수한 계산이라, 꺼내 놓으면 커서 없이 증명된다.
	/// ★ 상자 크기를 인자로 받는다 — 예전엔 240/120 이 계산 안에 박혀 있어서, 상자 모양을 바꾸면
	///   뒤집는 지점이 조용히 어긋났다(수치 노출 룰 정합).
	///
	/// 좌표계: 화면 좌표(왼쪽 아래 원점)를 받아 UI 좌표(왼쪽 *위* 원점)를 돌려준다.
	/// </summary>
	public static class TowerDefenseTooltipPlacement
	{
		/// <summary>
		/// 커서 옆에 놓되, 오른쪽·아래로 넘칠 것 같으면 반대쪽으로 뒤집는다.
		/// 뒤집어도 넘치면(상자가 화면보다 클 때) 화면 안으로 밀어 넣는다 — 잘려 보이는 것보다 낫다.
		/// </summary>
		public static Vector2 Resolve(Vector2 screenPosition, Vector2 screenSize, Vector2 tooltipSize, float offset)
		{
			float left = screenPosition.x + offset;
			float top = screenSize.y - screenPosition.y + offset;

			if (left + tooltipSize.x > screenSize.x)
				left = screenPosition.x - tooltipSize.x - offset;
			if (top + tooltipSize.y > screenSize.y)
				top = screenSize.y - screenPosition.y - tooltipSize.y - offset;

			left = Mathf.Clamp(left, 0f, Mathf.Max(0f, screenSize.x - tooltipSize.x));
			top = Mathf.Clamp(top, 0f, Mathf.Max(0f, screenSize.y - tooltipSize.y));
			return new Vector2(left, top);
		}
	}
}
