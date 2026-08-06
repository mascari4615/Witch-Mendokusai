using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-201 — 진단 장치가 *조용히* 죽는 유일한 길 = 에셋 실종.
    ///
    /// 설정 에셋이 없으면 릴레이도 표시기도 「아무 일 없다는 듯」 설치를 건너뛴다(의도된 안전장치).
    /// 그 말은, 누가 에셋을 지우거나 이름을 바꾸면 폰에서 로그도 빌드 표시도 영영 안 나오는데
    /// 아무 에러도 안 난다는 뜻이다. 그 무음을 여기서 잡는다.
    /// </summary>
    public class DiagnosticsResourcesTest
    {
        [Test]
        public void 로그_릴레이_설정이_리소스에_있다()
        {
            DeviceLogSettings settings = Resources.Load<DeviceLogSettings>(nameof(DeviceLogSettings));
            Assert.IsNotNull(settings, "Resources/DeviceLogSettings 없음 — 폰 로그가 조용히 안 올라온다");
            Assert.IsFalse(string.IsNullOrEmpty(settings.Endpoint), "보낼 주소가 비었다");
            Assert.Greater(settings.BufferCapacity, 0);
            Assert.Greater(settings.MaxLinesPerBatch, 0);
            Assert.Greater(settings.FlushIntervalSeconds, 0f);
        }

        [Test]
        public void 빌드_표시기_설정과_패널이_리소스에_있다()
        {
            BuildStampSettings settings = Resources.Load<BuildStampSettings>(nameof(BuildStampSettings));
            Assert.IsNotNull(settings, "Resources/BuildStampSettings 없음 — 화면 구석 빌드 표시가 조용히 사라진다");

            PanelSettings panel = Resources.Load<PanelSettings>("BuildStampPanelSettings");
            Assert.IsNotNull(panel, "Resources/BuildStampPanelSettings 없음 — 표시기가 그려질 판이 없다");
            Assert.Greater(settings.SortingOrder, 0f, "게임 UI 위에 오려면 정렬 순서가 커야 한다");
        }

        [Test]
        public void 빌드_정보가_없어도_표시할_글자는_나온다()
        {
            // 에디터엔 구운 정보가 없다 — 그 상태에서도 빈 줄이 아니라 말이 되는 글자여야 한다.
            Assert.IsFalse(string.IsNullOrEmpty(BuildInfo.Current.CollapsedLine()));
            Assert.IsFalse(string.IsNullOrEmpty(BuildInfo.Current.ShortLabel()));
            Assert.Greater(BuildInfo.Current.DetailRows().Count, 0);
        }
    }
}
