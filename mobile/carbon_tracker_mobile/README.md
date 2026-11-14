# Carbon Tracker mobile

A Flutter app for viewing your carbon footprint items.

## Prerequisites

- Install the [Flutter SDK](https://docs.flutter.dev/get-started/install) and Android development tools.
- Create an Android Virtual Device in Android Studio, or connect an Android device.
- Set up and start the API and create an account. See the [backend setup and client connection guide](../../backend/CarbonTracker.API/README.md#connecting-clients-in-development) and the [authentication setup guide](../../README.md#authentication-setup-and-testing-issue-16).

## Run on an Android emulator

From this directory, install dependencies and start the app:

```powershell
flutter pub get
flutter devices
flutter run
```

Start an emulator first using Android Studio's Device Manager. Alternatively, add the Android SDK's `emulator` directory to your PATH and run:

```powershell
emulator -list-avds
emulator -avd <avd-name>
```

The default API address is `http://10.0.2.2:7285`, which maps from the Android emulator to the host. For a physical device or another host, set `API_BASE_URL` as described in the backend connection guide. Local HTTP is enabled for Android debug builds; use HTTPS for deployed clients.

Sign in with an existing username or email and password to load items. The session is kept in memory, so sign in again after restarting the app. The app automatically refreshes an expired access token while the session remains valid.

## Test

```powershell
flutter test
```

## Configuration and troubleshooting

The API address is configured once with `--dart-define=API_BASE_URL=...` (no
trailing slash). The default is `http://10.0.2.2:7285` for an Android emulator.
For example, with the API running under its `http` launch profile:

```powershell
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:7285
```

For a physical device, bind the API to a reachable network interface as described
in the backend connection guide, and replace `10.0.2.2` with the host's LAN IP:

```powershell
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:7285
```

The phone and host must be on the same network and the host firewall must allow
the port. Android permits cleartext HTTP in debug builds only. Desktop clients
can use `http://localhost:7285`; web clients also require the API's development
CORS policy to allow the web origin. HTTPS requires a certificate trusted by the
actual device or browser and valid for the hostname used; a host's local
ASP.NET development certificate is not automatically trusted by an emulator or
phone. Do not disable certificate verification. Use HTTPS for deployed builds.

If sign-in cannot reach the API, check the configured address, server binding,
firewall and certificate trust. The app keeps credentials only in memory. The app refreshes an expired access token automatically. If the refresh token
expires or is revoked, sign in again. The total shown is the sum of
your recorded item footprints, not an estimated or amortised footprint.

Run `flutter analyze` and `flutter test` before submitting changes.
