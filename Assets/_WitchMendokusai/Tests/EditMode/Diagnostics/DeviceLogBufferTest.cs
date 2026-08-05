using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 기기 로그 버퍼·직렬화. 「넘칠 때 무엇을 지키는가」가 본체다.
    /// 여기가 틀리면 정작 원인인 예외가 잡음에 밀려 사라지고, 우리는 그걸 알 방법이 없다.
    /// </summary>
    public class DeviceLogBufferTest
    {
        private static DeviceLogEntry Info(string message)
        {
            return new DeviceLogEntry(1000, DeviceLogEntry.LEVEL_LOG, message, string.Empty);
        }

        private static DeviceLogEntry Error(string message)
        {
            return new DeviceLogEntry(2000, DeviceLogEntry.LEVEL_EXCEPTION, message, "at Foo()");
        }

        [Test]
        public void 버퍼가_넘치면_비에러_줄부터_버린다()
        {
            DeviceLogBuffer buffer = new DeviceLogBuffer(3);
            buffer.Add(Info("a"));
            buffer.Add(Error("터짐"));
            buffer.Add(Info("b"));
            buffer.Add(Info("c"));

            List<DeviceLogEntry> taken = new List<DeviceLogEntry>();
            Assert.IsTrue(buffer.TryTakeBatch(10, taken));
            CollectionAssert.AreEqual(
                new string[] { "터짐", "b", "c" },
                taken.ConvertAll(entry => entry.Message));
            Assert.AreEqual(1, buffer.DroppedTotal);
        }

        [Test]
        public void 전부_에러면_그때는_가장_오래된_것을_버린다()
        {
            DeviceLogBuffer buffer = new DeviceLogBuffer(2);
            buffer.Add(Error("첫번째"));
            buffer.Add(Error("두번째"));
            buffer.Add(Error("세번째"));

            List<DeviceLogEntry> taken = new List<DeviceLogEntry>();
            buffer.TryTakeBatch(10, taken);
            CollectionAssert.AreEqual(
                new string[] { "두번째", "세번째" },
                taken.ConvertAll(entry => entry.Message));
        }

        [Test]
        public void 배치는_들어온_순서대로_최대_줄수만큼_나온다()
        {
            DeviceLogBuffer buffer = new DeviceLogBuffer(100);
            for (int i = 0; i < 10; i++)
            {
                buffer.Add(Info($"m{i}"));
            }

            List<DeviceLogEntry> taken = new List<DeviceLogEntry>();
            buffer.TryTakeBatch(4, taken);
            CollectionAssert.AreEqual(
                new string[] { "m0", "m1", "m2", "m3" },
                taken.ConvertAll(entry => entry.Message));
            Assert.AreEqual(6, buffer.Count);
        }

        [Test]
        public void 빈_버퍼는_배치를_안_준다()
        {
            DeviceLogBuffer buffer = new DeviceLogBuffer(10);
            List<DeviceLogEntry> taken = new List<DeviceLogEntry>();
            Assert.IsFalse(buffer.TryTakeBatch(5, taken));
            Assert.AreEqual(0, taken.Count);
        }

        [Test]
        public void 전송_실패한_배치는_앞자리로_돌아간다()
        {
            DeviceLogBuffer buffer = new DeviceLogBuffer(10);
            buffer.Add(Info("나중"));

            List<DeviceLogEntry> failed = new List<DeviceLogEntry>
            {
                Info("먼저1"),
                Info("먼저2"),
            };
            buffer.PushFront(failed);

            List<DeviceLogEntry> taken = new List<DeviceLogEntry>();
            buffer.TryTakeBatch(10, taken);
            CollectionAssert.AreEqual(
                new string[] { "먼저1", "먼저2", "나중" },
                taken.ConvertAll(entry => entry.Message));
        }

        [Test]
        public void 줄바꿈_따옴표_역슬래시가_JSON_을_안_깬다()
        {
            DeviceLogEntry entry = new DeviceLogEntry(
                7, DeviceLogEntry.LEVEL_ERROR, "그는 \"안녕\"\n이라 했다\\끝", "at A()\nat B()");
            string json = DeviceLogBuffer.BuildEntryJson(entry);

            StringAssert.Contains("\\\"안녕\\\"", json);
            StringAssert.Contains("\\n", json);
            StringAssert.Contains("\\\\끝", json);
            Assert.IsFalse(json.Contains("\n"), "실제 줄바꿈이 JSON 안에 남으면 NDJSON 한 줄 규약이 깨진다");
        }

        [Test]
        public void 제어문자는_유니코드_이스케이프로_나간다()
        {
            string withControlChar = "A" + (char)0x01 + "B";
            DeviceLogEntry entry = new DeviceLogEntry(1, DeviceLogEntry.LEVEL_LOG, withControlChar, string.Empty);
            StringAssert.Contains("\\u0001", DeviceLogBuffer.BuildEntryJson(entry));
        }

        [Test]
        public void 스택이_없으면_stack_필드를_안_넣는다()
        {
            string json = DeviceLogBuffer.BuildEntryJson(Info("깨끗"));
            Assert.IsFalse(json.Contains("stack"));
        }

        [Test]
        public void 페이로드에_세션과_기기와_줄이_담긴다()
        {
            List<DeviceLogEntry> lines = new List<DeviceLogEntry> { Info("하나"), Error("둘") };
            string json = DeviceLogBuffer.BuildPayloadJson(
                "android-20260805-120000", "Pixel 7", "Android", "0.1.0", "WM 0.1.0", lines);

            StringAssert.Contains("\"session\":\"android-20260805-120000\"", json);
            StringAssert.Contains("\"device\":\"Pixel 7\"", json);
            StringAssert.Contains("\"platform\":\"Android\"", json);
            StringAssert.Contains("\"하나\"", json);
            StringAssert.Contains("\"exception\"", json);
            Assert.IsTrue(json.StartsWith("{") && json.EndsWith("}"));
        }

        [Test]
        public void 스풀에서_읽은_원문_줄은_다시_직렬화하지_않고_그대로_실린다()
        {
            List<string> raw = new List<string>
            {
                "{\"t\":1,\"level\":\"error\",\"msg\":\"보존\"}",
                "{\"t\":2,\"level\":\"log\",\"msg\":\"둘\"}",
            };
            string json = DeviceLogBuffer.BuildPayloadJsonFromRawLines("s1", "d", "Android", "v", "b", raw);

            StringAssert.Contains("\"lines\":[{\"t\":1,\"level\":\"error\",\"msg\":\"보존\"},{\"t\":2", json);
        }

        [Test]
        public void 에러급_판정은_error_exception_assert_만()
        {
            Assert.IsTrue(new DeviceLogEntry(0, DeviceLogEntry.LEVEL_ERROR, "m", "").IsError);
            Assert.IsTrue(new DeviceLogEntry(0, DeviceLogEntry.LEVEL_EXCEPTION, "m", "").IsError);
            Assert.IsTrue(new DeviceLogEntry(0, DeviceLogEntry.LEVEL_ASSERT, "m", "").IsError);
            Assert.IsFalse(new DeviceLogEntry(0, DeviceLogEntry.LEVEL_WARNING, "m", "").IsError);
            Assert.IsFalse(new DeviceLogEntry(0, DeviceLogEntry.LEVEL_LOG, "m", "").IsError);
        }
    }
}
