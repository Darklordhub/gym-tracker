import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'

const apiBaseUrl = process.env.VITE_API_BASE_URL?.trim()

if (!apiBaseUrl) {
  console.error('VITE_API_BASE_URL must be set to the public HTTPS API URL before building Android.')
  process.exit(1)
}

let parsedApiBaseUrl

try {
  parsedApiBaseUrl = new URL(apiBaseUrl)
} catch {
  console.error('VITE_API_BASE_URL must be an absolute HTTPS URL.')
  process.exit(1)
}

const privateIpv4Pattern = /^(127\.|10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)/
const unsafeHost = parsedApiBaseUrl.hostname === 'localhost'
  || parsedApiBaseUrl.hostname.endsWith('.local')
  || privateIpv4Pattern.test(parsedApiBaseUrl.hostname)
  || parsedApiBaseUrl.hostname === '::1'

if (parsedApiBaseUrl.protocol !== 'https:' || unsafeHost) {
  console.error('VITE_API_BASE_URL must use a public HTTPS domain, not localhost or a private network address.')
  process.exit(1)
}

if (!process.argv.includes('--check-dist')) {
  process.exit(0)
}

const expectedOrigin = parsedApiBaseUrl.origin
const distDirectory = join(process.cwd(), 'dist')
const assetsDirectory = join(distDirectory, 'assets')

if (!existsSync(assetsDirectory)) {
  console.error('Build output is missing. Run the Vite build before verifying the Android bundle.')
  process.exit(1)
}

const builtFiles = readdirSync(assetsDirectory)
  .filter((fileName) => fileName.endsWith('.js'))

const compiledOriginExists = builtFiles.some((fileName) =>
  readFileSync(join(assetsDirectory, fileName), 'utf8').includes(expectedOrigin),
)

if (!compiledOriginExists) {
  console.error('The built Android bundle does not contain the configured VITE_API_BASE_URL origin.')
  process.exit(1)
}
