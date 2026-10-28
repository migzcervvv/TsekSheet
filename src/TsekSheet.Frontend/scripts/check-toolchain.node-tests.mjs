import { strict as assert } from 'node:assert';
import { test } from 'node:test';
import { supportedToolchain } from './check-toolchain.mjs';

test('accepts the current PC and previous PC toolchains', () => {
  assert.equal(supportedToolchain('24.16.0', 'npm/11.13.0 node/v24.16.0 win32 x64'), true);
  assert.equal(supportedToolchain('24.22.1', 'npm/11.19.1 node/v24.22.1 win32 x64'), true);
});

test('rejects older and unreviewed major versions', () => {
  for (const version of ['22.22.2', '24.15.9', '25.0.0', '26.0.0'])
    assert.equal(supportedToolchain(version, 'npm/11.13.0'), false, version);
  for (const version of ['10.9.8', '11.12.9', '12.0.0'])
    assert.equal(supportedToolchain('24.16.0', `npm/${version}`), false, version);
});

test('rejects missing, malformed, preview and alternate package-manager identities', () => {
  for (const version of [undefined, '', 'v24.16.0', '24.16.0-rc.1', '24.16'])
    assert.equal(supportedToolchain(version, 'npm/11.13.0'), false);
  for (const agent of [undefined, '', 'yarn/11.13.0', 'pnpm/11.13.0', 'npm/11.13.0-beta.1', 'npm/11.13'])
    assert.equal(supportedToolchain('24.16.0', agent), false);
});
