using Microsoft.AspNetCore.Http;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// <b>이 창이 어디서 왔나</b> — 한 곳에서 너무 많이 붙는 것을 세려면 먼저 「곳」을 알아야 한다
	/// (TASK-WM-220).
	///
	/// ⚠ 세계는 터널(cloudflared) 뒤에 산다. 그러면 소켓이 말하는 주소는 <b>전부 127.0.0.1</b> 이라
	///   그것만 보고 세면 모든 사람이 한 사람으로 뭉친다 — 상한을 걸면 세계가 통째로 닫힌다.
	///   터널이 붙여 주는 이름표(CF-Connecting-IP · X-Forwarded-For)를 먼저 본다.
	///
	/// ⚠ 그 이름표는 <b>창이 지어낼 수도</b> 있다. 우리 세계는 터널 뒤에서만 열리므로 그건 터널이
	///   덮어쓴다. 터널 없이 직접 열어 두는 판이라면 이 자리는 믿을 게 못 된다.
	/// </summary>
	public static class ClientOrigin
	{
		/// <summary>그 창이 온 곳 — 없으면 "?"(그때는 다 같은 곳으로 센다).</summary>
		public static string Of(HttpContext context)
		{
			if (context == null)
				return "?";

			string tunnel = First(context.Request.Headers["CF-Connecting-IP"]);
			if (string.IsNullOrEmpty(tunnel) == false)
				return tunnel;

			string forwarded = First(context.Request.Headers["X-Forwarded-For"]);
			if (string.IsNullOrEmpty(forwarded) == false)
				return forwarded;

			return context.Connection.RemoteIpAddress?.ToString() ?? "?";
		}

		/// <summary>
		/// 같은 기계에서 온 창인가 — 그렇다면 <b>세지 않는다</b> (TASK-WM-220).
		///
		/// ★ 왜 빼나: 같은 기계 = 세계를 돌리는 사람 자신이다(혼자 놀기·시험·부하 재기).
		///   거기에 상한을 걸면 창 200개로 재는 관문이 통째로 막힌다 — 실제로 막혔다.
		///   막고 싶은 것은 <b>바깥에서</b> 한 곳이 무한히 두드리는 일이다.
		/// </summary>
		public static bool IsSameMachine(string origin)
		{
			return origin == "127.0.0.1" || origin == "::1" || origin == "localhost";
		}

		/// <summary>「a, b, c」로 이어 붙는 이름표에서 <b>맨 앞</b>(진짜 창) 하나만.</summary>
		public static string First(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return string.Empty;

			int comma = value.IndexOf(',');
			string head = comma < 0 ? value : value.Substring(0, comma);
			return head.Trim();
		}
	}
}
