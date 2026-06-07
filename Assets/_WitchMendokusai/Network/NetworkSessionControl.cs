using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-190 — 멀티 진입 seam 의 WM.Network impl. DomainSDK INetworkSessionControl 을
    /// 구현하고 AfterAssembliesLoaded 에 NetworkSessionBridge 에 register → Domain 로비 UI 가
    /// NetCode 직접참조 없이(boundary 게이트 준수) 호스트/참가 트리거. NetworkBootstrap + InviteCode 위임.
    /// </summary>
    public sealed class NetworkSessionControl : INetworkSessionControl
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            NetworkSessionBridge.Register(new NetworkSessionControl());
        }

        public bool IsActive => NetworkBootstrap.IsRunning;

        public bool StartHost() => NetworkBootstrap.StartHost();

        public bool JoinByCode(string inviteCode)
        {
            if (InviteCode.TryDecode(inviteCode, out string address, out ushort port) == false)
            {
                Debug.LogWarning($"[NetSession] 초대코드 파싱 실패: '{inviteCode}'");
                return false;
            }
            Debug.Log($"[NetSession] 참가 — {address}:{port}");
            return NetworkBootstrap.StartClient(address, port);
        }

        public string GetHostInviteCode() => InviteCode.Encode(LocalIPv4(), NetworkBootstrap.DEFAULT_PORT);

        // LAN IPv4 (친구가 같은 망에서 연결). 못 찾으면 loopback(같은 PC 테스트용).
        private static string LocalIPv4()
        {
            try
            {
                IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && IPAddress.IsLoopback(ip) == false)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetSession] 로컬 IP 조회 실패 — loopback 폴백: {ex.Message}");
            }
            return "127.0.0.1";
        }
    }
}
