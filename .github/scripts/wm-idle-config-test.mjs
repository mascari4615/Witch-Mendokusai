import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { audit, exitCode, readSnapshot } from './wm-idle-config-audit.mjs';

const root = fileURLToPath(new URL('../..', import.meta.url));
const baseline = readSnapshot(root);
const workflow = '.github/workflows/wm-idle-build.yml';
const source = [...baseline.keys()].find((path) => path.endsWith('/IdlePlayerBuild.cs'));
const definitionPath = (files, name) => [...files.keys()].find((path) => path.endsWith('.asmdef') && JSON.parse(files.get(path)).name === name);
const changeDefinition = (files, name, change) => {
  const path = definitionPath(files, name);
  const definition = JSON.parse(files.get(path)); change(definition); files.set(path, JSON.stringify(definition));
};
const changed = (change) => { const files = new Map(baseline); change(files); return audit(files); };
const red = (name, change, id) => test(name, () => {
  const checks = changed(change);
  assert.equal(exitCode(checks), 1, JSON.stringify(checks));
  assert.ok(checks.some((item) => item.id === id && item.status === 'FAIL'), JSON.stringify(checks));
});

test('current repository configuration passes with real source and assembly metadata', () => assert.equal(exitCode(audit(baseline)), 0));
red('old namespace is rejected', (files) => files.set(workflow, files.get(workflow).replaceAll('WitchMendokusai.Idle.Editor', 'WitchMendokusai.EditorTools')), 'entrypoint-Build');
red('Windows and Android methods cannot be swapped', (files) => files.set(workflow, files.get(workflow).replace(".IdlePlayerBuild.BuildAndroid'", ".IdlePlayerBuild.Build'")), 'entrypoint-BuildAndroid');
red('a method mentioned only in a comment is not callable', (files) => files.set(source, files.get(source).replace('public static void BuildAndroid()', '// public static void BuildAndroid()')), 'entrypoint-BuildAndroid');
red('WM.App exclusion regression is caught', (files) => changeDefinition(files, 'WM.App', (value) => { value.defineConstraints = []; }), 'asmdef-reference');
red('another runtime assembly exclusion regression is also caught', (files) => changeDefinition(files, 'WM.Network', (value) => { value.defineConstraints = []; }), 'asmdef-reference');
red('missing internal reference is rejected', (files) => changeDefinition(files, 'WM.Idle', (value) => value.references.push('WM.DoesNotExist')), 'asmdef-reference');
red('runtime dependency cycles are rejected', (files) => changeDefinition(files, 'WM.Foundation', (value) => { value.references = ['WM.Idle']; }), 'asmdef-cycle');
red('runtime reference to an editor assembly is rejected', (files) => changeDefinition(files, 'WM.Idle', (value) => value.references.push('WM.Editor')), 'asmdef-reference');
red('Android-only exclusion is checked separately from Windows', (files) => changeDefinition(files, 'WM.Foundation', (value) => { value.excludePlatforms = ['Android']; }), 'asmdef-reference');
red('excluding the entire game cannot give a false green', (files) => changeDefinition(files, 'WM.Idle', (value) => { value.defineConstraints = ['!WM_IDLE']; }), 'idle-runtime');
red('simultaneous include and exclude lists are invalid', (files) => changeDefinition(files, 'WM.Foundation', (value) => { value.includePlatforms = ['Android']; value.excludePlatforms = ['Editor']; }), 'asmdef-schema');
red('duplicate assembly names are rejected', (files) => changeDefinition(files, 'WM.Foundation', (value) => { value.name = 'WM.Idle'; }), 'asmdef-schema');
red('malformed assembly JSON is a configuration failure', (files) => files.set(definitionPath(files, 'WM.App'), '{'), 'asmdef-schema');
red('missing assembly metadata is rejected', (files) => files.delete(`${definitionPath(files, 'WM.App')}.meta`), 'asmdef-schema');
red('GUID and name reference forms cannot be mixed', (files) => changeDefinition(files, 'WM.Idle', (value) => value.references.push('GUID:00000000000000000000000000000000')), 'asmdef-schema');
red('missing scene is rejected', (files) => files.delete('Assets/_WitchMendokusai/Scenes/Idle/Idle.unity'), 'scene');
red('missing scene metadata is rejected', (files) => files.delete('Assets/_WitchMendokusai/Scenes/Idle/Idle.unity.meta'), 'scene');
red('Unity version and revision must refer to the same version', (files) => files.set('ProjectSettings/ProjectVersion.txt', files.get('ProjectSettings/ProjectVersion.txt').replace(/m_EditorVersion: .+/, 'm_EditorVersion: 6000.0.1f1')), 'unity-version');
red('Android WM_IDLE assignment cannot be removed', (files) => files.set(source, files.get(source).replace('options.extraScriptingDefines = new string[] { IDLE_DEFINE };', '')), 'options-BuildAndroid');
red('Windows launch target cannot silently become Android', (files) => files.set(workflow, files.get(workflow).replace("else { 'Win64' }", "else { 'Android' }")), 'workflow-target');
red('main and Idle builds cannot use separate concurrency groups', (files) => files.set(workflow, files.get(workflow).replace('group: wm-build', 'group: wm-idle')), 'build-serialization');
red('a missing preflight is rejected', (files) => files.set(workflow, files.get(workflow).replace('run: node .github/scripts/wm-idle-config-audit.mjs', 'run: echo skipped')), 'preflight-wiring');
red('preflight cannot ignore failure', (files) => files.set(workflow, files.get(workflow).replace('      - name: Check Idle build configuration', '      - name: Check Idle build configuration\n        continue-on-error: true')), 'preflight-wiring');
red('preflight cannot be conditional', (files) => files.set(workflow, files.get(workflow).replace('      - name: Check Idle build configuration', '      - name: Check Idle build configuration\n        if: false')), 'preflight-wiring');

