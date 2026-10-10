import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import test from 'node:test';
import { getHttpErrorStatus } from '../../src/frontend/service-desk-web/src/shared/api/httpError.ts';

const requireFrontend = createRequire(new URL('../../src/frontend/service-desk-web/package.json', import.meta.url));
const { AxiosError } = requireFrontend('axios');

for (const status of [401, 404, 409]) {
  test(`preserves Axios HTTP status ${status} for existing UI decisions`, () => {
    const response = { status, statusText: '', headers: {}, config: {}, data: {} };
    assert.equal(getHttpErrorStatus(new AxiosError('synthetic', 'E_TEST', {}, undefined, response)), status);
  });
}
test('network Axios errors have no response status', () => {
  assert.equal(getHttpErrorStatus(new AxiosError('synthetic network', 'ERR_NETWORK')), undefined);
});
test('ordinary application errors are not treated as HTTP errors', () => {
  assert.equal(getHttpErrorStatus(new Error('synthetic')), undefined);
});
test('untrusted response-shaped objects do not select an Axios status branch', () => {
  assert.equal(getHttpErrorStatus({ response: { status: 409 } }), undefined);
});
test('null, undefined and primitive caught values are safe', () => {
  for (const value of [null, undefined, 'synthetic', 409]) {
    assert.equal(getHttpErrorStatus(value), undefined);
  }
});
