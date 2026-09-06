using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	/// <summary>
	/// 세계가 아는 <b>사람 하나</b> (TASK-WM-218).
	///
	/// 이름도 얼굴도 아직 없다 — 「다시 온 그 사람」을 알아보는 데 필요한 최소만 있다.
	/// <see cref="Secret"/> 는 <b>세계만</b> 갖는다(창에는 그것만 주고, 세계는 그걸로 알아본다).
	/// </summary>
	[Serializable]
	public class WorldIdentityRecord
	{
		/// <summary>세계 안에서의 번호 — 인형·가방·집이 이 번호에 붙는다.</summary>
		public int id;

		/// <summary>
		/// ⚠ <b>옛 저장 파일을 읽기 위해서만</b> 남아 있는 자리 (TASK-WM-220). 새로 적을 때는 비어 있다.
		///
		/// 전에는 창이 갖고 다니는 열쇠를 세계가 <b>그대로</b> 적어 뒀다. 그 파일 한 장이 새면
		/// 모두의 신원을 그대로 가져갈 수 있다(백업·지원 문의·실수로 올린 로그 — 다 새는 길이다).
		/// 이제 세계는 <see cref="secretHash"/> 만 갖는다.
		/// </summary>
		public string secret = string.Empty;

		/// <summary>
		/// 열쇠의 <b>지문</b>(SHA-256). 세계는 이것만 갖고, 창이 내민 열쇠의 지문과 맞춰 본다.
		/// 지문에서 열쇠를 되돌릴 수는 없다 — 파일이 새도 남의 사람이 되지는 못한다.
		/// </summary>
		public string secretHash = string.Empty;

		/// <summary>마지막으로 본 시각(세계 기준 총 일수) — 오래된 신원 정리에 쓸 수 있다.</summary>
		public int lastSeenDay;

		/// <summary>
		/// 세계에서 불리는 이름 (TASK-WM-218). 비어 있으면 창이 「손님 N」으로 부른다.
		/// 계정으로 들어오면 그 계정의 이름이 여기 들어간다 — 그래야 남이 나를 알아본다.
		/// </summary>
		public string name = string.Empty;

		/// <summary>
		/// 바깥 계정(KarmoLab 등)의 이름표 — 있으면 <b>그게 진짜 나</b>다 (TASK-WM-218).
		/// 형식 = "제공자:아이디"(예: karmolab:mascari). 없으면 빈 문자열(기기 열쇠만 쓰는 손님).
		/// </summary>
		public string externalId = string.Empty;
	}
}

