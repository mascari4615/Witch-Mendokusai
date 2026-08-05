using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 기기 스풀. 「앱이 죽어서 못 보낸 줄이 다음 실행에 되살아나는가」가 본체.
    /// 이게 없으면 정작 알고 싶은 *죽기 직전 줄*만 골라서 사라진다.
    /// </summary>
    public class DeviceLogSpoolTest
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "wm-device-log-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private static List<DeviceLogEntry> Lines(params string[] messages)
        {
            List<DeviceLogEntry> list = new List<DeviceLogEntry>();
            foreach (string message in messages)
            {
                list.Add(new DeviceLogEntry(1, DeviceLogEntry.LEVEL_LOG, message, string.Empty));
            }
            return list;
        }

        [Test]
        public void 확인_지점_뒤의_줄만_남는다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 1024 * 1024);
            spool.Append(Lines("보냈다1", "보냈다2"));
            spool.MarkSentThrough(spool.CurrentLength);
            spool.Append(Lines("못보냈다"));

            List<string> pending = DeviceLogSpool.ReadPendingLines(_directory, "s1");
            Assert.AreEqual(1, pending.Count);
            StringAssert.Contains("못보냈다", pending[0]);
        }

        [Test]
        public void 확인_지점이_없으면_전부_미전송이다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 1024 * 1024);
            spool.Append(Lines("a", "b", "c"));

            Assert.AreEqual(3, DeviceLogSpool.ReadPendingLines(_directory, "s1").Count);
        }

        [Test]
        public void 전부_확인됐으면_남는_줄이_없다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 1024 * 1024);
            spool.Append(Lines("a", "b"));
            spool.MarkSentThrough(spool.CurrentLength);

            CollectionAssert.IsEmpty(DeviceLogSpool.ReadPendingLines(_directory, "s1"));
        }

        [Test]
        public void 크래시로_잘린_마지막_줄은_버리고_앞줄은_살린다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 1024 * 1024);
            spool.Append(Lines("멀쩡한줄"));
            File.AppendAllText(spool.LogPath, "{\"t\":1,\"level\":\"log\",\"msg\":\"잘린");

            List<string> pending = DeviceLogSpool.ReadPendingLines(_directory, "s1");
            Assert.AreEqual(1, pending.Count);
            StringAssert.Contains("멀쩡한줄", pending[0]);
        }

        [Test]
        public void 상한을_넘으면_더_안_적는다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 64);
            Assert.IsTrue(spool.Append(Lines(new string('x', 200))));
            Assert.IsFalse(spool.Append(Lines("더")), "디스크를 채워 세이브까지 망가뜨리면 안 된다");
        }

        [Test]
        public void 지난_실행_세션을_현재_세션과_구분해_찾는다()
        {
            new DeviceLogSpool(_directory, "old-1", 1024).Append(Lines("a"));
            new DeviceLogSpool(_directory, "old-2", 1024).Append(Lines("b"));
            new DeviceLogSpool(_directory, "current", 1024).Append(Lines("c"));

            List<string> orphans = DeviceLogSpool.FindOrphanSessions(_directory, "current");
            CollectionAssert.AreEquivalent(new string[] { "old-1", "old-2" }, orphans);
        }

        [Test]
        public void 세션을_지우면_로그와_확인지점이_같이_사라진다()
        {
            DeviceLogSpool spool = new DeviceLogSpool(_directory, "s1", 1024);
            spool.Append(Lines("a"));
            spool.MarkSentThrough(1);

            DeviceLogSpool.DeleteSession(_directory, "s1");
            Assert.IsFalse(File.Exists(spool.LogPath));
            Assert.IsFalse(File.Exists(spool.OffsetPath));
        }

        [Test]
        public void 보관_세션_수를_넘으면_오래된_것부터_지운다()
        {
            for (int i = 0; i < 4; i++)
            {
                DeviceLogSpool spool = new DeviceLogSpool(_directory, $"old-{i}", 1024);
                spool.Append(Lines("x"));
                // 정렬이 수정시각 기준이므로 결정적으로 벌려둔다.
                File.SetLastWriteTimeUtc(spool.LogPath, new System.DateTime(2026, 1, 1 + i));
            }
            new DeviceLogSpool(_directory, "current", 1024).Append(Lines("c"));

            DeviceLogSpool.TrimOldSessions(_directory, "current", 2);

            List<string> remaining = DeviceLogSpool.FindOrphanSessions(_directory, "current");
            CollectionAssert.AreEquivalent(new string[] { "old-3", "old-2" }, remaining);
        }

        [Test]
        public void 없는_세션을_읽어도_터지지_않는다()
        {
            CollectionAssert.IsEmpty(DeviceLogSpool.ReadPendingLines(_directory, "없음"));
            CollectionAssert.IsEmpty(DeviceLogSpool.FindOrphanSessions(Path.Combine(_directory, "없는폴더"), "s"));
        }
    }
}
