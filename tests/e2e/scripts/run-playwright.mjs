import { spawnSync } from 'node:child_process'

const forwardedArgs = process.argv.slice(2)

if (forwardedArgs.includes('--run')) {
  console.log('Skipping Playwright E2E during recursive unit test command. Run `pnpm --dir tests/e2e test` explicitly.')
  process.exit(0)
}

const executable = process.platform === 'win32' ? 'playwright.cmd' : 'playwright'
const result = spawnSync(executable, ['test', ...forwardedArgs], {
  stdio: 'inherit',
  shell: process.platform === 'win32',
})

process.exit(result.status ?? 1)
