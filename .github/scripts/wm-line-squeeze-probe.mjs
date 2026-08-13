#!/usr/bin/env node
// wm-line-squeeze-probe.mjs — <b>회선을 좁히는 것이 진짜로 먹나</b> (2026-08-14 진단 도구).
//
// ★ 왜 남기나: 좁은 회선 관문이 세계의 방어를 <b>여섯 가지로 꺼도</b> 초록이었다. 원인을 관문 안에서
//   찾다 못 찾아, 관문 밖에서 <b>자 자체</b>를 쟀다 — 그러자 곧바로 나왔다:
//   넓을 때 14.0KB/s → 초당 3KB 로 좁힌 뒤 <b>14.1KB/s</b>. 즉 좁히기가 안 먹는다.
//   (조각마다 제 몫의 시간은 붙는데 앞 조각과 <b>겹치지 않아</b> 폭이 안 생긴다.)
//
// 이 파일은 관문이 아니라 <b>자를 재는 자</b>다 — 고칠 때 이걸로 먼저 확인한다.
// 실행: node .github/scripts/wm-line-squeeze-probe.mjs

import { spawn, execSync } from 'node:child_process';
import { mkdtempSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { openBadLine } from './lib/bad-line.mjs';

const wait = (ms) => new Promise((d) => setTimeout(d, ms));
const worldPort = 5412;
const linePort = 5413;

const out = join(mkdtempSync(join(tmpdir(), 'wm-sq-')), 'app');
execSync(`dotnet publish "${new URL("../../Server/WM.Server/WM.Server.csproj", import.meta.url).pathname.slice(1)}" -c Release -o "${out}" --nologo`, { stdio: 'pipe' });
const dll = join(out, 'WM.Server.dll');
const worldFile = join(mkdtempSync(join(tmpdir(), 'wm-sqw-')), 'world.json');
const world = spawn('dotnet', [dll, '--urls', `http://127.0.0.1:${worldPort}`], {
	cwd: dirname(dll), env: { ...process.env, WM_WORLD_FILE: worldFile }, stdio: 'ignore',
});

for (let i = 0; i < 200; i += 1) {
	try { if ((await fetch(`http://127.0.0.1:${worldPort}/health`)).ok) break; } catch { /* 아직 */ }
	await wait(300);
}

const line = openBadLine({ listenPort: linePort, targetPort: worldPort, latencyMs: 100, jitterMs: 10, bytesPerSecond: 32000 });
await line.listen();

// 광장을 만든다 — 봇 여럿이 곧은 회선으로 붙어 계속 걷는다.
const bots = [];
for (let i = 0; i < 40; i += 1) {
	const one = new WebSocket(`ws://127.0.0.1:${worldPort}/ws`);
	one.onopen = () => one.send(JSON.stringify({ type: 'hello', secret: '' }));
	one.onerror = () => {};
	bots.push(one);
}
const milling = setInterval(() => {
	for (let i = 0; i < bots.length; i += 1) {
		if (bots[i].readyState !== 1) continue;
		bots[i].send(JSON.stringify({ type: 'move', x: i % 2 ? 0.15 : -0.15, z: 0 }));
	}
}, 100);

// 재는 사람 — 좁은 회선 너머
let bytes = 0;
let plates = 0;
const me = new WebSocket(`ws://127.0.0.1:${linePort}/ws`);
me.onopen = () => me.send(JSON.stringify({ type: 'hello', secret: '' }));
me.onerror = () => {};
me.onmessage = (event) => {
	plates += 1;
	bytes += typeof event.data === 'string' ? Buffer.byteLength(event.data, 'utf8') : event.data.byteLength;
};

await wait(6000);
bytes = 0; plates = 0;
await wait(4000);
console.log(`넓을 때 — ${plates}판 · ${(bytes / 4 / 1000).toFixed(1)}KB/s`);

console.log('좁히기 전 파이프:', JSON.stringify(line.peek()));
line.squeeze(3000);
await wait(2000);
bytes = 0; plates = 0;
await wait(4000);
console.log(`좁힌 뒤(3.0KB/s 로) — ${plates}판 · ${(bytes / 4 / 1000).toFixed(1)}KB/s`);
console.log('좁힌 뒤 파이프:', JSON.stringify(line.peek()));

clearInterval(milling);
for (const one of bots) { try { one.close(); } catch {} }
try { me.close(); } catch {}
line.close();
try { execSync(`taskkill /PID ${world.pid} /F /T`, { stdio: 'ignore' }); } catch {}
