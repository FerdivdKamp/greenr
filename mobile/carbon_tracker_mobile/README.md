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

Sign in with an existing username or email and password to load items. The session is kept in memory, so sign in again after restarting the app. If the session expires, sign out and sign in again.

## Test

```powershell
flutter test
```
