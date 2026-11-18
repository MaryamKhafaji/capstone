# MasAR+ Capstone

This repository contains the current app and admin website.

```text
app/
  flutter/       Flutter app, Android bridge and Firebase configuration
  unity/         Unity game source and project settings
website/         Admin and sponsor website and Netlify functions
```

## App

The Android app embeds the Unity game. First export `app/unity` as an Android Gradle project into `app/flutter/android/unity-export-updated`, then build the Flutter APK. The Unity project uses **Unity 6000.0.84f1** with Android Build Support, SDK and NDK. See [the app README](app/flutter/README.md) for commands and additional details.

The generated Unity export, Flutter build output, Android local SDK configuration, and dependencies are intentionally not tracked. Each developer must regenerate them locally. The `lib` folder contains the Flutter app's Dart source and is tracked.

## Website

From `website`, run `npm install` and then `npm run dev`. Open the local URL printed by Netlify Dev. Some backend functions require server-side environment variables configured by the project administrator; do not commit credentials or `.env` files. See [the website README](website/README.md).

## Firebase

Firebase project configuration and the current app Firestore rules are in `app/flutter`. The website also contains an older `firebase/firestore.rules` file; do not deploy it over the live rules without reviewing the differences. Rules affect shared project data and should be tested before deployment.
