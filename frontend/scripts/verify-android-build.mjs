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
