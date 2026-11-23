using System;
using System.IO;

namespace KarmoLabs
{
	/// <summary> 세이브 주인 잠금을 잡으려 한 결과 </summary>
	public enum SaveOwnerLockResult
	{
		/// <summary> 잡음. 이 프로세스가 주인 </summary>
		Acquired = 0,
		/// <summary> 다른 프로세스 (또는 같은 프로세스의 다른 잠금) 가 쥐고 있음 </summary>
		HeldByOther = 1,
		/// <summary> 잠금 파일을 못 다룸 (권한, 읽기 전용 속성 등). 주인 여부를 알 수 없어 잠금 없이 예전처럼 동작 </summary>
		Unavailable = 2,
	}

	/// <summary>
	/// 세이브 파일 하나를 한 프로세스만 쓰게 하는 잠금
	/// 저장은 불러온 뒤의 메모리를 통째로 쓴다. 같은 세이브를 쓰는 프로세스가 둘이면 (에디터 플레이 모드와 플레이어, 플레이어 둘)
	/// 각자 옛 메모리로 덮어 마지막에 쓴 쪽이 이김. 2026-11-23 MOIS2 실측에서 에디터 플레이 모드가 플레이어 진행을 두 번 되돌림
	/// 잠금 파일을 독점으로 여는 동안만 주인. 프로세스가 죽으면 OS 가 핸들을 닫아 저절로 풀림
	/// 한계: FileShare.None 은 Windows 에서만 프로세스 사이에 강제됨. 다른 플랫폼은 늘 Acquired 라 예전 동작과 같음
	/// </summary>
	public sealed class SaveOwnerLock : IDisposable
	{
		public const string LOCK_SUFFIX = ".lock";
		// Win32 ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION. IOException.HResult 의 아래 16비트
		private const int ERROR_SHARING_VIOLATION = 32;
		private const int ERROR_LOCK_VIOLATION = 33;

		private FileStream stream;

		public string LockPath { get; }
		public bool IsHeld => stream != null;

		private SaveOwnerLock(string lockPath, FileStream stream)
		{
			LockPath = lockPath;
			this.stream = stream;
		}

		/// <summary> Acquired 일 때만 ownerLock 이 생김. 같은 프로세스 안에서도 두 번째는 HeldByOther </summary>
		public static SaveOwnerLockResult TryAcquire(string saveFilePath, out SaveOwnerLock ownerLock)
		{
			ownerLock = null;
			string lockPath = saveFilePath + LOCK_SUFFIX;
			try
			{
				string directory = Path.GetDirectoryName(lockPath);
				if (string.IsNullOrEmpty(directory) == false)
					Directory.CreateDirectory(directory);

				FileStream stream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				ownerLock = new SaveOwnerLock(lockPath, stream);
				return SaveOwnerLockResult.Acquired;
			}
			catch (IOException exception) when (IsSharingViolation(exception))
			{
				return SaveOwnerLockResult.HeldByOther;
			}
			catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
			{
				UnityEngine.Debug.LogWarning($"[{nameof(SaveOwnerLock)}] 잠금 파일을 못 다뤄 잠금 없이 진행: {lockPath} ({exception.GetType().Name}: {exception.Message})");
				return SaveOwnerLockResult.Unavailable;
			}
		}

		private static bool IsSharingViolation(IOException exception)
		{
			int code = exception.HResult & 0xFFFF;
			return code == ERROR_SHARING_VIOLATION || code == ERROR_LOCK_VIOLATION;
		}

		public void Dispose()
		{
			stream?.Dispose();
			stream = null;
		}
	}
}
