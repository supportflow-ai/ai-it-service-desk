import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const compiler = fileURLToPath(new URL('../../src/frontend/service-desk-web/node_modules/typescript/bin/tsc', import.meta.url));
const project = fileURLToPath(new URL('./contracts/tsconfig.json', import.meta.url));

test('auth DTO contract requires passwords and accepts nullable backend fullName', () => {
  const result = spawnSync(process.execPath, [compiler, '-p', project], {
    encoding: 'utf8',
    timeout: 60000,
  });
  assert.equal(result.status, 0, result.stdout + result.stderr + String(result.error ?? ''));
});
