using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 건물 머리 위 작은 바(TASK-WM-194) — 「이건 지금 준비됐나」를 한 눈에.
	///
	/// ★ 왜 필요한가 (사용자 지시: "건물의 기능 동작 쿨타임 같은걸 건물마다 UI로 표시. 작은 파란색 바.
	///   공격 건물은 쿨타임이 다 차면 준비된 거고, 채집은 채집하는 거고, 패시브는 항상 채워져 있는 것"):
	///   지금 화면은 「무엇이 서 있나」만 말하고 「무엇이 일하고 있나」는 말하지 않는다. 전기가 끊겨 멈춘
	///   포탑과 방금 쏜 포탑이 똑같이 보인다.
	/// ★ 왜 이름표와 같은 층에 그리나: 둘 다 건물 머리 위에 붙는 것이라 좌표 계산이 같다.
	///   따로 만들면 한쪽이 카메라를 다르게 읽어 어긋난다.
	/// </summary>
	public static class TowerDefenseProgressBar
	{
		public const float WIDTH = 34f;
		public const float HEIGHT = 4f;

		public static VisualElement Create()
		{
			VisualElement track = new VisualElement();
			track.style.position = Position.Absolute;
			track.style.width = WIDTH;
			track.style.height = HEIGHT;
			track.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f, 0.75f);
			track.pickingMode = PickingMode.Ignore;

			VisualElement fill = new VisualElement { name = "Fill" };
			fill.style.height = HEIGHT;
			fill.style.width = Length.Percent(0f);
			fill.style.backgroundColor = ReadyColor;
			fill.pickingMode = PickingMode.Ignore;
			track.Add(fill);

			return track;
		}

		/// <summary> 채운 비율(0~1)과 상태색. 멈춘 건물은 파랑이 아니라 회색이다 — 차 있는데 안 도는 것은 거짓말. </summary>
		public static void SetRatio(VisualElement track, float ratio, bool working)
		{
			VisualElement fill = track.Q("Fill");
			if (fill == null)
				return;

			fill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
			fill.style.backgroundColor = working ? ReadyColor : StalledColor;
		}

		private static readonly Color ReadyColor = new Color(0.42f, 0.72f, 1f, 0.95f);
		private static readonly Color StalledColor = new Color(0.45f, 0.47f, 0.52f, 0.8f);
	}
}
