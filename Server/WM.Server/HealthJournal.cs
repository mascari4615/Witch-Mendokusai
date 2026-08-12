using System;
using System.Collections.Generic;
using System.IO;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계가 <b>스스로 제 상태를 적어 두는 자리</b> (TASK-WM-297).
	///
	/// ★ 왜: 소크 시험(WM-296)은 3분짜리다. 그런데 진짜 물음은 「<b>며칠</b> 돌면 어떻게 되나」이고,
	///   그건 prod(노트북 24시간)에서만 답이 나온다. 그런데 지금은 그 답을 볼 <b>기록이 없다</b> —
	///   서버가 죽으면 그때까지의 상태도 같이 사라진다.
	///
	/// ★ 그래서 세계가 몇 분마다 한 줄씩 적는다. 밖에서 무엇을 띄울 필요가 없다 —
	///   세계 파일 옆에 나란히 남으므로, 나중에 열어 보기만 하면 된다.
	///
	/// ★ 무한히 자라면 그것도 새는 것이다 — <see cref="MOST_LINES"/> 줄만 남긴다(오래된 것부터 버린다).
	/// </summary>
	public sealed class HealthJournal
	{
		/// <summary>남겨 두는 줄 수 — 10분마다 한 줄이면 대략 한 달치다.</summary>
		public const int MOST_LINES = 5000;

		private readonly object gate = new object();
		private readonly string path;

		public HealthJournal(string worldFilePath)
		{
			path = string.IsNullOrEmpty(worldFilePath) ? null : worldFilePath + ".health.jsonl";
		}

		/// <summary>어디에 적히나 — 없으면 null(안 적는다).</summary>
		public string Path => path;

		/// <summary>한 줄 적는다. 못 적어도 세계는 돈다(적는 것이 세계를 멈추면 안 된다).</summary>
		public void Write(string line)
		{
			if (path == null || string.IsNullOrEmpty(line))
				return;

			lock (gate)
			{
				try
				{
					File.AppendAllText(path, line + Environment.NewLine);
					TrimIfLong();
				}
				catch (IOException)
				{
					// 디스크가 바쁘거나 잠겨 있다 — 다음 판에 다시 적는다.
				}
				catch (UnauthorizedAccessException)
				{
					// 못 쓰는 자리다 — 그래도 세계는 돈다.
				}
			}
		}

		// ⚠ 이미 자물쇠를 쥔 자리에서 부른다.
		private void TrimIfLong()
		{
			string[] lines = File.ReadAllLines(path);
			if (lines.Length <= MOST_LINES)
				return;

			List<string> kept = new List<string>(MOST_LINES);
			for (int i = lines.Length - MOST_LINES; i < lines.Length; i++)
				kept.Add(lines[i]);

			File.WriteAllLines(path, kept);
		}
	}
}
