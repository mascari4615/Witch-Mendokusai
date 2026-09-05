using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 틱의 판 <b>전부</b> (TASK-WM-411). 그리는 데 필요한 것이 아니라 <b>다시 굴리는 데</b> 필요한 것이다 —
	/// 쿨다운·남은 대시·탄 속도까지 들어 있어야 이 자리로 되감아 같은 미래를 만들 수 있다.
	///
	/// ★ 왜 있나 (근본): 온라인 대결에서 내 조작이 서버 왕복을 기다렸다 반응하면 그건 다른 게임이다.
	///   그래서 창은 <b>자기 판을 미리 굴린다</b>(예측). 서버가 「그 틱은 사실 이랬다」를 보내면
	///   그 스냅샷으로 <b>되감고</b>, 저장해 둔 내 입력을 다시 굴려 지금 틱까지 따라잡는다(롤백).
	///   그러려면 판이 ① 통째로 찍히고 ② 통째로 되돌려지고 ③ 같은 입력에 같은 답을 내야 한다.
	///
	/// 필드가 <c>public</c> 이고 배열인 이유 = 서버·유니티·웹이 같은 글자로 주고받기 위해서다.
	/// </summary>
	[Serializable]
	public class VersusRoundSnapshot
	{
		/// <summary>이 스냅샷이 몇 번째 틱의 것인가 — 되감을 자리를 이 번호로 찾는다.</summary>
		public int tick;

		public float elapsed;
		public bool isOver;
		public int winner = MatchConstants.NO_WINNER;

		public VersusFighterSnapshot[] fighters = new VersusFighterSnapshot[0];
		public VersusShotSnapshot[] shots = new VersusShotSnapshot[0];
	}

	/// <summary> 싸우는 한 명의 지금 상태 전부(그리는 값 + 굴리는 값). </summary>
	[Serializable]
	public class VersusFighterSnapshot
	{
		public float x;
		public float y;
		public float facingX;
		public float facingY;
		public float fireCooldown;
		public float dashCooldown;
		public int dashLeft;
		public int shieldLeft;
		public bool alive;
	}

	/// <summary> 탄 하나의 지금 상태 전부. 속도가 있어야 다음 틱을 만들 수 있다(위치만으론 못 굴린다). </summary>
	[Serializable]
	public class VersusShotSnapshot
	{
		public float x;
		public float y;
		public float velocityX;
		public float velocityY;
		public float lifeLeft;
		public float radius;
		public int owner;
		public int bouncesLeft;
	}
}
