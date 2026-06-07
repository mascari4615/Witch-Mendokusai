using System;
using System.Text;

namespace WitchMendokusai.DomainSDK.Network
{
    /// <summary>
    /// TASK-WM-190 — 초대코드 codec (cozy 공유 표면). IPv4:port(6바이트) ↔ 짧은 코드 "XXXXX-XXXXX".
    ///
    /// 친구에게 IP 를 불러주는 대신 외우기 쉬운 코드. Crockford Base32(I·L·O·U 제외 = 혼동 0)로
    /// 48비트(IPv4 4 + port 2)를 10자에 인코딩. 순수 C#(Unity/FishNet 무의존) — DomainSDK 정합.
    /// 게임 UI 는 이 코드 문자열만 다루고, 디코드→연결은 WM.Network(NetworkSessionControl) 가.
    /// (LAN/직결 전제. 인터넷 릴레이/NAT 펀치 = 후속.)
    /// </summary>
    public static class InviteCode
    {
        public const ushort DEFAULT_PORT = 7770;

        // Crockford Base32 — I, L, O, U 제외(혼동 방지). 32자.
        private const string ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        /// <summary>IPv4("a.b.c.d") + port → "XXXXX-XXXXX". 잘못된 IPv4 = ArgumentException.</summary>
        public static string Encode(string ipv4, ushort port)
        {
            byte[] octets = ParseIPv4(ipv4);
            if (octets == null)
            {
                throw new ArgumentException($"invalid IPv4: '{ipv4}'");
            }

            // 6바이트 = IPv4(4) + port(2, big-endian) → 48비트 value.
            ulong value =
                ((ulong)octets[0] << 40) |
                ((ulong)octets[1] << 32) |
                ((ulong)octets[2] << 24) |
                ((ulong)octets[3] << 16) |
                ((ulong)(port >> 8) << 8) |
                (ulong)(port & 0xFF);

            // 48비트를 10×5비트(=50비트)에 담기 위해 2비트 패딩(상위정렬).
            ulong padded = value << 2;

            StringBuilder builder = new StringBuilder(11);
            for (int i = 0; i < 10; i++)
            {
                int shift = 45 - (i * 5);
                int index = (int)((padded >> shift) & 0x1F);
                builder.Append(ALPHABET[index]);
                if (i == 4)
                {
                    builder.Append('-');
                }
            }
            return builder.ToString();
        }

        /// <summary>"XXXXX-XXXXX"(대소문자·하이픈·공백 무관) → IPv4 + port. 실패 = false.</summary>
        public static bool TryDecode(string code, out string ipv4, out ushort port)
        {
            ipv4 = null;
            port = 0;
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            // 정규화: 하이픈/공백 제거 + 대문자.
            StringBuilder norm = new StringBuilder(10);
            foreach (char raw in code)
            {
                if (raw == '-' || char.IsWhiteSpace(raw))
                {
                    continue;
                }
                norm.Append(char.ToUpperInvariant(raw));
            }
            if (norm.Length != 10)
            {
                return false;
            }

            ulong padded = 0;
            for (int i = 0; i < 10; i++)
            {
                int index = ALPHABET.IndexOf(norm[i]);
                if (index < 0)
                {
                    return false;
                }
                padded |= (ulong)index << (45 - (i * 5));
            }

            ulong value = padded >> 2; // 패딩 2비트 제거 → 48비트.
            byte b0 = (byte)((value >> 40) & 0xFF);
            byte b1 = (byte)((value >> 32) & 0xFF);
            byte b2 = (byte)((value >> 24) & 0xFF);
            byte b3 = (byte)((value >> 16) & 0xFF);
            byte pHi = (byte)((value >> 8) & 0xFF);
            byte pLo = (byte)(value & 0xFF);

            ipv4 = $"{b0}.{b1}.{b2}.{b3}";
            port = (ushort)((pHi << 8) | pLo);
            return true;
        }

        // "a.b.c.d" → 4바이트. 형식/범위 위반 = null.
        private static byte[] ParseIPv4(string ipv4)
        {
            if (string.IsNullOrWhiteSpace(ipv4))
            {
                return null;
            }
            string[] parts = ipv4.Trim().Split('.');
            if (parts.Length != 4)
            {
                return null;
            }
            byte[] octets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (int.TryParse(parts[i], out int octet) == false || octet < 0 || octet > 255)
                {
                    return null;
                }
                octets[i] = (byte)octet;
            }
            return octets;
        }
    }
}
