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
		private const string SERVER_URL_KEY = "wm.world.server";

		/// <summary>이번 접속에서 세계가 새로 준 열쇠 — 아직 없으면 빈 문자열(스모크가 물려줄 때 쓴다).</summary>
		public static string LastGranted { get; private set; } = string.Empty;

		private const string ACCOUNT_CODE_KEY = "wm.world.klcode";

		/// <summary>
		/// 적어 둔 열쇠. 없으면 빈 문자열(그때는 세계가 새로 준다).
		///
		/// ★ 환경변수 <c>WM_WORLD_SECRET</c> 가 있으면 그것이 이긴다 (TASK-WM-217).
		///   왜: 한 기기에서 판을 둘 띄우면 <b>둘이 같은 사람</b>이 되어 서로를 밀어낸다(중복 로그인 규칙).
		///   그러면 「둘이 같이 논다」를 한 기기에서 시험할 수 없다 — 실측으로 스모크 한쪽이 그렇게 죽었다.
		///   사람이 쓰는 길이 아니라 <b>시험·여러 계정</b>을 위한 옆문이다.
		/// </summary>
		public static string Load()
		{
			string fromEnvironment = System.Environment.GetEnvironmentVariable("WM_WORLD_SECRET");
			if (string.IsNullOrEmpty(fromEnvironment) == false)
				return fromEnvironment;

			return PlayerPrefs.GetString(KEY, string.Empty);
		}

		/// <summary>새로 받은 열쇠를 적어 둔다. 빈 값으로는 덮어쓰지 않는다(잃어버리면 새 사람이 된다).</summary>
		public static void Save(string secret)
		{
			if (string.IsNullOrEmpty(secret))
				return;

			// 세계가 준 열쇠는 <b>기억은 해 둔다</b> — 옆문으로 들어온 판이 「다음에 또 나」로 들어오려면 필요하다.
			LastGranted = secret;

			// 옆문(환경변수)으로 들어온 판은 기기의 열쇠를 덮어쓰지 않는다 — 시험이 사람의 것을 갈아치우면 안 된다.
			if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("WM_WORLD_SECRET")) == false)
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

		/// <summary>
		/// 이 기기가 붙을 세계 주소. 환경변수 <c>WM_WORLD_SERVER</c> 가 있으면 그것이 이긴다.
		///
		/// ★ 왜 따로 적어 두나 (TASK-WM-219): 공개 배포 뒤에도 판마다 씬/프리팹 값을 다시 굽는 구조면
		///   운영이 너무 무겁다. 기본값은 빌드가 들고 가되, 기기에서 한 번 바꾼 주소는 남아야
		///   로컬·스테이징·공개 세계를 같은 플레이어로 오갈 수 있다.
		/// </summary>
		public static string LoadServerUrl(string fallback)
		{
			string fromEnvironment = System.Environment.GetEnvironmentVariable("WM_WORLD_SERVER");
			if (string.IsNullOrWhiteSpace(fromEnvironment) == false)
				return fromEnvironment;

			string saved = PlayerPrefs.GetString(SERVER_URL_KEY, string.Empty);
			return string.IsNullOrWhiteSpace(saved) ? fallback : saved;
		}

		/// <summary>
		/// 붙을 세계 주소를 적어 둔다. 환경변수로 강제된 판은 기기 값을 덮어쓰지 않는다.
		/// </summary>
		public static void SaveServerUrl(string url)
		{
			if (string.IsNullOrWhiteSpace(url))
				return;

			if (string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("WM_WORLD_SERVER")) == false)
				return;

			PlayerPrefs.SetString(SERVER_URL_KEY, url);
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
