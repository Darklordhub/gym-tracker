import type { CapacitorConfig } from '@capacitor/cli'

const config: CapacitorConfig = {
  appId: 'com.mohamedbadri.stride',
  appName: 'STRIDE',
  webDir: 'dist',
  server: {
    // The packaged WebView stays a secure origin; API requests target VITE_API_BASE_URL.
    androidScheme: 'https',
    hostname: 'app.stride.local',
  },
}

export default config
