import { spawnSync } from 'node:child_process'

const productionApiBaseUrl = 'https://gym.mediaplexserverbadri.trade'
const apiBaseUrl = process.env.VITE_API_BASE_URL?.trim() || productionApiBaseUrl
const commandEnvironment = {
  ...process.env,
  VITE_API_BASE_URL: apiBaseUrl,
}

function run(command, args) {
  const result = spawnSync(command, args, {
    cwd: process.cwd(),
    env: commandEnvironment,
    stdio: 'inherit',
  })

  if (result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}

run(process.execPath, ['scripts/verify-android-build.mjs'])
run(process.platform === 'win32' ? 'npm.cmd' : 'npm', ['run', 'build'])
run(process.execPath, ['scripts/verify-android-build.mjs', '--check-dist'])
run(process.platform === 'win32' ? 'npx.cmd' : 'npx', ['cap', 'sync', 'android'])
