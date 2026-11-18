# MASAR+ Flutter App

A high-performance walking/navigation app built with Flutter & Dart.
Supports Android and iOS with seamless Light/Dark theme switching.

## Project Structure

```
masar_plus/
├── lib/
│   ├── main.dart                    ← App entry point
│   ├── theme/
│   │   ├── app_theme.dart           ← Light + Dark ThemeData & MasarColors extension
│   │   └── theme_provider.dart      ← ChangeNotifier, persists theme to SharedPreferences
│   ├── widgets/
│   │   └── shared_widgets.dart      ← GradientButton, StatCard, GlassCard, PulseDot, etc.
│   └── screens/
│       ├── login_screen.dart        ← Login / splash
│       ├── main_shell.dart          ← Bottom nav shell + MasarAppBar
│       ├── home_screen.dart         ← Dashboard
│       ├── walk_session_screen.dart ← Post-walk summary
│       ├── history_screen.dart      ← Activity history
│       ├── quests_screen.dart       ← Quests list
│       ├── leaderboard_screen.dart  ← Podium + rankings
│       ├── profile_screen.dart      ← User profile
│       └── settings_screen.dart     ← Theme toggle + app settings
└── pubspec.yaml
```

## Setup & Run

### Prerequisites
- Flutter SDK ≥ 3.0.0 (https://docs.flutter.dev/get-started/install)
- Android Studio or Xcode

### Steps

```bash
# 1. Navigate to project
cd masar_plus

# 2. Get dependencies
flutter pub get

# 3a. Run on Android
flutter run

# 3b. Run on iOS (Mac only)
flutter run -d ios

# 4. Build release APK (Android)
flutter build apk --release

# 5. Build release IPA (iOS — requires Apple dev account)
flutter build ios --release
```

## Theme System

The app uses a custom `MasarColors` ThemeExtension that adds
surface layers, gradient colors and button text colors on top of
Flutter's standard `ColorScheme`. Both themes are in `app_theme.dart`.

Toggle in the app: **Profile → Settings → System Appearance**
Or use the quick Switch toggle in Settings.

Theme is persisted across app restarts via `shared_preferences`.

## Dependencies

| Package | Use |
|---|---|
| `google_fonts` | Space Grotesk (headlines) + Manrope (body) |
| `provider` | Theme state management |
| `shared_preferences` | Persist dark/light choice |
| `percent_indicator` | Progress indicators |
| `fl_chart` | Charts (ready for stats screen) |

## Android AR game

MasAR+ includes an Android Gradle export of the `masAR` Unity project. The **Play AR**
button opens it inside the same installed app. Flutter and NRSDK use separate Android
processes so Unity can shut down without closing the Flutter screens.

**Settings → Close-range AR testing** uses the existing nearby spawns without
GPS. With the toggle off, Android uses precise GPS during the hunt. The first and later
hunts randomly pick an eligible monster and then pick one of its three nearest sites
within the search range. At the selected site, the existing AR hunt starts after the
player accepts. Both monsters currently use the supplied pin at Sabah Al Salem Walkway.
Edit `android/app/src/main/assets/hunt_sites.json` to add sites, with a monster ID
(`bu_daryah` or `om_el_saaf`), name, latitude, longitude, and arrival radius in meters.

### Rebuild after editing Unity

With the Unity project closed, run the following from the Flutter project folder.
Unity 6000.0.84f1 needs Android Build Support, including the SDK and NDK modules.
Use your installed editor path if it differs.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe'
$project = (Resolve-Path '..\unity').Path
$export = Join-Path (Get-Location) 'android\unity-export-updated'
$log = Join-Path (Get-Location) 'unity-export.log'
$argsForUnity = "-batchmode -nographics -quit -buildTarget Android -projectPath `"$project`" -executeMethod MasarAndroidExport.ExportFromCommandLine -masarExportPath `"$export`" -logFile `"$log`""
$build = Start-Process -FilePath $unity -ArgumentList $argsForUnity -Wait -PassThru -WindowStyle Hidden
if ($build.ExitCode -ne 0) { throw "Unity export failed. See $log" }
flutter build apk --debug --target-platform android-arm64
```

Install `build/app/outputs/flutter-apk/app-debug.apk` on the Android phone. Connect
the XREAL glasses, then test both modes from **Play AR** and use the in-game return
button to check hunt history and reward sync.

