using System.Globalization;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-201 — 「로그가 실제로 나가고 있나」를 한 줄로 말한다.
    ///
    /// 이 장치의 유일한 무음 지점을 막는 물건이다. 폰이 로그를 못 보내는 상황(토큰 불일치 401,
    /// 전파 끊김, 서버 down)에서 지금까지는 *아무 일도 안 일어난 것처럼* 보였다 — 로그가 안 오는
    /// 게 「조용한 실행」인지 「망가진 전송」인지 구분할 방법이 없었다.
    ///
    /// 그래서 화면 구석 표시기가 이 줄을 함께 보여준다. 「보냄 128줄」이 안 늘면 그 자리에서 안다.
    /// (`process.md` § no-news is bad-news — 자동화는 healthy log 를 전제한다.)
    ///
    /// 문자열 조립은 순수 함수라 EditMode 로 못 박는다.
    /// </summary>
    public static class DeviceLogStatus
    {
        public const string HTTP_UNAUTHORIZED_HINT = "토큰 불일치";

        /// <summary>릴레이가 아예 안 켜진 경우 — 설정이 꺼졌거나 에디터다.</summary>
        public static string OffLine()
        {
            return "로그 전송 꺼짐";
        }

        /// <summary>
        /// 켜진 릴레이의 상태 한 줄.
        /// </summary>
        /// <param name="sentLines">서버가 받았다고 답한 누적 줄 수.</param>
        /// <param name="pendingLines">아직 못 보낸(버퍼에 남은) 줄 수.</param>
        /// <param name="consecutiveFailures">연속 실패 횟수. 0 이면 건강.</param>
        /// <param name="lastResponseCode">마지막 실패 응답 코드 (0 = 응답 자체가 없었다).</param>
        public static string Line(int sentLines, int pendingLines, int consecutiveFailures, long lastResponseCode)
        {
            string sent = sentLines.ToString(CultureInfo.InvariantCulture);
            string pending = pendingLines > 0
                ? $" · 대기 {pendingLines.ToString(CultureInfo.InvariantCulture)}줄"
                : string.Empty;

            if (consecutiveFailures <= 0)
            {
                return $"보냄 {sent}줄{pending}";
            }

            string reason;
            if (lastResponseCode == 401 || lastResponseCode == 403)
            {
                reason = $"{lastResponseCode.ToString(CultureInfo.InvariantCulture)} {HTTP_UNAUTHORIZED_HINT}";
            }
            else if (lastResponseCode > 0)
            {
                reason = lastResponseCode.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                reason = "응답 없음";
            }

            return $"막힘 {reason} · 실패 {consecutiveFailures.ToString(CultureInfo.InvariantCulture)}회 · 보냄 {sent}줄{pending}";
        }
    }
}
