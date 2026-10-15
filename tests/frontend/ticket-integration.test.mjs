import assert from 'node:assert/strict';
import fs from 'node:fs';
import { createRequire } from 'node:module';
import vm from 'node:vm';
import test from 'node:test';

const frontend = new URL('../../src/frontend/service-desk-web/', import.meta.url);
const requireFrontend = createRequire(new URL('package.json', frontend));
const ts = requireFrontend('typescript');
function loadModule(relative, apiClient) {
  const source = fs.readFileSync(new URL(relative, frontend), 'utf8');
  const { outputText } = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } });
  const exports = {};
  vm.runInNewContext(outputText, {
    exports,
    require: name => {
      assert.equal(name, '@/shared/api/apiClient');
      return { default: apiClient };
    },
  });
  return exports;
}

test('frontend status values match every persisted backend ticket state', () => {
  const source = fs.readFileSync(new URL('../../src/backend/ServiceDesk.Domain/Ticketing/TicketStatus.cs', import.meta.url), 'utf8');
  const backend = Object.fromEntries([...source.matchAll(/\b(\w+)\s*=\s*(\d+)/g)].map(([, name, number]) => [name, Number(number)]));
  const { TicketStatus } = loadModule('src/features/tickets/types.ts');
  const frontendStates = Object.fromEntries(Object.entries(TicketStatus).filter(([, value]) => typeof value === 'number'));
  assert.deepEqual(frontendStates, backend);
  assert.equal(TicketStatus.Resolved, 4);
  assert.equal(TicketStatus.Cancelled, 6);
});

test('requester client sends server paging and preserves paged metadata', async () => {
  const result = { items: [{ id: 'owned-ticket', status: 4 }], page: 2, pageSize: 5, totalCount: 12 };
  const calls = [];
  const api = { get: async (url, config) => { calls.push({ url, config }); return { data: result }; } };
  const { ticketApi } = loadModule('src/features/tickets/api/ticketApi.ts', api);
  assert.equal(await ticketApi.getMyTickets(2, 5), result);
  assert.equal(calls[0].url, '/tickets/me');
  assert.deepEqual(JSON.parse(JSON.stringify(calls[0].config)), { params: { page: 2, pageSize: 5 } });
});
