using System;
using System.IO;
using System.Text.RegularExpressions;
using KarmoLabs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	// 내 안의 세계 지우기. 앞 판까지 지워야 다음 읽기가 빈 세계, 남이 쓰는 세계는 지우면 안 됨. 임시 폴더에서 실제 파일로 확인
	public sealed class LocalWorldStoreTest
	{
		private const string PATH_VARIABLE = "WM_LOCAL_WORLD_FILE";

		private string directory;
		private string previousPath;

		[SetUp]
		public void SetUp()
		{
			directory = Path.Combine(Application.temporaryCachePath, $"{nameof(LocalWorldStoreTest)}-{Guid.NewGuid():N}");
			Directory.CreateDirectory(directory);
			previousPath = Environment.GetEnvironmentVariable(PATH_VARIABLE);
			Environment.SetEnvironmentVariable(PATH_VARIABLE, Path.Combine(directory, "world.json"));
		}

		[TearDown]
		public void TearDown()
		{
			SaveOwnerLock.ReleaseClaim(LocalWorldStore.Path);
			Environment.SetEnvironmentVariable(PATH_VARIABLE, previousPath);
			Directory.Delete(directory, true);
		}

		[Test]
		public void Deleted_world_does_not_come_back_from_backup()
		{
			LocalWorldStore.TrySave(new WorldSaveData { day = 3 });
			LocalWorldStore.TrySave(new WorldSaveData { day = 4 });
			Assert.IsTrue(File.Exists(LocalWorldStore.BackupPath));

			LocalWorldStore.Delete();

			Assert.IsNull(LocalWorldStore.TryLoad());
			Assert.IsFalse(File.Exists(LocalWorldStore.BackupPath));
		}

		[Test]
		public void Deleting_missing_world_is_quiet() => Assert.DoesNotThrow(LocalWorldStore.Delete);

		[Test]
		public void World_held_by_other_is_not_owned()
		{
			Assert.AreEqual(SaveOwnerLockResult.Acquired, SaveOwnerLock.TryAcquire(LocalWorldStore.Path, out SaveOwnerLock other));
			LogAssert.Expect(LogType.Error, new Regex("다른 WM"));

			Assert.IsFalse(LocalWorldStore.IsOwned);

			other.Dispose();
		}
	}
}
