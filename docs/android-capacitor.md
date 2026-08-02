# Android Capacitor Runbook

STRIDE's Android app packages the existing Vite frontend in a Capacitor WebView. The ASP.NET Core API remains the source of truth; no application logic, credentials, or provider keys are included in the Android project.

## Prerequisites

- Node.js 20 or later and npm.
- Android Studio with the Android SDK, platform tools, and an installed Android platform matching the generated project.
- A supported JDK. Use Android Studio's bundled JDK unless a project-specific JDK is required by Gradle.
- A public HTTPS deployment of STRIDE. The Android bundle cannot use `localhost`, a LAN IP, or cleartext HTTP.

## Initial setup

Run the commands from `frontend`:

```bash
npm ci
npx cap sync android
```

The generated native project is tracked at `frontend/android`. Open it in Android Studio with:

```bash
npm run cap:open:android
```

Do not edit `android/app/src/main/assets/public` directly. Capacitor regenerates it from `frontend/dist` during every sync.

## Building the Android bundle

The API base URL is compiled into the frontend bundle. It is a public URL, not a secret. `npm run build:android` defaults to STRIDE's production API origin, `https://gym.mediaplexserverbadri.trade`. The frontend adds `/api` exactly once, so application calls resolve as `https://gym.mediaplexserverbadri.trade/api/...`.

```bash
npm run build:android
```

For a different staging or production deployment, pass its public HTTPS application origin without a trailing `/api` requirement:

```bash
VITE_API_BASE_URL=https://gym.example.com npm run build:android
```

`build:android` rejects HTTP, localhost, `.local`, and private IPv4 API URLs before compiling. It verifies that the compiled bundle contains the configured public origin, then synchronizes it to Android.

For a debug APK on a machine with the Android SDK and JDK:

```bash
npm run android:debug
```

The generated debug APK is normally at `frontend/android/app/build/outputs/apk/debug/app-debug.apk`.

For an emulator or a connected device, open Android Studio, choose the `android` project, then run the `app` configuration. Re-run `npm run build:android` after frontend changes before using Android Studio's Run command.

## Production API and CORS

The packaged WebView uses the fixed secure origin `https://app.stride.local` and calls the public API URL supplied as `VITE_API_BASE_URL`. It does not use the Vite development proxy.

The API must allow that origin in CORS. The supplied Compose stack sets `Cors__AllowedOrigins__1` from `CAPACITOR_APP_ORIGIN`, which defaults to `https://app.stride.local`. Keep that value aligned with `frontend/capacitor.config.ts` when deploying a different configuration.

Example production deployment values:

```dotenv
APP_BASE_URL=https://gym.example.com
CAPACITOR_APP_ORIGIN=https://app.stride.local
```

`VITE_API_BASE_URL` is only needed when overriding the Android build default. Do not add `OPENAI_API_KEY`, JWT signing keys, database passwords, or other backend secrets to frontend environment files or Android resources.

Android cleartext traffic is disabled in the generated app. Use a trusted HTTPS certificate for the public STRIDE domain; HTTP API endpoints and LAN development servers are intentionally unsupported by the Android build command.

## Login troubleshooting

If login fails only in the Android app, rebuild and reinstall the APK with `npm run android:debug`. The packaged WebView uses `https://app.stride.local` for its internal application files, so relative `/api` requests would target the wrong origin. The Android build embeds the public API origin instead; verify it before sync with:

```bash
VITE_API_BASE_URL=https://gym.mediaplexserverbadri.trade node scripts/verify-android-build.mjs --check-dist
grep -R "gym.mediaplexserverbadri.trade" dist/assets dist/index.html
```

The backend must also allow `https://app.stride.local` through CORS, as described above.

## Sync and maintenance commands

```bash
npm run lint
npm run build
npm run cap:copy -- android
npm run cap:sync -- android
npm run cap:open:android
```

Use `cap copy` after frontend-only changes. Use `cap sync` after adding or changing Capacitor plugins, as well as after frontend builds.

## Release signing and Play Store

1. Create and protect a release keystore outside the repository.
2. Configure Gradle signing through local, secret-managed properties or CI secrets. Never commit a keystore, passwords, or signing configuration containing credentials.
3. Build a signed Android App Bundle (`.aab`) from Android Studio or Gradle.
4. Upload the `.aab` to Play Console testing first, then complete the required privacy, data-safety, content-rating, and store-listing declarations.
5. Verify login, authenticated API requests, public media, and logout against the production HTTPS deployment on a physical device.

The generated Capacitor launcher icon is a development placeholder. Replace it with final STRIDE Android adaptive icon and splash assets before Play Store submission; no suitable raster launcher assets currently exist in this repository.

## Current Play Store and native gaps

- No runtime permissions beyond Capacitor's standard network access are added.
- No service worker is added, so authenticated API, Admin, and private draft-media responses are not cached by the wrapper.
- Health Connect is intentionally not included. Add it later as a separate permission-reviewed Capacitor plugin integration.
- Complete production privacy policy, account deletion, target SDK, app signing, and Play Console requirements before release.
