# carbon_tracker_mobile

A Flutter app to help users monitor and reduce their carbon footprint.

## 🚀 Getting Started

This project is the starting point for a Flutter application. If you're new to Flutter, check out:

- [Write your first Flutter app (Codelab)](https://docs.flutter.dev/get-started/codelab)
- [Flutter Cookbook (Sample Recipes)](https://docs.flutter.dev/cookbook)
- [Flutter Documentation](https://docs.flutter.dev/) – includes tutorials, samples, and API references.

---

## 🛠️ Setup Instructions

### 1. Prerequisites

- [Flutter SDK](https://flutter.dev/docs/get-started/install)
- Android Studio or Visual Studio Code with Flutter/Dart plugins
- An Android Virtual Device (AVD) created via Android Studio’s Device Manager

### 2. Add Android Emulator to PATH

Ensure the Android emulator CLI is available by adding this directory to your system `PATH`:

`# carbon_tracker_mobile

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Lab: Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Cookbook: Useful Flutter samples](https://docs.flutter.dev/cookbook)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.



## Steps to get it running

Have `C:\Users\[username]\AppData\Local\Android\Sdk\emulator added in your path.

```powershell
emulator -list-avds
```



```powershell
emulator -avd Pixel_6_API_34_2
```
_Use the name of the phone you created in Android Studio__

This will start the emulator

`C:\Users<your-username>\AppData\Local\Android\Sdk\emulator`

Replace `<your-username>` with your actual Windows username.

Then restart your terminal and test:

```powershell
emulator -list-avds

emulator -avd Pixel_6_API_34_2
```

Once the emulator is running, start your app:

```bash
flutter run
```
_Make sure the backend is already running_


Adding a package when adding directly to pubspec.yaml does not work
https://pub.dev/packages

```bash
flutter pub add fl_chart
```