using System;
using System.Threading.Tasks;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 서버를 켜는 자리 (TASK-WM-216 → 217). 세계를 굴리는 일은 <see cref="WorldHost"/> 가 한다.
	/// </summary>
	public static class Program
	{
		public static async Task Main(string[] args)
		{
			// 계약 뽑기 — 서버가 소유한 정의에서 웹이 쓸 타입 선언을 만든다.
			// 시험이 「뽑은 것 == 저장된 것」을 보므로, 계약을 고치면 이 명령을 다시 돌려야 한다.
			if (args.Length > 0 && args[0] == "--emit-protocol")
			{
				string outputPath = System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot", "protocol.d.ts");
				if (args.Length > 1)
					outputPath = args[1];

				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
				System.IO.File.WriteAllText(outputPath, Protocol.ToTypeScript());
				Console.WriteLine("계약을 뽑았다: " + outputPath);
				return;
			}

			WorldHost host = new WorldHost(WorldStore.Default());
			await host.Build(args).RunAsync();
		}
	}
}
