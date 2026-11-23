using System.IO;
using System.Text.RegularExpressions;
using KarmoLabs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	// 저장 파일 쓰기와 읽기의 실패 처리. 임시 폴더에서 실제 파일로 확인
	public class SaveToolTest
	{
		private class Sample
		{
			public int value;
		}

		private string directory;
		private string path;

		[SetUp]
		public void SetUp()
		{
			directory = Path.Combine(Application.temporaryCachePath, $"{nameof(SaveToolTest)}-{System.Guid.NewGuid():N}");
			Directory.CreateDirectory(directory);
			path = Path.Combine(directory, "save.json");
		}

		[TearDown]
		public void TearDown() => Directory.Delete(directory, true);

		[Test]
		public void First_write_leaves_only_the_file()
		{
			SaveTool.WriteAtomic(path, "{\"value\":1}");

			Assert.IsTrue(File.Exists(path));
			Assert.IsFalse(File.Exists(path + SaveTool.TEMP_SUFFIX));
			Assert.IsFalse(File.Exists(path + SaveTool.BACKUP_SUFFIX));
		}

		[Test]
		public void Second_write_keeps_previous_as_backup()
		{
			SaveTool.WriteAtomic(path, "{\"value\":1}");
			SaveTool.WriteAtomic(path, "{\"value\":2}");

			Assert.AreEqual("{\"value\":2}", File.ReadAllText(path));
			Assert.AreEqual("{\"value\":1}", File.ReadAllText(path + SaveTool.BACKUP_SUFFIX));
			Assert.IsFalse(File.Exists(path + SaveTool.TEMP_SUFFIX));
		}

		[Test]
		public void Missing_file_reads_as_absent()
		{
			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample sample));
			Assert.IsNull(sample);
		}

		[Test]
		public void Broken_file_falls_back_to_backup_and_is_set_aside()
		{
			SaveTool.WriteAtomic(path, "{\"value\":1}");
			SaveTool.WriteAtomic(path, "{\"value\":2}");
			File.WriteAllText(path, "{\"value\":");

			LogAssert.Expect(LogType.Error, new Regex("읽기 실패"));
			LogAssert.Expect(LogType.Warning, new Regex("옮김"));
			LogAssert.Expect(LogType.Warning, new Regex("앞 판"));
			Assert.IsTrue(SaveTool.TryReadWithBackup(path, out Sample sample));

			Assert.AreEqual(1, sample.value);
			Assert.IsFalse(File.Exists(path));
			Assert.AreEqual(1, Directory.GetFiles(directory, "save.json" + SaveTool.CORRUPT_INFIX + "*").Length);
		}

		[Test]
		public void Empty_file_counts_as_broken()
		{
			File.WriteAllText(path, string.Empty);

			LogAssert.Expect(LogType.Warning, new Regex("옮김"));
			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample sample));

			Assert.IsNull(sample);
			Assert.IsFalse(File.Exists(path));
		}

		[Test]
		public void Write_after_broken_file_starts_clean()
		{
			File.WriteAllText(path, "not json");
			LogAssert.Expect(LogType.Error, new Regex("읽기 실패"));
			LogAssert.Expect(LogType.Warning, new Regex("옮김"));
			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample _));

			SaveTool.WriteAtomic(path, "{\"value\":3}");

			Assert.IsTrue(SaveTool.TryReadWithBackup(path, out Sample sample));
			Assert.AreEqual(3, sample.value);
		}

		[Test]
		public void Source_tells_first_run_from_broken_save()
		{
			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample _, out SaveReadSource missing));
			Assert.AreEqual(SaveReadSource.Missing, missing);

			SaveTool.WriteAtomic(path, "{\"value\":1}");
			Assert.IsTrue(SaveTool.TryReadWithBackup(path, out Sample _, out SaveReadSource main));
			Assert.AreEqual(SaveReadSource.Main, main);

			SaveTool.WriteAtomic(path, "{\"value\":2}");
			File.WriteAllText(path, "{\"value\":");
			LogAssert.Expect(LogType.Error, new Regex("읽기 실패"));
			LogAssert.Expect(LogType.Warning, new Regex("옮김"));
			LogAssert.Expect(LogType.Warning, new Regex("앞 판"));
			Assert.IsTrue(SaveTool.TryReadWithBackup(path, out Sample _, out SaveReadSource backup));
			Assert.AreEqual(SaveReadSource.Backup, backup);
		}

		[Test]
		public void Source_is_unreadable_when_both_files_are_broken()
		{
			File.WriteAllText(path, "not json");
			File.WriteAllText(path + SaveTool.BACKUP_SUFFIX, string.Empty);
			LogAssert.Expect(LogType.Error, new Regex("읽기 실패"));
			LogAssert.Expect(LogType.Warning, new Regex("옮김"));
			LogAssert.Expect(LogType.Warning, new Regex("옮김"));

			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample _, out SaveReadSource source));
			Assert.AreEqual(SaveReadSource.Unreadable, source);
		}

		[Test]
		public void Old_save_without_version_reads_as_zero()
		{
			GameData gameData = Newtonsoft.Json.JsonConvert.DeserializeObject<GameData>("{\"curDollIndex\":2}");

			Assert.AreEqual(0, gameData.saveVersion);
			Assert.AreEqual(2, gameData.curDollIndex);
		}
	}
}
