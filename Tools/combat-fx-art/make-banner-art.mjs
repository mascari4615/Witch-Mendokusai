// 전투 띠 연출 (BannerFx, Banner.uss) 용 흰색 + 알파 그림 생성
// UI Toolkit 그라데이션 부재. 알파 곡선을 그림으로 굽고 USS tint 로 색 입힘
// 실행: node Tools/combat-fx-art/make-banner-art.mjs (저장소 뿌리에서). 결과는 Assets/_WitchMendokusai/Domain/Dungeon/Fx/Banner/Art/
// 외부 패키지 없이 node 내장 zlib 만 씀

import { deflateSync } from "node:zlib";
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const outputDirectory = resolve(scriptDirectory, "../../Assets/_WitchMendokusai/Domain/Dungeon/Fx/Banner/Art");

const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++)
{
	let c = n;
	for (let k = 0; k < 8; k++)
		c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
	crcTable[n] = c >>> 0;
}

function crc32(buffer)
{
	let crc = 0xffffffff;
	for (const byte of buffer)
		crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
	return (crc ^ 0xffffffff) >>> 0;
}

function chunk(type, data)
{
	const length = Buffer.alloc(4);
	length.writeUInt32BE(data.length);
	const typeAndData = Buffer.concat([Buffer.from(type, "ascii"), data]);
	const crc = Buffer.alloc(4);
	crc.writeUInt32BE(crc32(typeAndData));
	return Buffer.concat([length, typeAndData, crc]);
}

// alphaAt(x, y) 는 0 ~ 1. 색은 흰색 고정
function writeWhiteAlphaPng(fileName, width, height, alphaAt)
{
	const raw = Buffer.alloc((width * 4 + 1) * height);
	for (let y = 0; y < height; y++)
	{
		const rowStart = y * (width * 4 + 1);
		raw[rowStart] = 0;
		for (let x = 0; x < width; x++)
		{
			const alpha = Math.max(0, Math.min(1, alphaAt(x, y)));
			const pixel = rowStart + 1 + x * 4;
			raw[pixel] = 255;
			raw[pixel + 1] = 255;
			raw[pixel + 2] = 255;
			raw[pixel + 3] = Math.round(alpha * 255);
		}
	}
	const header = Buffer.alloc(13);
	header.writeUInt32BE(width, 0);
	header.writeUInt32BE(height, 4);
	header[8] = 8;
	header[9] = 6;
	header[10] = 0;
	header[11] = 0;
	header[12] = 0;
	const png = Buffer.concat([
		Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
		chunk("IHDR", header),
		chunk("IDAT", deflateSync(raw, { level: 9 })),
		chunk("IEND", Buffer.alloc(0)),
	]);
	mkdirSync(outputDirectory, { recursive: true });
	writeFileSync(join(outputDirectory, fileName), png);
	console.log(`${fileName} ${width}x${height}`);
}

function smoothstep(edge0, edge1, value)
{
	const t = Math.max(0, Math.min(1, (value - edge0) / (edge1 - edge0)));
	return t * t * (3 - 2 * t);
}

// 가운데 0, 양 끝 1 인 가로 위치
const distanceFromCenter = (x, width) => Math.abs((x + 0.5) / width * 2 - 1);

// 띠: 가운데 짙고 양 끝과 위아래로 부드럽게 사라짐
writeWhiteAlphaPng("banner-band.png", 512, 64, (x, y) =>
{
	const horizontal = 1 - smoothstep(0.18, 1.0, distanceFromCenter(x, 512));
	const vertical = 1 - smoothstep(0.45, 1.0, Math.abs((y + 0.5) / 64 * 2 - 1));
	return horizontal * vertical;
});

// 가는 선: 가운데 넓게 또렷하고 양 끝에서 사라짐
writeWhiteAlphaPng("banner-line.png", 512, 4, (x) =>
{
	return 1 - smoothstep(0.35, 1.0, distanceFromCenter(x, 512));
});

// 화면 가장자리 어둡게: 가운데 투명, 바깥으로 갈수록 짙음
writeWhiteAlphaPng("banner-vignette.png", 256, 256, (x, y) =>
{
	const dx = (x + 0.5) / 256 * 2 - 1;
	const dy = (y + 0.5) / 256 * 2 - 1;
	const radius = Math.sqrt(dx * dx + dy * dy) / Math.SQRT2;
	return smoothstep(0.32, 1.0, radius);
});

// 가는 원호: 원 둘레의 두 호. 호 양 끝이 가늘게 사라짐. 회전해도 같은 결이 되게 반 바퀴 대칭
writeWhiteAlphaPng("banner-arc.png", 256, 256, (x, y) =>
{
	const dx = x + 0.5 - 128;
	const dy = y + 0.5 - 128;
	const distance = Math.sqrt(dx * dx + dy * dy);
	const ringRadius = 122;
	const halfStroke = 1.1;
	const stroke = 1 - smoothstep(halfStroke - 0.6, halfStroke + 0.6, Math.abs(distance - ringRadius));
	const angle = Math.atan2(dy, dx);
	// 반 바퀴마다 0 ~ 1
	const phase = ((angle / Math.PI) % 1 + 1) % 1;
	// 호는 반 바퀴 중 앞 70%. 양 끝 15% 구간에서 사라짐
	const arcLength = 0.7;
	const along = phase / arcLength;
	const inArc = phase < arcLength ? Math.min(smoothstep(0, 0.15, along), smoothstep(0, 0.15, 1 - along)) : 0;
	return stroke * inArc;
});

// 사선 줄무늬: 경고 테이프 결. 45도 줄, 양 끝에서 사라짐. 반복 대신 넓게 굽고 stretch-to-fill
writeWhiteAlphaPng("banner-stripe.png", 1024, 16, (x, y) =>
{
	const period = 24;
	const stripeWidth = 12;
	const position = ((x + 0.5 + (15.5 - (y + 0.5))) % period + period) % period;
	const edge = 0.8;
	const stripe = smoothstep(0, edge, position) * (1 - smoothstep(stripeWidth - edge, stripeWidth, position));
	const horizontal = 1 - smoothstep(0.55, 1.0, distanceFromCenter(x, 1024));
	return stripe * horizontal;
});

// 한쪽 꼬리: 왼쪽 짙고 오른쪽으로 사라짐. 상호작용 표시 줄의 꼬리
writeWhiteAlphaPng("banner-fade.png", 256, 8, (x) =>
{
	return 1 - smoothstep(0.0, 1.0, (x + 0.5) / 256);
});
