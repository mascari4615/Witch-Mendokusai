using System.IO;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 저장 파일이 <b>안 사라지나</b> (TASK-WM-406).
	///
	/// ★ 여기 시험이 없던 동안, 「임시 파일에 적고 바꿔치기 한다」고 <b>주석에 적어 놓고</b>
	///   실제로는 원본을 먼저 지우는 코드가 살아 있었다(2026-08-17 발견).
	///   그 둘 사이에 게임이 죽으면 몇 주치가 사라진다. 판정이 아니라 <b>사람의 판</b>을
	///   지키는 자리라, 글이 아니라 시험이 지켜야 한다.
	/// </summary>
	public sealed class IdleSaveFilesTests
	{
		private string folder;
		private string path;

		[SetUp]
		public void MakeRoom()
		{
			folder = Path.Combine(Path.GetTempPath(), "wm-idle-save-" + Path.GetRandomFileName());
			Directory.CreateDirectory(folder);
			path = Path.Combine(folder, "idle.json");
		}

		[TearDown]
		public void CleanUp()
		{
			if (Directory.Exists(folder))
			{
				Directory.Delete(folder, true);
			}
		}

		/// <summary>★ 적고 다시 읽으면 그대로 나온다.</summary>
		[Test]
		public void WhatYouWrote_ComesBack()
		{
			IdleSaveFiles.Write(path, "{\"stage\":42}");

			Assert.AreEqual(IdleSaveFiles.ReadOutcome.Fine,
				IdleSaveFiles.Read(path, Usable, out string got));
			Assert.AreEqual("{\"stage\":42}", got);
		}

		/// <summary>
		/// ★ 두 번째 저장부터는 <b>직전 판</b>이 남는다 — 되살릴 것이 생긴다.
		/// </summary>
		[Test]
		public void TheSecondSave_LeavesTheOneBefore()
		{
			IdleSaveFiles.Write(path, "{\"stage\":1}");
			IdleSaveFiles.Write(path, "{\"stage\":2}");

			Assert.IsTrue(File.Exists(IdleSaveFiles.BackupPathFor(path)), "직전 판이 안 남았다");
			Assert.AreEqual("{\"stage\":1}", File.ReadAllText(IdleSaveFiles.BackupPathFor(path)));
		}

		/// <summary>
		/// ★ 본 파일이 <b>부서져 있으면</b> 직전 판으로 되살린다 — 그리고 깨진 것은 옆으로 옮긴다.
		///
		/// 옮기지 않으면 몇 초 뒤 자동 저장이 덮어써서 <b>증거가 사라진다</b>.
		/// </summary>
		[Test]
		public void ABrokenSave_FallsBackAndIsKept()
		{
			IdleSaveFiles.Write(path, "{\"stage\":7}");
			IdleSaveFiles.Write(path, "{\"stage\":8}");

			// 적다가 죽은 꼴 — 반쯤 적힌 글자.
			File.WriteAllText(path, "{\"stage\":8");

			Assert.AreEqual(IdleSaveFiles.ReadOutcome.FellBackToBackup,
				IdleSaveFiles.Read(path, Usable, out string got));
			Assert.AreEqual("{\"stage\":7}", got, "직전 판이 아니라 다른 것을 줬다");

			Assert.IsTrue(File.Exists(IdleSaveFiles.BrokenPathFor(path)), "깨진 것을 안 남겼다");
			Assert.AreEqual("{\"stage\":8", File.ReadAllText(IdleSaveFiles.BrokenPathFor(path)));
		}

		/// <summary>★ 둘 다 못 쓰면 <b>잃었다</b>고 말한다 — 조용히 처음부터 돌지 않는다.</summary>
		[Test]
		public void WhenBothAreGone_ItSaysSo()
		{
			IdleSaveFiles.Write(path, "{\"stage\":3}");
			File.WriteAllText(path, "쓰레기");

			Assert.AreEqual(IdleSaveFiles.ReadOutcome.Lost,
				IdleSaveFiles.Read(path, Usable, out string _));
		}

		/// <summary>★ 아무것도 없으면 <b>없다</b>고 한다 — 처음 켠 사람이다.</summary>
		[Test]
		public void AnEmptyFolder_IsJustANewPlayer()
		{
			Assert.AreEqual(IdleSaveFiles.ReadOutcome.Nothing,
				IdleSaveFiles.Read(path, Usable, out string _));
		}

		/// <summary>
		/// ★ 적기가 <b>깨져도</b> 옛 판은 그대로다 — 임시 파일에만 흠집이 난다.
		///
		/// 「지우고 옮기기」였을 때 바로 이 자리에서 판이 통째로 없어졌다.
		/// </summary>
		[Test]
		public void AFailedWrite_LeavesTheOldSaveAlone()
		{
			IdleSaveFiles.Write(path, "{\"stage\":5}");

			// 임시 파일을 폴더로 만들어 둔다 — 다음 적기는 여기서 터진다.
			Directory.CreateDirectory(path + ".tmp");

			Assert.Catch(() => IdleSaveFiles.Write(path, "{\"stage\":6}"));

			Assert.AreEqual("{\"stage\":5}", File.ReadAllText(path),
				"적다가 터졌는데 옛 판이 사라지거나 상했다");
		}

		private static bool Usable(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			string trimmed = text.Trim();
			return trimmed.Length > 2 && trimmed[0] == '{' && trimmed[trimmed.Length - 1] == '}';
		}
	}
}
