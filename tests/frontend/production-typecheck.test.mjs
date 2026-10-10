import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const frontend = fileURLToPath(new URL('../../src/frontend/service-desk-web/', import.meta.url));
const compiler = fileURLToPath(new URL('../../src/frontend/service-desk-web/node_modules/typescript/bin/tsc', import.meta.url));

test('production frontend compiles with its strict application and dependency types', () => {
  const result = spawnSync(process.execPath, [compiler, '-b', '--force'], {
    cwd: frontend,
    encoding: 'utf8',
    timeout: 60000,
  });
  assert.equal(result.status, 0, result.stdout + result.stderr + String(result.error ?? ''));
});
