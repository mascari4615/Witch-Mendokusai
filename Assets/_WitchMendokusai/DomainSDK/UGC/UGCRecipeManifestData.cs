using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.UGC
{
	/// <summary>
	/// 팬이 공유하는 마도서 레시피 1개 매니페스트 = JSON 직렬화 정본 표면.
	/// Map/Seed 매니페스트와 같은 schemaVersion/version/author/tags/meta 컨벤션 — 기존 UGC 인프라(loader/validator) 정합.
	/// meta = free-form 문자열(JObject 동적 dict 대신) — sandbox 표면 closed 유지.
	/// </summary>
	[Serializable]
	public class UGCRecipeManifestData
	{
		public int schemaVersion;
		public string manifestId;
		public int version;
		public string author;
		public UGCRecipePageData recipe;
		public List<string> tags = new();
		public string note;
	}
}
