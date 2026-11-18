// 몬스터 처치 파편 (MonsterDeathStyle 처치 파편, DeathSpark.mat) 용 흰색 + 알파 원 그림 생성
// 가운데 짙고 바깥으로 부드럽게 사라짐. 파티클 색이 곱해지고 속도 방향으로 늘어남
// 실행: node Tools/combat-fx-art/make-spark-art.mjs (저장소 뿌리에서). 결과는 Assets/_WitchMendokusai/Domain/Dungeon/Fx/Life/
// 외부 패키지 없이 node 내장 zlib 만 씀

import { deflateSync } from "node:zlib";
import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const outputDirectory = resolve(scriptDirectory, "../../Assets/_WitchMendokusai/Domain/Dungeon/Fx/Life");

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

// 부드러운 원: 가운데 1, 반지름 절반에서 약 0.35, 가장자리 0
const SIZE = 64;
writeWhiteAlphaPng("DeathSpark.png", SIZE, SIZE, (x, y) =>
{
	const dx = (x + 0.5) / SIZE * 2 - 1;
	const dy = (y + 0.5) / SIZE * 2 - 1;
	const radius = Math.sqrt(dx * dx + dy * dy);
	return Math.pow(1 - smoothstep(0, 1, radius), 1.5);
});
