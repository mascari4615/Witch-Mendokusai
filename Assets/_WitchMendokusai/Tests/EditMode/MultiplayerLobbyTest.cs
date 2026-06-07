using NUnit.Framework;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-190 — 멀티 진입 UX 회귀 락. 초대코드 codec round-trip + 로비 로직(host/join)
    /// 을 fake 세션으로 검증 (NetCode/UI 무의존 = boundary 준수 seam + MonoBehaviour decouple).
    /// </summary>
    public sealed class MultiplayerLobbyTest
    {
        [Test]
        public void InviteCode_RoundTrips_Loopback()
        {
            string code = InviteCode.Encode("127.0.0.1", 7770);
            Assert.That(InviteCode.TryDecode(code, out string ip, out ushort port), Is.True, code);
            Assert.That(ip, Is.EqualTo("127.0.0.1"));
            Assert.That(port, Is.EqualTo((ushort)7770));
        }

        [Test]
        public void InviteCode_RoundTrips_LanAddress()
        {
            string code = InviteCode.Encode("192.168.0.42", 7770);
            Assert.That(InviteCode.TryDecode(code, out string ip, out ushort port), Is.True, code);
            Assert.That(ip, Is.EqualTo("192.168.0.42"));
            Assert.That(port, Is.EqualTo((ushort)7770));
        }

        [Test]
        public void InviteCode_Normalizes_LowercaseAndNoDash()
        {
            string code = InviteCode.Encode("10.0.0.1", 25565);
            string mangled = code.Replace("-", "").ToLowerInvariant();
            Assert.That(InviteCode.TryDecode(mangled, out string ip, out ushort port), Is.True);
            Assert.That(ip, Is.EqualTo("10.0.0.1"));
            Assert.That(port, Is.EqualTo((ushort)25565));
        }

        [Test]
        public void InviteCode_Rejects_Garbage()
        {
            Assert.That(InviteCode.TryDecode("", out _, out _), Is.False);
            Assert.That(InviteCode.TryDecode("nope", out _, out _), Is.False);
            Assert.That(InviteCode.TryDecode("IIIII-LLLLL", out _, out _), Is.False); // I/L 알파벳 외
        }

        [Test]
        public void Logic_Host_StartsAndShowsCode()
        {
            FakeSession session = new FakeSession { HostResult = true, InviteCodeValue = "ABCDE-12345" };
            MultiplayerLobbyLogic logic = new MultiplayerLobbyLogic(session);

            string status = logic.Host();

            Assert.That(session.StartHostCalls, Is.EqualTo(1));
            Assert.That(status, Does.Contain("ABCDE-12345"));
        }

        [Test]
        public void Logic_Host_FailureReported()
        {
            FakeSession session = new FakeSession { HostResult = false };
            MultiplayerLobbyLogic logic = new MultiplayerLobbyLogic(session);
            Assert.That(logic.Host(), Does.Contain("실패"));
        }

        [Test]
        public void Logic_Join_EmptyCodeRejectedWithoutCallingSession()
        {
            FakeSession session = new FakeSession();
            MultiplayerLobbyLogic logic = new MultiplayerLobbyLogic(session);

            string status = logic.Join("   ");

            Assert.That(session.JoinCalls, Is.EqualTo(0), "빈 코드인데 세션 호출됨");
            Assert.That(status, Does.Contain("입력"));
        }

        [Test]
        public void Logic_Join_SuccessAndFailure()
        {
            FakeSession ok = new FakeSession { JoinResult = true };
            MultiplayerLobbyLogic okLogic = new MultiplayerLobbyLogic(ok);
            Assert.That(okLogic.Join("ABCDE-12345"), Does.Contain("진입"));
            Assert.That(ok.LastJoinCode, Is.EqualTo("ABCDE-12345"));
            Assert.That(okLogic.LastSucceeded, Is.True, "성공인데 LastSucceeded=false (World 진입 트리거 안 됨)");

            FakeSession bad = new FakeSession { JoinResult = false };
            MultiplayerLobbyLogic badLogic = new MultiplayerLobbyLogic(bad);
            Assert.That(badLogic.Join("ABCDE-12345"), Does.Contain("실패"));
            Assert.That(badLogic.LastSucceeded, Is.False, "실패인데 LastSucceeded=true (World 오진입)");
        }

        // 테스트 더블 — INetworkSessionControl (DomainSDK seam). NetCode 실런타임 무의존.
        private sealed class FakeSession : INetworkSessionControl
        {
            public bool HostResult;
            public bool JoinResult;
            public string InviteCodeValue = "TEST0-00000";
            public int StartHostCalls;
            public int JoinCalls;
            public string LastJoinCode;

            public bool IsActive => StartHostCalls > 0 || JoinCalls > 0;

            public bool StartHost()
            {
                StartHostCalls++;
                return HostResult;
            }

            public bool JoinByCode(string inviteCode)
            {
                JoinCalls++;
                LastJoinCode = inviteCode;
                return JoinResult;
            }

            public string GetHostInviteCode() => InviteCodeValue;
        }
    }
}
