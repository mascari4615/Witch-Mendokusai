using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 이 기기의 <b>열쇠</b>를 적어 두는 자리 (TASK-WM-218).
	///
	/// ★ 사람은 「로그인」을 몰라도 된다: 처음 붙을 때 세계가 열쇠를 주고, 여기 적힌 그 열쇠가
	///   다음부터 「나」다. 잃어버리면 새 사람이 된다 — 그래서 <b>남의 것을 물려받는 일은 없다</b>.
	///
	/// ⚠ 여러 기기를 한 사람으로 잇는 일(계정 연결)은 후속이다. 지금은 기기 하나 = 사람 하나.
	/// </summary>
	public static class WorldKeyStore
	{
		private const string KEY = "wm.world.secret";
		private const string ACCOUNT_CODE_KEY = "wm.world.klcode";

		/// <summary>적어 둔 열쇠. 없으면 빈 문자열(그때는 세계가 새로 준다).</summary>
		public static string Load() => PlayerPrefs.GetString(KEY, string.Empty);

		/// <summary>새로 받은 열쇠를 적어 둔다. 빈 값으로는 덮어쓰지 않는다(잃어버리면 새 사람이 된다).</summary>
		public static void Save(string secret)
		{
			if (string.IsNullOrEmpty(secret))
				return;

			PlayerPrefs.SetString(KEY, secret);
			PlayerPrefs.Save();
		}

		/// <summary>
		/// KarmoLab 연결 코드가 적혀 있으면 그것 (TASK-WM-218). 없으면 빈 문자열.
		///
		/// 사람이 KarmoLab 에서 받은 코드를 여기 넣으면, 다음 접속부터 <b>그 계정의 나</b>로 들어간다.
		/// ⚠ 아직 화면(입력창)이 없다 — 지금은 환경변수 <c>WM_KL_CODE</c> 나 이 저장 자리로만 넣는다.
		///   화면은 세계관 톤이 필요해 사용자 컨펌 뒤에 만든다.
		/// </summary>
		public static string LoadAccountCode()
		{
			string fromEnvironment = System.Environment.GetEnvironmentVariable("WM_KL_CODE");
			if (string.IsNullOrWhiteSpace(fromEnvironment) == false)
				return fromEnvironment;

			return PlayerPrefs.GetString(ACCOUNT_CODE_KEY, string.Empty);
		}

		/// <summary>연결 코드를 적어 둔다(한 번 쓰면 세계가 계정을 기억하므로 그 뒤엔 없어도 된다).</summary>
		public static void SaveAccountCode(string code)
		{
			if (string.IsNullOrWhiteSpace(code))
				return;

			PlayerPrefs.SetString(ACCOUNT_CODE_KEY, code);
			PlayerPrefs.Save();
		}

		/// <summary>열쇠를 버린다 — 다음 접속에 새 사람이 된다(시험·디버그용).</summary>
		public static void Forget()
		{
			PlayerPrefs.DeleteKey(KEY);
			PlayerPrefs.Save();
		}
	}
}
