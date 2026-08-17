using System.Text.Json.Serialization;
using WitchMendokusai;
using WitchMendokusai.Net;

namespace WitchMendokusai.Wasm
{
	/// <summary>
	/// 브라우저용 글자 변환 표 (TASK-WM-411).
	///
	/// ★ 왜 소스 생성인가 (2026-08-17 실측): 웹어셈블리는 리플렉션 직렬화가 <b>꺼진 채로</b> 나온다.
	///   그대로 두면 브라우저 콘솔에 <c>JsonSerializerIsReflectionDisabled</c> 만 쏟아지고 판정이 말을 못 만든다.
	///   설정으로 켜는 길도 있지만, 그건 「트리밍이 지운 것을 도로 살려 달라」는 부탁이라 앱이 커지고 언제 또 꺼질지 모른다.
	///   여기서는 <b>컴파일 때 변환 코드를 만들어 둔다</b> — 리플렉션이 필요 없고, 지워질 것도 없다.
	///
	/// 말의 <b>모양</b>은 여전히 하나다(DomainSDK/Net). 만드는 방법만 브라우저 사정에 맞춘 것이다.
	/// </summary>
	[JsonSourceGenerationOptions(IncludeFields = true)]
	[JsonSerializable(typeof(VersusJoinMessage))]
	[JsonSerializable(typeof(VersusStartMessage))]
	[JsonSerializable(typeof(VersusInputMessage))]
	[JsonSerializable(typeof(VersusStateMessage))]
	[JsonSerializable(typeof(VersusRoundEndMessage))]
	[JsonSerializable(typeof(VersusOfferMessage))]
	[JsonSerializable(typeof(VersusPickMessage))]
	[JsonSerializable(typeof(VersusRematchMessage))]
	[JsonSerializable(typeof(VersusRematchStateMessage))]
	[JsonSerializable(typeof(VersusMatchEndMessage))]
	[JsonSerializable(typeof(VersusRoundStartMessage))]
	[JsonSerializable(typeof(VersusSnapshotMessage))]
	[JsonSerializable(typeof(VersusRoundSnapshot))]
	[JsonSerializable(typeof(VersusFighterSnapshot))]
	[JsonSerializable(typeof(VersusShotSnapshot))]
	[JsonSerializable(typeof(VersusRemoteInput))]
	[JsonSerializable(typeof(VersusBodyMessage))]
	[JsonSerializable(typeof(VersusFighterStats))]
	[JsonSerializable(typeof(VersusViewPacket))]
	[JsonSerializable(typeof(VersusHudPacket))]
	public partial class VersusJsonContext : JsonSerializerContext
	{
	}
}
