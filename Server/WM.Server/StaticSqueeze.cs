using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 창을 이루는 파일들을 <b>한 번만 최고로 눌러</b> 들고 있는다 (TASK-WM-226).
	///
	/// ★ 왜: 요청마다 누르면 CPU 를 매번 쓰므로 어쩔 수 없이 <b>빠른 단계</b>로 누르게 된다.
	///   실측(2026-08-12): 그렇게 누른 Brotli 는 374KB 로, gzip(328KB)보다도 컸다 —
	///   최신 창일수록 br 을 고르니 <b>좋은 창이 더 나쁜 파일을 받는</b> 뒤집힌 일이 났다.
	///
	/// ★ 이 파일들은 서버가 도는 동안 <b>안 바뀐다</b>. 그러니 한 번 최고로 눌러 들고 있으면
	///   요청당 CPU 는 0 이 되고 압축률은 최고가 된다. 둘 다 얻는 자리다.
	///
	/// 누르기는 <b>뒤에서</b> 한다 — 세계가 뜨는 것을 몇 초 늦추면 안 된다. 아직 안 눌린 파일은
	/// 그냥 원본으로 나간다(늦게 눌려도 다음 사람부터 이득이다).
	/// </summary>
	public sealed class StaticSqueeze
	{
		/// <summary>이만큼보다 작으면 안 누른다 — 눌러 봐야 줄지 않고 헤더만 붙는다.</summary>
		private const int SMALLEST_WORTH_SQUEEZING = 1024;

		/// <summary>눌러 둔 것 하나 — 몸통과 <b>이름표</b>(같은 것인지 알아보는 표).</summary>
		public readonly struct Pressed
		{
			public Pressed(byte[] bytes, string tag, DateTimeOffset when)
			{
				Bytes = bytes;
				Tag = tag;
				When = when;
			}

			public byte[] Bytes { get; }

			/// <summary>이 몸통의 이름표(ETag) — 창이 「이거 그대로면 안 보내도 돼」 라고 물을 때 쓴다.</summary>
			public string Tag { get; }

			public DateTimeOffset When { get; }
		}

		private readonly ConcurrentDictionary<string, Pressed> pressed = new ConcurrentDictionary<string, Pressed>();
		private readonly string root;

		public StaticSqueeze(string webRoot)
		{
			root = webRoot;
		}

		/// <summary>지금까지 눌러 둔 파일 수 — /health 가 들여다보는 창구.</summary>
		public int Count => pressed.Count;

		/// <summary>글로 된 파일만 누른다 — 이미 눌린 것(png·woff2)은 눌러도 안 줄고 CPU 만 쓴다.</summary>
		public static bool WorthSqueezing(string path)
		{
			string extension = Path.GetExtension(path);
			return extension == ".html" || extension == ".js" || extension == ".mjs"
				|| extension == ".css" || extension == ".json" || extension == ".svg"
				|| extension == ".ts" || extension == ".map" || extension == ".txt";
		}

		/// <summary>창 하나가 부를 만한 파일을 미리 다 눌러 둔다(뒤에서 돈다).</summary>
		public void SqueezeAllInBackground()
		{
			if (Directory.Exists(root) == false)
				return;

			_ = Task.Run(() =>
			{
				foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
				{
					try
					{
						if (WorthSqueezing(file) == false)
							continue;

						byte[] raw = File.ReadAllBytes(file);
						if (raw.Length < SMALLEST_WORTH_SQUEEZING)
							continue;

						byte[] small = Squeeze(raw);
						DateTimeOffset when = File.GetLastWriteTimeUtc(file);
						pressed[KeyOf(file)] = new Pressed(small, TagOf(small, when), when);
					}
					catch (IOException)
					{
						// 못 읽은 파일은 그냥 원본으로 나간다 — 누르기는 덤이지 필수가 아니다.
					}
				}
			});
		}

		/// <summary>이 길에 대해 눌러 둔 것이 있으면 준다.</summary>
		public bool TryTake(string requestPath, out Pressed ready)
		{
			return pressed.TryGetValue(Normalize(requestPath), out ready);
		}

		/// <summary>
		/// 같은 몸통엔 같은 이름표 — 창이 두 번째 올 때 <b>안 받아도 되게</b> 한다 (TASK-WM-233).
		///
		/// ★ 왜 필요했나: 미리 눌러 보내기(WM-226)를 넣으면서 정적 파일 미들웨어를 비켜 갔고,
		///   그때 <b>이름표·마지막 손댄 시각도 같이 잃었다</b>. 그래서 다시 방문할 때마다 138KB 를
		///   또 받았다 — 누르기로 번 것을 다시 오는 사람에게서 도로 빼앗고 있었다.
		/// </summary>
		public static string TagOf(byte[] bytes, DateTimeOffset when)
		{
			return "\"" + bytes.Length.ToString("x") + "-" + when.ToUnixTimeSeconds().ToString("x") + "\"";
		}

		/// <summary>「/」 는 index.html 이다 — 창이 처음 여는 문이라 이걸 놓치면 제일 큰 파일이 샌다.</summary>
		public static string Normalize(string requestPath)
		{
			string path = requestPath ?? "/";
			if (path.EndsWith("/", StringComparison.Ordinal))
				path += "index.html";

			return path.ToLowerInvariant();
		}

		private string KeyOf(string file)
		{
			string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
			return ("/" + relative).ToLowerInvariant();
		}

		private static byte[] Squeeze(byte[] raw)
		{
			using MemoryStream held = new MemoryStream();
			using (BrotliStream press = new BrotliStream(held, CompressionLevel.SmallestSize, leaveOpen: true))
				press.Write(raw, 0, raw.Length);

			return held.ToArray();
		}

		/// <summary>이 요청이 br 을 받겠다고 했나.</summary>
		public static bool WantsBrotli(HttpRequest request)
		{
			string said = request.Headers.AcceptEncoding.ToString();
			return said != null && said.Contains("br", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>길 끝을 보고 무슨 글인지 말한다 — 안 붙이면 창이 js 를 글자로 읽는다.</summary>
		public static string KindOf(string path)
		{
			string extension = Path.GetExtension(Normalize(path));
			if (extension == ".html") return "text/html; charset=utf-8";
			if (extension == ".js" || extension == ".mjs") return "text/javascript; charset=utf-8";
			if (extension == ".css") return "text/css; charset=utf-8";
			if (extension == ".json") return "application/json; charset=utf-8";
			if (extension == ".svg") return "image/svg+xml";
			if (extension == ".ts") return "text/plain; charset=utf-8";
			return "application/octet-stream";
		}
	}
}
