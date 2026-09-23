#!/usr/bin/env node
// One source for local diagnosis and the pre-Unity build gate. No Unity launch.
import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';

const ownPath = (path) => /^(Assets\/_WitchMendokusai|DomainSDK)\//.test(path);
const ownName = (name) => /^(WM\.|WitchMendokusai\.|Gwan\.)/.test(name);
const workflowPath = '.github/workflows/wm-idle-build.yml';
const stringToken = /@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'/g;
const blank = (text) => text.replace(/[^\r\n]/g, ' ');
const noComments = (text) => text.replace(/@"(?:[^"]|"")*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\/\/[^\r\n]*|\/\*[\s\S]*?\*\//g,
  (token) => token.startsWith('/') ? blank(token) : token);
const codeOnly = (text) => text.replace(stringToken, blank);
const noYamlComments = (text) => text.replace(/^\s*#[^\r\n]*/gm, '');

export function readSnapshot(root) {
  const top = execFileSync('git', ['rev-parse', '--show-toplevel'], { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  if (resolve(top).toLowerCase() !== resolve(root).toLowerCase()) throw new Error('Expected the repository root.');
  const paths = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'],
    { cwd: root, encoding: 'utf8', maxBuffer: 8388608 }).split('\0').filter(Boolean);
  const files = new Map();
  for (const path of paths) {
    if (!existsSync(resolve(root, path))) continue;
    if (/\.asmdef(?:\.meta)?$|\/IdlePlayerBuild\.cs$/.test(path) ||
      [workflowPath, '.github/workflows/build.yml', 'ProjectSettings/ProjectVersion.txt'].includes(path)) {
      files.set(path, readFileSync(resolve(root, path), 'utf8').replace(/^\uFEFF/, ''));
    } else if (/\.unity(?:\.meta)?$/.test(path)) files.set(path, '');
  }
  return files;
}

function bodyOf(source, method) {
  const masked = codeOnly(source);
  const match = new RegExp(`public\\s+static\\s+void\\s+${method}\\s*\\(\\s*\\)\\s*\\{`).exec(masked);
  if (!match) return null;
  const start = match.index + match[0].length;
  let depth = 1;
  for (let index = start; index < masked.length; index++) {
    if (masked[index] === '{') depth++;
    if (masked[index] === '}' && --depth === 0) return source.slice(start, index);
  }
  return null;
}

// Unknown symbols stay unknown. AND across lines, OR within one line.
function active(definition, platform) {
  if ((definition.includePlatforms?.length && !definition.includePlatforms.includes(platform)) ||
    definition.excludePlatforms?.includes(platform)) return false;
  const symbols = { WM_IDLE: true, UNITY_EDITOR: false, UNITY_INCLUDE_TESTS: false,
    UNITY_ANDROID: platform === 'Android', UNITY_STANDALONE: platform === 'WindowsStandalone64',
    UNITY_STANDALONE_WIN: platform === 'WindowsStandalone64' };
  const lines = (definition.defineConstraints ?? []).map((line) => {
    const terms = line.split('||').map((term) => {
      const token = term.trim();
      if (!/^!?[A-Za-z_]\w*$/.test(token)) return null;
      const negated = token.startsWith('!');
      const value = symbols[negated ? token.slice(1) : token];
      return value === undefined ? null : negated ? !value : value;
    });
    return terms.includes(true) ? true : terms.includes(null) ? null : false;
  });
  return lines.includes(false) ? false : lines.includes(null) ? null : true;
}

export function audit(files) {
  const checks = [];
  const add = (id, status, detail) => checks.push({ id, status, detail, next: status === 'PASS' ? '' : 'Fix the reported file, then rerun the config audit.' });
  const requireText = (path) => {
    if (!files.has(path)) { add('file', 'FAIL', `Missing ${path}`); return ''; }
    return files.get(path);
  };
  const workflow = noYamlComments(requireText(workflowPath));
  const mainWorkflow = noYamlComments(requireText('.github/workflows/build.yml'));
  const version = requireText('ProjectSettings/ProjectVersion.txt');
  const sources = [...files.keys()].filter((path) => ownPath(path) && path.endsWith('/IdlePlayerBuild.cs'));
  if (sources.length !== 1) add('entrypoint', 'FAIL', `Expected one IdlePlayerBuild.cs, found ${sources.length}`);
  const sourcePath = sources[0] ?? '';
  const source = noComments(files.get(sourcePath) ?? '');
  if (/^\s*#(if|elif|else|endif)\b/m.test(codeOnly(source))) add('entrypoint-conditional', 'CANNOT-RUN', 'Conditional builder source requires compilation or explicit parser support');
  const namespace = codeOnly(source).match(/\bnamespace\s+([\w.]+)/)?.[1];
  const calls = workflow.match(/\$method\s*=\s*if\s*\(\$platform\s+-eq\s+'android'\)\s*{\s*'([^']+)'\s*}\s*else\s*{\s*'([^']+)'\s*}/);
  const validClass = /\bpublic\s+static\s+class\s+IdlePlayerBuild\b/.test(codeOnly(source));
  if (!calls) add('entrypoint', 'CANNOT-RUN', 'Unrecognized platform-to-method assignment in workflow');
  for (const [method, called, target, group] of [
    ['BuildAndroid', calls?.[1], 'Android', 'Android'], ['Build', calls?.[2], 'StandaloneWindows64', 'Standalone'],
  ]) {
    const body = bodyOf(source, method);
    add(`entrypoint-${method}`, namespace && validClass && body && called === `${namespace}.IdlePlayerBuild.${method}` ? 'PASS' : 'FAIL',
      `${method}: workflow ${called ?? 'missing'}, source ${namespace ?? 'missing'}.IdlePlayerBuild`);
    if (!body) continue;
    const patterns = [
      /\boptions\.scenes\s*=\s*new\s+string\[\]\s*{\s*SCENE_PATH\s*}\s*;/,
      /\boptions\.extraScriptingDefines\s*=\s*new\s+string\[\]\s*{\s*IDLE_DEFINE\s*}\s*;/,
      new RegExp(`\\boptions\\.target\\s*=\\s*BuildTarget\\.${target}\\s*;`),
      new RegExp(`\\boptions\\.targetGroup\\s*=\\s*BuildTargetGroup\\.${group}\\s*;`),
      /\bBuildPipeline\.BuildPlayer\(options\)/,
    ];
    add(`options-${method}`, patterns.every((pattern) => pattern.test(codeOnly(body))) ? 'PASS' : 'FAIL',
      `${method}: scene, WM_IDLE define, target and BuildPlayer options`);
  }
  const scene = source.match(/\bconst\s+string\s+SCENE_PATH\s*=\s*"([^"\r\n]+)"\s*;/)?.[1];
  add('scene', scene && files.has(scene) && files.has(`${scene}.meta`) ? 'PASS' : 'FAIL', `Build scene and metadata: ${scene ?? 'missing'}`);
  add('idle-define', /\bconst\s+string\s+IDLE_DEFINE\s*=\s*"WM_IDLE"\s*;/.test(source) ? 'PASS' : 'FAIL', 'Player-only WM_IDLE define');
  const editorVersion = version.match(/^m_EditorVersion:\s*(\S+)\s*$/m)?.[1];
  const revision = version.match(/^m_EditorVersionWithRevision:\s*(\S+)\s+\(([a-f0-9]{12})\)\s*$/m);
  add('unity-version', editorVersion && revision?.[1] === editorVersion && /^\d+\.\d+\.\d+[abfp]\d+$/.test(editorVersion) ? 'PASS' : 'FAIL',
    `ProjectVersion version/revision: ${editorVersion ?? 'missing'}`);
  add('workflow-target', /\$target\s*=\s*if\s*\(\$platform\s+-eq\s+'android'\)\s*{\s*'Android'\s*}\s*else\s*{\s*'Win64'\s*}/.test(workflow) &&
    /'-buildTarget',\s*\$target/.test(workflow) && /'-executeMethod',\s*\$method/.test(workflow) ? 'PASS' : 'FAIL', 'Unity launch target and executeMethod arguments');
  const group = (text) => text.match(/^concurrency:\s*\n\s+group:\s*([^\r\n]+)\s*$/m)?.[1]?.trim();
  add('build-serialization', group(workflow) === 'wm-build' && group(mainWorkflow) === group(workflow) &&
    /cancel-in-progress:\s*false/.test(workflow) ? 'PASS' : 'FAIL', 'Main and Idle builds share wm-build without cancellation');
  const preflight = workflow.split(/^      - /m).find((step) => /^name: Check Idle build configuration\s*\n/.test(step));
  add('preflight-wiring', preflight && /run: node \.github\/scripts\/wm-idle-config-audit\.mjs\s*$/.test(preflight.trim()) &&
    !/^\s+(if|continue-on-error):/m.test(preflight) && workflow.indexOf('name: Check Idle build configuration') < workflow.indexOf('name: Resolve Unity editor') ? 'PASS' : 'FAIL',
    'Required config audit before Unity resolution and launch');

  const assemblies = [];
  const names = new Map();
  const guids = new Map();
  for (const [path, text] of files) {
    if (!path.endsWith('.asmdef')) continue;
    try {
      const definition = JSON.parse(text);
      const guid = files.get(`${path}.meta`)?.match(/^guid:\s*([a-f0-9]{32})\s*$/m)?.[1];
      if (ownPath(path)) {
        if (typeof definition.name !== 'string' || !definition.name.trim()) throw new Error('Missing name');
        for (const field of ['references', 'defineConstraints', 'includePlatforms', 'excludePlatforms']) {
          if (definition[field] !== undefined && (!Array.isArray(definition[field]) || definition[field].some((value) => typeof value !== 'string'))) throw new Error(`Invalid ${field}`);
        }
        const references = definition.references ?? [];
        if (references.some((value) => value.startsWith('GUID:')) && references.some((value) => !value.startsWith('GUID:'))) throw new Error('Mixed GUID and assembly-name references');
        if (references.some((value) => value.startsWith('GUID:') && !/^GUID:[a-f0-9]{32}$/i.test(value))) throw new Error('Invalid reference GUID');
        if (definition.includePlatforms?.length && definition.excludePlatforms?.length) throw new Error('includePlatforms and excludePlatforms both set');
        if (!guid) throw new Error('Missing or invalid .meta GUID');
        if (names.has(definition.name) || guids.has(guid)) throw new Error('Duplicate assembly name or GUID');
      }
      const item = { path, definition, guid, own: ownPath(path) };
      assemblies.push(item);
      names.set(definition.name, item);
      if (guid) guids.set(guid, item);
    } catch (error) { add('asmdef-schema', ownPath(path) ? 'FAIL' : 'CANNOT-RUN', `${path}: ${error.message}`); }
  }
  const own = assemblies.filter((item) => item.own);
  if (!own.length) add('asmdef-graph', 'CANNOT-RUN', 'No first-party assembly definitions');
  const builderAssembly = own.filter((item) => sourcePath.startsWith(`${dirname(item.path).replaceAll('\\', '/')}/`))
    .sort((left, right) => right.path.length - left.path.length)[0];
  add('builder-editor', builderAssembly?.definition.includePlatforms?.length === 1 &&
    builderAssembly.definition.includePlatforms[0] === 'Editor' ? 'PASS' : 'FAIL', 'Build class belongs to an Editor-only assembly');
  for (const platform of ['WindowsStandalone64', 'Android']) {
    const before = checks.length;
    const states = new Map(own.map((item) => [item, active(item.definition, platform)]));
    if (states.get(names.get('WM.Idle')) !== true) add('idle-runtime', 'FAIL', `${platform}: WM.Idle runtime assembly must be present and enabled`);
    for (const item of own) {
      if (states.get(item) === false) continue;
      if (states.get(item) === null) { add('asmdef-graph', 'CANNOT-RUN', `${platform}: unknown define constraint in ${item.path}`); continue; }
      for (const reference of item.definition.references ?? []) {
        const target = reference.startsWith('GUID:') ? guids.get(reference.slice(5).toLowerCase()) : names.get(reference);
        if (!target && (ownName(reference) || reference.startsWith('GUID:'))) {
          add('asmdef-reference', reference.startsWith('GUID:') ? 'CANNOT-RUN' : 'FAIL', `${platform}: ${item.definition.name} -> unresolved ${reference}`);
        } else if (target?.own && states.get(target) !== true) {
          add('asmdef-reference', states.get(target) === false ? 'FAIL' : 'CANNOT-RUN', `${platform}: ${item.definition.name} -> excluded or unknown ${target.definition.name}`);
        }
      }
    }
    const visited = new Set();
    const visiting = new Set();
    const visit = (item, chain) => {
      if (visiting.has(item)) { add('asmdef-cycle', 'FAIL', `${platform}: ${[...chain, item.definition.name].join(' -> ')}`); return; }
      if (visited.has(item) || states.get(item) !== true) return;
      visiting.add(item);
      for (const reference of item.definition.references ?? []) {
        const target = reference.startsWith('GUID:') ? guids.get(reference.slice(5).toLowerCase()) : names.get(reference);
        if (target?.own) visit(target, [...chain, item.definition.name]);
      }
      visiting.delete(item); visited.add(item);
    };
    for (const item of own) visit(item, []);
    if (checks.length === before && own.length) add('asmdef-graph', 'PASS', `${platform}: ${own.length} first-party assemblies checked (external package internals excluded)`);
  }
  return checks;
}

export const exitCode = (checks) => checks.some((item) => item.status === 'FAIL') ? 1
  : checks.length && checks.every((item) => item.status === 'PASS') ? 0 : 2;

export function main(args = process.argv.slice(2)) {
  let checks;
  let values;
  try {
    ({ values } = parseArgs({ args, options: { root: { type: 'string' }, json: { type: 'boolean' } } }));
    checks = audit(readSnapshot(resolve(values.root ?? fileURLToPath(new URL('../..', import.meta.url)))));
  } catch (error) { checks = [{ id: 'config', status: 'CANNOT-RUN', detail: error.message }]; }
  const report = { checks, exitCode: exitCode(checks) };
  if (values?.json) console.log(JSON.stringify(report));
  else for (const item of checks) console.log(`[${item.status}] ${item.id}: ${item.detail}`);
  process.exitCode = report.exitCode;
  return report;
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) main();
