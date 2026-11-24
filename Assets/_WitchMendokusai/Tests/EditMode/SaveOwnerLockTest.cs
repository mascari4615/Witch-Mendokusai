using System.IO;
using System.Text.RegularExpressions;
using KarmoLabs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	// 세이브 주인 잠금. 둘째 주인은 거절되고, 첫 주인이 놓으면 다시 잡힘. 임시 폴더에서 실제 파일로 확인
	public class SaveOwnerLockTest
	{
		private string directory;
		private string path;

		[SetUp]
		public void SetUp()
		{
			directory = Path.Combine(Application.temporaryCachePath, $"{nameof(SaveOwnerLockTest)}-{System.Guid.NewGuid():N}");
			Directory.CreateDirectory(directory);
			path = Path.Combine(directory, "save.json");
		}

		[TearDown]
		public void TearDown() => Directory.Delete(directory, true);

		private class Sample
		{
			public System.Collections.Generic.Dictionary<int, int> stats = new();
		}

		[Test]
		public void Non_owner_read_leaves_unreadable_file_in_place()
		{
			File.WriteAllText(path, "{ not json");
			LogAssert.Expect(LogType.Error, new Regex("읽기 실패"));

			Assert.IsFalse(SaveTool.TryReadWithBackup(path, out Sample _, out SaveReadSource source, quarantineUnreadable: false));
			Assert.AreEqual(SaveReadSource.Unreadable, source);
			// 주인의 파일이라 그대로 둠
			Assert.AreEqual("{ not json", File.ReadAllText(path));
			Assert.AreEqual(0, Directory.GetFiles(directory, "save.json" + SaveTool.CORRUPT_INFIX + "*").Length);
		}

		[Test]
		public void Clone_through_json_does_not_share_collections()
		{
			Sample original = new();
			original.stats[1] = 1000;

			Sample clone = SaveTool.CloneThroughJson(original);
			original.stats.Clear();

			// 원본을 비워도 사본은 남음. 읽기 전용 새 게임이 통계를 잃던 원인
			Assert.AreEqual(1000, clone.stats[1]);
		}

		[Test]
		public void Process_claim_is_remembered_and_blocks_others_until_released()
		{
			try
			{
				Assert.IsTrue(SaveOwnerLock.ClaimForProcess(path, out SaveOwnerLockResult first));
				Assert.AreEqual(SaveOwnerLockResult.Acquired, first);
				// 같은 프로세스가 다시 물으면 기억한 답. 잠금을 또 잡지 않음
				Assert.IsTrue(SaveOwnerLock.ClaimForProcess(path, out SaveOwnerLockResult _));
				// 다른 주인 (여기서는 같은 프로세스의 별도 잠금) 은 거절
				Assert.AreEqual(SaveOwnerLockResult.HeldByOther, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock _));
			}
			finally
			{
				SaveOwnerLock.ReleaseClaim(path);
			}

			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock again));
			again.Dispose();
		}

		[Test]
		public void Second_owner_is_refused_while_first_holds()
		{
			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock first));
			try
			{
				Assert.IsTrue(first.IsHeld);
				Assert.AreEqual(SaveOwnerLockResult.HeldByOther, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock second));
				Assert.IsNull(second);
			}
			finally
			{
				first.Dispose();
			}
		}

		[Test]
		public void Released_lock_can_be_taken_again()
		{
			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock first));
			first.Dispose();
			Assert.IsFalse(first.IsHeld);

			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock again));
			again.Dispose();
		}

		[Test]
		public void Lock_does_not_block_reading_or_writing_the_save()
		{
			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(path, out SaveOwnerLock ownerLock));
			try
			{
				// 잠금은 옆 파일이라 저장 쓰기와 앞 판 교체를 막지 않음
				SaveTool.WriteAtomic(path, "{\"value\":1}");
				SaveTool.WriteAtomic(path, "{\"value\":2}");
				Assert.AreEqual("{\"value\":2}", File.ReadAllText(path));
				Assert.IsTrue(File.Exists(path + SaveOwnerLock.LOCK_SUFFIX));
			}
			finally
			{
				ownerLock.Dispose();
			}
		}
	}
}
