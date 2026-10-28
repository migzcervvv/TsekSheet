import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

export function supportedToolchain(nodeVersion, npmUserAgent) {
  const node = /^(\d+)\.(\d+)\.(\d+)$/.exec(nodeVersion ?? '');
  const npm = /^npm\/(\d+)\.(\d+)\.(\d+)(?: |$)/.exec(npmUserAgent ?? '');
  return node !== null && Number(node[1]) === 24 && Number(node[2]) >= 16
    && npm !== null && Number(npm[1]) === 11 && Number(npm[2]) >= 13;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)
  && !supportedToolchain(process.versions.node, process.env.npm_config_user_agent)) {
  console.error('TsekSheet requires stable Node.js >=24.16.0 <25 and npm >=11.13.0 <12. See docs/development/toolchain.md.');
  process.exitCode = 1;
}
