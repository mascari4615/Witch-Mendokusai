using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 조작에서 온 시점 회전량 — 손가락으로 화면을 훑은 양이 마우스 이동량 자리에 들어온다 (TASK-WM-200).
	///
	/// ★ 왜 다리(bridge)인가: <see cref="JoystickBridge"/> 와 같은 이유다. 화면 조작 UI 는 Domain 층에
	///   있고 입력 관리자는 Core 층에 있다 — 아래층이 위층을 부를 수 없다. 그래서 위층이 값을 꽂는다.
	/// ★ 왜 raw 델타를 안 쓰나: 손가락 끌기는 *조이스틱을 만지는 손*과 같은 화면에서 일어난다.
	///   화면 어디를 끌든 시점이 돌면 조이스틱을 움직일 때마다 시점이 같이 돈다. 「어느 자리의 끌기가
	///   시점인가」는 화면(UI) 만 알 수 있으므로 판단도 거기서 하고, 여기엔 결론만 온다.
	/// </summary>
	public static class LookBridge
	{
		public static Func<Vector2> GetDelta = () => Vector2.zero;
	}
}