test('equivalent OR conditions are accepted', () => {
  assert.equal(exitCode(changed((files) => changeDefinition(files, 'WM.App', (value) => { value.defineConstraints = ['!WM_IDLE || UNITY_EDITOR']; }))), 0);
});
test('an unknown active symbol is CANNOT-RUN, not assumed false', () => {
  assert.equal(exitCode(changed((files) => changeDefinition(files, 'WM.App', (value) => { value.defineConstraints = ['FEATURE_UNKNOWN']; }))), 2);
});
test('GUID references resolve to the same internal target', () => {
  assert.equal(exitCode(changed((files) => {
    const path = definitionPath(files, 'WM.Foundation');
    const guid = files.get(`${path}.meta`).match(/^guid:\s*(\w+)/m)[1];
    changeDefinition(files, 'WM.Idle', (value) => { value.references = [`GUID:${guid}`]; });
  })), 0);
});
test('unresolved GUID may be an external package, so cannot pass', () => {
  assert.equal(exitCode(changed((files) => changeDefinition(files, 'WM.Idle', (value) => { value.references = ['GUID:00000000000000000000000000000000']; }))), 2);
});
test('optional omitted arrays are valid JSON defaults', () => {
  assert.equal(exitCode(changed((files) => changeDefinition(files, 'WM.Foundation', (value) => {
    delete value.defineConstraints; delete value.includePlatforms; delete value.excludePlatforms;
  }))), 0);
});
test('conditional builder code is explicitly unverified', () => {
  const checks = changed((files) => files.set(source, `#if UNKNOWN\n${files.get(source)}\n#endif`));
  assert.equal(exitCode(checks), 2);
});
test('the actual CLI rejects a broken checkout and reports the requested root', () => {
  const fixture = mkdtempSync(join(tmpdir(), 'wm-idle-config-'));
  try {
    execFileSync('git', ['init', '--quiet', fixture]);
    for (const [path, text] of baseline) {
      const destination = join(fixture, path); mkdirSync(dirname(destination), { recursive: true }); writeFileSync(destination, text);
    }
    const app = definitionPath(baseline, 'WM.App');
    const value = JSON.parse(baseline.get(app)); value.defineConstraints = [];
    writeFileSync(join(fixture, app), JSON.stringify(value));
    const result = spawnSync(process.execPath, [fileURLToPath(new URL('./wm-idle-config-audit.mjs', import.meta.url)), '--root', fixture, '--json'], { encoding: 'utf8' });
    assert.equal(result.status, 1, result.stderr);
    assert.ok(JSON.parse(result.stdout).checks.some((item) => item.id === 'asmdef-reference' && item.status === 'FAIL'));
  } finally {
    assert.equal(dirname(resolve(fixture)), resolve(tmpdir()));
    assert.ok(fixture.startsWith(join(tmpdir(), 'wm-idle-config-')));
    rmSync(fixture, { recursive: true, force: true });
  }
});
test('quality gate runs both the audit and mutation tests on config changes', () => {
  const yaml = readFileSync(join(root, '.github/workflows/wm-quality-gate.yml'), 'utf8');
  assert.match(yaml, /run: node --test \.github\/scripts\/wm-idle-config-test\.mjs/);
  assert.match(yaml, /run: node \.github\/scripts\/wm-idle-config-audit\.mjs/);
  for (const event of ['push', 'pull_request']) {
    const section = yaml.split(`  ${event}:`)[1].split(/\n  [a-z_]+:/)[0];
    for (const path of ['**/*.asmdef', '**/*.asmdef.meta', 'ProjectSettings/ProjectVersion.txt', workflow,
      '.github/workflows/build.yml', '.github/scripts/wm-idle-config-audit.mjs', '.github/scripts/wm-idle-config-test.mjs']) {
      assert.ok(section.includes(`'${path}'`), `${event} misses ${path}`);
    }
  }
});
