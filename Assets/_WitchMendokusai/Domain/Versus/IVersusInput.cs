using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 싸우는 한 명을 움직이는 「손」 (TASK-WM-411). 사람 손(<see cref="VersusInputScheme"/>)과
	/// 봇 손(<see cref="VersusBotInput"/>)이 같은 구멍에 꽂힌다 — <b>친구가 없어도 판을 돌려 볼 수 있어야</b>
	/// v0 의 질문(「한 판 더가 나오나」)을 사람 둘이 모일 때까지 안 미룬다.
	/// </summary>
	public interface IVersusInput : IDisposable
	{
		/// <summary> 이동 입력(-1~1). 봇도 여기로만 말한다 — 감독은 누가 사람인지 모른다. </summary>
		Vector2 ReadMove();

		/// <summary> 꾹 눌러도 연사(간격은 스탯이 정한다). </summary>
		bool IsFireHeld { get; }

		/// <summary> 이번 프레임에 눌렸나 — 카드 확정에 쓴다. </summary>
		bool WasFirePressedThisFrame { get; }

		bool WasDashPressedThisFrame { get; }

		/// <summary> 한 프레임 생각한다. 사람 손은 할 일이 없고 봇만 여기서 판을 본다. </summary>
		void Tick(float deltaTime);
	}
}
