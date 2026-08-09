using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// KarmoLab 계정에게 <b>「이 사람 누구냐」</b>고 물어보는 자리 (TASK-WM-218).
	///
	/// ★ 왜 남의 서버에 묻나: KarmoLab 에는 이미 계정이 있다(디스코드 로그인·패스키·복구 코드·
	///   계정 잇기까지). 같은 걸 WM 이 또 만들면 사람은 계정을 두 개 갖게 되고, 둘은 반드시 갈라진다.
	///
	/// ★ 못 물어봐도 게임은 열린다(fail-open). 그 서버가 죽었거나 느리면 <b>손님으로</b> 논다 —
	///   계정은 「더 좋은 경우」이지 「필요 조건」이 아니다. 이 원칙은 KarmoLab 쪽 account.ts 와 같다.
	/// </summary>
	public sealed class KarmoLabAccounts
	{
		/// <summary>이 시간을 넘기면 없는 셈 친다 — 남의 서버 때문에 우리 세계가 멈추면 안 된다.</summary>
		private static readonly TimeSpan TIMEOUT = TimeSpan.FromSeconds(4);

		private readonly HttpClient http;

		public KarmoLabAccounts(string apiBase = null, HttpMessageHandler handler = null)
		{
			ApiBase = string.IsNullOrWhiteSpace(apiBase)
				? (Environment.GetEnvironmentVariable("WM_KARMOLAB_API") ?? "https://yawnbot.mascari4615.com")
				: apiBase;

			http = handler == null ? new HttpClient() : new HttpClient(handler);
			http.Timeout = TIMEOUT;
		}

		public string ApiBase { get; }

		/// <summary>
		/// 그 세션 쿠키가 누구인지 물어본다. 모르면 null(손님으로 논다).
		/// 돌려주는 값 = 신원 이름표 "karmolab:핸들".
		/// </summary>
		public async Task<string> TryResolveAsync(string sessionCookie)
		{
			if (string.IsNullOrWhiteSpace(sessionCookie))
				return null;

			try
			{
				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, ApiBase.TrimEnd('/') + "/kl/me");

				// 그쪽은 쿠키로 사람을 안다 — 우리는 창이 준 값을 그대로 전한다(우리가 보관하지 않는다).
				request.Headers.Add("Cookie", "kl_session=" + sessionCookie);

				using HttpResponseMessage response = await http.SendAsync(request);
				if (response.IsSuccessStatusCode == false)
					return null;

				string body = await response.Content.ReadAsStringAsync();
				using JsonDocument document = JsonDocument.Parse(body);

				if (document.RootElement.TryGetProperty("account", out JsonElement account) == false
					|| account.ValueKind != JsonValueKind.Object)
				{
					return null; // 로그인 안 한 사람 — 손님이다.
				}

				string handle = account.TryGetProperty("handle", out JsonElement handleElement) ? handleElement.GetString() : null;
				return string.IsNullOrWhiteSpace(handle) ? null : "karmolab:" + handle;
			}
			catch (Exception error) when (error is HttpRequestException || error is TaskCanceledException || error is JsonException)
			{
				// 남의 서버가 죽었다고 우리 세계가 안 열리면 안 된다.
				Console.WriteLine("[identity] KarmoLab 에 못 물어봤다 — 손님으로 받는다: " + error.Message);
				return null;
			}
		}
	}
}
