using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 빌드 식별 문자열. 「화면·로그·디스코드가 같은 글자를 말하는가」가 본체다.
    /// 여기가 갈라지면 폰에 깔린 빌드와 서버에 쌓인 로그를 이어붙일 방법이 사라진다.
    /// </summary>
    public class BuildInfoTest
    {
        private static BuildInfo Sample()
        {
            return new BuildInfo
            {
                commit = "a3f9c2148bd0e7f",
                branch = "main",
                buildNumber = 412,
                builtAtKst = "2026-08-06 03:12",
                channel = "dev",
                platform = "Android",
                unityVersion = "6000.5.6f1",
                runUrl = "https://github.com/x/y/actions/runs/1",
                dirty = false,
            };
        }

        [Test]
        public void 접힌_한_줄은_통로와_번호와_짧은_커밋이다()
        {
            Assert.AreEqual("dev #412 · a3f9c21", Sample().CollapsedLine());
        }

        [Test]
        public void 커밋_안_된_변경이_섞였으면_별표가_붙는다()
        {
            BuildInfo info = Sample();
            info.dirty = true;
            StringAssert.EndsWith("a3f9c21*", info.CollapsedLine());
        }

        [Test]
        public void 손으로_구운_빌드는_번호를_안_붙인다()
        {
            BuildInfo info = Sample();
            info.buildNumber = 0;
            Assert.AreEqual("dev · a3f9c21", info.CollapsedLine());
        }

        [Test]
        public void 커밋을_모르면_물음표로_말한다()
        {
            BuildInfo info = Sample();
            info.commit = string.Empty;
            Assert.AreEqual("?", info.ShortCommit);
            StringAssert.Contains("?", info.CollapsedLine());
        }

        [Test]
        public void 짧은_커밋은_원본이_짧아도_안_터진다()
        {
            BuildInfo info = Sample();
            info.commit = "abc";
            Assert.AreEqual("abc", info.ShortCommit);
        }

        [Test]
        public void 값이_없는_줄은_펼친_카드에_안_들어간다()
        {
            BuildInfo info = Sample();
            info.branch = string.Empty;
            info.builtAtKst = string.Empty;
            info.runUrl = string.Empty;

            List<KeyValuePair<string, string>> rows = info.DetailRows();
            foreach (KeyValuePair<string, string> row in rows)
            {
                Assert.IsFalse(string.IsNullOrEmpty(row.Value), $"빈 값이 카드에 들어갔다: {row.Key}");
            }
            CollectionAssert.DoesNotContain(KeysOf(rows), "가지");
            CollectionAssert.DoesNotContain(KeysOf(rows), "CI");
        }

        [Test]
        public void 더러운_빌드는_펼친_카드에서_경고한다()
        {
            BuildInfo info = Sample();
            info.dirty = true;
            CollectionAssert.Contains(KeysOf(info.DetailRows()), "주의");
        }

        [Test]
        public void 복사되는_덩어리는_카드에_보이는_줄_전부다()
        {
            BuildInfo info = Sample();
            string described = info.Describe();
            foreach (KeyValuePair<string, string> row in info.DetailRows())
            {
                StringAssert.Contains($"{row.Key}: {row.Value}", described);
            }
        }

        [Test]
        public void 로그에_실리는_라벨은_화면_접힌_줄을_품는다()
        {
            BuildInfo info = Sample();
            StringAssert.Contains(info.CollapsedLine(), info.ShortLabel());
        }

        [Test]
        public void 구운_정보를_JSON_으로_주고받아도_안_변한다()
        {
            BuildInfo original = Sample();
            string json = UnityEngine.JsonUtility.ToJson(original);
            BuildInfo parsed = BuildInfo.Parse(json);

            Assert.IsNotNull(parsed);
            Assert.AreEqual(original.CollapsedLine(), parsed.CollapsedLine());
            Assert.AreEqual(original.branch, parsed.branch);
            Assert.AreEqual(original.builtAtKst, parsed.builtAtKst);
            Assert.AreEqual(original.runUrl, parsed.runUrl);
        }

        [Test]
        public void 파일이_깨져_있으면_null_을_주고_호출부가_기본값으로_간다()
        {
            Assert.IsNull(BuildInfo.Parse(""));
            Assert.IsNull(BuildInfo.Parse(null));

            BuildInfo fallback = BuildInfo.Fallback();
            Assert.IsNotNull(fallback);
            Assert.IsFalse(string.IsNullOrEmpty(fallback.CollapsedLine()));
        }

        private static List<string> KeysOf(List<KeyValuePair<string, string>> rows)
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, string> row in rows)
            {
                keys.Add(row.Key);
            }
            return keys;
        }
    }
}
