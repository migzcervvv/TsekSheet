if (process.versions.node !== '24.22.1' || !process.env.npm_config_user_agent?.startsWith('npm/11.19.1 ')) {
  console.error('TsekSheet requires Node.js 24.22.1 and npm 11.19.1. See docs/development/toolchain.md.');
  process.exit(1);
}
