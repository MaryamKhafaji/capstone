pluginManagement {
    val flutterSdkPath =
        run {
            val properties = java.util.Properties()
            file("local.properties").inputStream().use { properties.load(it) }
            val flutterSdkPath = properties.getProperty("flutter.sdk")
            require(flutterSdkPath != null) { "flutter.sdk not set in local.properties" }
            flutterSdkPath
        }

    includeBuild("$flutterSdkPath/packages/flutter_tools/gradle")

    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

plugins {
    id("dev.flutter.flutter-plugin-loader") version "1.0.0"
    id("com.android.application") version "9.1.0" apply false
    // START: FlutterFire Configuration
    id("com.google.gms.google-services") version("4.3.15") apply false
    // END: FlutterFire Configuration
    id("org.jetbrains.kotlin.android") version "2.4.0" apply false
}

include(":app")

val unityExport = file(providers.gradleProperty("masarUnityExport").getOrElse("unity-export-updated"))
val unityLibrary = unityExport.resolve("unityLibrary")
require(unityLibrary.resolve("build.gradle").isFile) {
    "Export the masAR Android Gradle project into ${unityExport.absolutePath} before building the app."
}
include(":unityLibrary", ":unityLibrary:xrmanifest.androidlib", ":unityLibrary:FootwearBlePlugin.androidlib")
project(":unityLibrary").projectDir = unityLibrary
project(":unityLibrary:xrmanifest.androidlib").projectDir = unityLibrary.resolve("xrmanifest.androidlib")
project(":unityLibrary:FootwearBlePlugin.androidlib").projectDir = unityLibrary.resolve("FootwearBlePlugin.androidlib")

val unityProperties = java.util.Properties().apply {
    unityExport.resolve("gradle.properties").inputStream().use { load(it) }
}
gradle.beforeProject {
    if (path == ":unityLibrary") {
        for (key in listOf("unityStreamingAssets", "unity.androidNdkPath", "unity.androidSdkPath")) {
            extensions.extraProperties[key] = unityProperties.getProperty(key, "")
        }
    }
}
