// 웹어셈블리 진입 JS — <b>얇다</b>. 진짜 창은 `versus.html` 이고, 그쪽이 `_framework/dotnet.js` 를
// 직접 불러 `getAssemblyExports` 로 판정을 꺼내 쓴다(TASK-WM-411).
// 이 파일은 SDK 가 진입점을 요구해서 있는 것이다.
import { dotnet } from './_framework/dotnet.js';

await dotnet.create();
