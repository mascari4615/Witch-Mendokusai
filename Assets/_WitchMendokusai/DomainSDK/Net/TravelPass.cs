using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 옆 세계로 넘어갈 때 들고 가는 <b>통행증</b> (TASK-WM-253) — 순수 셈, 엔진 밖.
	///
	/// ★ 왜 필요한가: 세계를 나누면(WM-252) 사람은 한 세계에서 다른 세계로 <b>걸어서</b> 넘어간다.
	///   그때 신원과 가방이 같이 가야 한다. 그런데 그 둘을 <b>창이 들고 간다</b> —
	///   그러니 창이 그 안을 고칠 수 있으면 「걸어서 국경을 넘으면 가방이 가득 차는」 세계가 된다.
	///
	/// ★ 그래서 <b>도장</b>을 찍는다: 두 세계가 같은 비밀을 알고, 보낸 세계가 찍은 도장을
	///   받는 세계가 확인한다. 창은 도장을 못 만든다(비밀을 모르니까).
	///
	/// ★ <b>기한</b>도 넣는다 — 도장이 영원하면 오늘 받은 통행증으로 내일 또 들어온다(가방 복사).
	/// </summary>
	public static class TravelPass
	{
		/// <summary>통행증이 살아 있는 시간 (ms) — 걸어서 넘어가는 데 이보다 오래 걸릴 일은 없다.</summary>
		public const long GOOD_FOR_MS = 30000;

		/// <summary>들고 가는 것.</summary>
		public readonly struct Bundle
		{
			public Bundle(string mark, string name, float x, float z, IReadOnlyList<(int ItemId, int Amount)> bag, long madeAtMs, int health)
			{
				Mark = mark ?? string.Empty;
				Name = name ?? string.Empty;
				X = x;
				Z = z;
				Bag = bag ?? new List<(int, int)>();
				MadeAtMs = madeAtMs;
				Health = health;
			}

			/// <summary>
			/// 그 사람의 <b>세계 공통 이름표</b> (TASK-WM-259) — 계정이거나 열쇠의 지문이다.
			/// ⚠ 세계 안의 번호를 실으면 안 된다. 번호는 세계마다 따로 매겨서,
			///   저쪽에 이미 그 번호로 사는 <b>남</b>이 있다(그 사람이 돼 버린다).
			/// </summary>
			public string Mark { get; }

			/// <summary>불리는 이름 — 이 이름도 같이 가야 국경을 넘어도 <b>같은 사람</b>으로 보인다.</summary>
			public string Name { get; }

			public float X { get; }

			public float Z { get; }

			public IReadOnlyList<(int ItemId, int Amount)> Bag { get; }

			public long MadeAtMs { get; }

			/// <summary>
			/// 넘어갈 때의 몸 (TASK-WM-258).
			/// ⚠ 이걸 안 들고 가면 <b>국경이 회복 장소</b>가 된다 — 맞기 직전에 넘어갔다 오면 가득 찬다.
			/// </summary>
			public int Health { get; }
		}

		/// <summary>왜 안 받나.</summary>
		public enum Refusal
		{
			None = 0,

			/// <summary>모양이 아니다.</summary>
			Garbled,

			/// <summary>도장이 안 맞는다 — 창이 지어냈거나 고쳤다.</summary>
			BadSeal,

			/// <summary>기한이 지났다.</summary>
			TooOld,

			/// <summary>아직 오지 않은 시각이다(시계를 앞당겨 만든 것).</summary>
			FromTheFuture,
		}

		/// <summary>통행증을 만든다 — 「내용|도장」.</summary>
		public static string Write(Bundle bundle, string sharedSecret)
		{
			string body = Body(bundle);
			return body + "|" + Seal(body, sharedSecret);
		}

		/// <summary>통행증을 읽는다. 못 받으면 <paramref name="why"/> 에 이유가 담긴다.</summary>
		public static bool TryRead(string pass, string sharedSecret, long nowMs, out Bundle bundle, out Refusal why)
		{
			bundle = default;
			why = Refusal.Garbled;

			if (string.IsNullOrEmpty(pass))
				return false;

			int split = pass.LastIndexOf('|');
			if (split <= 0 || split == pass.Length - 1)
				return false;

			string body = pass.Substring(0, split);
			string seal = pass.Substring(split + 1);

			// ⚠ 도장부터 본다 — 안 맞는 통행증의 <b>내용</b>은 읽을 가치가 없다.
			if (SameSeal(seal, Seal(body, sharedSecret)) == false)
			{
				why = Refusal.BadSeal;
				return false;
			}

			string[] parts = body.Split(';');
			if (parts.Length < 6)
				return false;

			string mark = Unescape(parts[0]);
			string name = Unescape(parts[5]);
			if (mark.Length == 0
				|| TryReadFloat(parts[1], out float x) == false
				|| TryReadFloat(parts[2], out float z) == false
				|| long.TryParse(parts[3], out long madeAtMs) == false
				|| int.TryParse(parts[4], out int health) == false)
			{
				return false;
			}

			if (madeAtMs > nowMs + 1000)
			{
				why = Refusal.FromTheFuture;
				return false;
			}

			if (nowMs - madeAtMs > GOOD_FOR_MS)
			{
				why = Refusal.TooOld;
				return false;
			}

			List<(int ItemId, int Amount)> bag = new List<(int, int)>();
			for (int i = 6; i < parts.Length; i++)
			{
				if (parts[i].Length == 0)
					continue;

				string[] pair = parts[i].Split(',');
				if (pair.Length != 2 || int.TryParse(pair[0], out int itemId) == false
					|| int.TryParse(pair[1], out int amount) == false)
				{
					return false;
				}

				if (amount > 0)
					bag.Add((itemId, amount));
			}

			bundle = new Bundle(mark, name, x, z, bag, madeAtMs, health);
			why = Refusal.None;
			return true;
		}

		private static string Body(Bundle bundle)
		{
			StringBuilder builder = new StringBuilder();
			builder.Append(Escape(bundle.Mark)).Append(';')
				.Append(bundle.X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
				.Append(bundle.Z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(';')
				.Append(bundle.MadeAtMs).Append(';')
				.Append(bundle.Health).Append(';')
				.Append(Escape(bundle.Name));

			foreach ((int ItemId, int Amount) held in bundle.Bag)
				builder.Append(';').Append(held.ItemId).Append(',').Append(held.Amount);

			return builder.ToString();
		}

		/// <summary>칸 나누는 글자가 <b>내용 안에</b> 들어 있으면 칸이 어긋난다 — 사람 이름은 아무 글자나 된다.</summary>
		private static string Escape(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			return text.Replace("%", "%25").Replace(";", "%3b").Replace("|", "%7c");
		}

		private static string Unescape(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;

			return text.Replace("%7c", "|").Replace("%3b", ";").Replace("%25", "%");
		}

		private static string Seal(string body, string sharedSecret)
		{
			using HMACSHA256 stamp = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret ?? string.Empty));
			byte[] print = stamp.ComputeHash(Encoding.UTF8.GetBytes(body));

			// ⚠ Convert.ToHexString 은 netstandard2.1 에 없다 — 판정 층은 유니티도 같이 본다.
			StringBuilder hex = new StringBuilder(print.Length * 2);
			foreach (byte one in print)
				hex.Append(one.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));

			return hex.ToString();
		}

		/// <summary>도장 비교는 <b>글자 수만큼 끝까지</b> 본다 — 빨리 틀리면 도장을 맞춰 갈 수 있다.</summary>
		private static bool SameSeal(string said, string mine)
		{
			if (said == null || mine == null || said.Length != mine.Length)
				return false;

			int different = 0;
			for (int i = 0; i < said.Length; i++)
				different |= said[i] ^ mine[i];

			return different == 0;
		}

		private static bool TryReadFloat(string said, out float number)
		{
			return float.TryParse(said, System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out number);
		}
	}
}
