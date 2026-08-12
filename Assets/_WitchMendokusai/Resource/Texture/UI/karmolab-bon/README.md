# karmolab-bon — KarmoLab 「본」으로 찍은 UI 부품

이 폴더의 SVG 는 손으로 그린 것이 아니라 **KarmoLab 의 「본」**(`/karmolab/#bon`)에서 숫자를
돌려 뽑은 것이다. 값이 `*.9slice.json` 의 `knobs` 에 그대로 남아 있으므로, 색이나 모서리를
바꾸고 싶으면 그 값으로 다시 열어 뽑으면 된다.

## 쓸 때

`*.9slice.json` 의 `unityBorder` 가 **[왼, 아래, 오른, 위]** 순서다 — 스프라이트 임포터의
`border` 에 그대로 넣는다. 그러면 버튼·창틀을 아무 크기로 늘려도 **모서리가 안 뭉개진다**.

| 파일 | border (L,B,R,T) | 쓰임 |
| --- | --- | --- |
| `wm-button.svg` | 16, 14, 16, 14 | 누르는 것 |
| `wm-panel.svg` | 24, 24, 24, 24 | 창·상자 |
| `wm-gauge.svg` | 12, 10, 12, 10 | 차오르는 것 |

색은 Yon 의 집 결에 맞췄다(어두운 나무 + 호박빛 테두리). 게이지만 붉은 계열이다.

라이선스 CC0-1.0 — 같은 것이 KarmoLab 선반(`laptop.mascari4615.com/foundry`)에도 있다.
