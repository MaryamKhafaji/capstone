import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:google_fonts/google_fonts.dart';

/// MasAR+ premium palette. Flat colors only; no background images or patterns.
class AppColors {
  static const lightPrimary = Color(0xFF602638);
  static const lightPrimaryContainer = Color(0xFF602638);
  static const lightOnPrimary = Color(0xFFFFFCF8);
  static const lightOnPrimaryContainer = Color(0xFFFFFCF8);
  static const lightSecondary = Color(0xFF776D66);
  static const lightTertiary = Color(0xFF99744F);
  static const lightTertiaryContainer = Color(0xFFEADCCB);
  static const lightSurface = Color(0xFFF6F0E8);
  static const lightSurfaceLow = Color(0xFFF3EAE0);
  static const lightSurfaceContainer = Color(0xFFF0E6DB);
  static const lightSurfaceHigh = Color(0xFFFFFCF8);
  static const lightSurfaceHighest = Color(0xFFEDE2D6);
  static const lightOnSurface = Color(0xFF292321);
  static const lightOnSurfaceVariant = Color(0xFF776D66);
  static const lightOutline = Color(0xFF9A8B80);
  static const lightOutlineVariant = Color(0xFFE7DBCF);
  static const lightError = Color(0xFFB3261E);
  static const lightErrorContainer = Color(0xFFF9DEDC);

  // Legacy dark names are retained for compatibility with any other files.
  // The chosen MasAR+ visual identity is intentionally beige in both modes.
  static const darkPrimary = lightPrimary;
  static const darkPrimaryContainer = lightPrimaryContainer;
  static const darkOnPrimary = lightOnPrimary;
  static const darkOnPrimaryContainer = lightOnPrimaryContainer;
  static const darkSecondary = lightSecondary;
  static const darkTertiary = lightTertiary;
  static const darkTertiaryContainer = lightTertiaryContainer;
  static const darkSurface = lightSurface;
  static const darkSurfaceLow = lightSurfaceLow;
  static const darkSurfaceContainer = lightSurfaceContainer;
  static const darkSurfaceHigh = lightSurfaceHigh;
  static const darkSurfaceHighest = lightSurfaceHighest;
  static const darkOnSurface = lightOnSurface;
  static const darkOnSurfaceVariant = lightOnSurfaceVariant;
  static const darkOutline = lightOutline;
  static const darkOutlineVariant = lightOutlineVariant;
  static const darkError = lightError;
  static const darkErrorContainer = lightErrorContainer;
}

class AppTheme {
  static TextTheme _textTheme(Color primary, Color secondary) {
    return GoogleFonts.manropeTextTheme().copyWith(
      displayLarge: GoogleFonts.manrope(fontSize: 48, fontWeight: FontWeight.w700, color: primary),
      displayMedium: GoogleFonts.manrope(fontSize: 40, fontWeight: FontWeight.w700, color: primary),
      displaySmall: GoogleFonts.manrope(fontSize: 34, fontWeight: FontWeight.w700, color: primary),
      headlineLarge: GoogleFonts.manrope(fontSize: 30, fontWeight: FontWeight.w700, color: primary),
      headlineMedium: GoogleFonts.manrope(fontSize: 26, fontWeight: FontWeight.w700, color: primary),
      headlineSmall: GoogleFonts.manrope(fontSize: 22, fontWeight: FontWeight.w700, color: primary),
      titleLarge: GoogleFonts.manrope(fontSize: 20, fontWeight: FontWeight.w700, color: primary),
      titleMedium: GoogleFonts.manrope(fontSize: 16, fontWeight: FontWeight.w600, color: primary),
      titleSmall: GoogleFonts.manrope(fontSize: 14, fontWeight: FontWeight.w600, color: primary),
      bodyLarge: GoogleFonts.manrope(fontSize: 16, color: primary),
      bodyMedium: GoogleFonts.manrope(fontSize: 14, color: secondary),
      bodySmall: GoogleFonts.manrope(fontSize: 12, color: secondary),
      labelLarge: GoogleFonts.manrope(fontSize: 13, fontWeight: FontWeight.w600, color: primary),
      labelMedium: GoogleFonts.manrope(fontSize: 11, fontWeight: FontWeight.w600, color: secondary),
      labelSmall: GoogleFonts.manrope(fontSize: 10, fontWeight: FontWeight.w600, color: secondary),
    );
  }

  static ThemeData light() {
    const cs = ColorScheme(
      brightness: Brightness.light,
      primary: AppColors.lightPrimary,
      onPrimary: AppColors.lightOnPrimary,
      primaryContainer: AppColors.lightPrimaryContainer,
      onPrimaryContainer: AppColors.lightOnPrimaryContainer,
      secondary: AppColors.lightSecondary,
      onSecondary: AppColors.lightOnPrimary,
      secondaryContainer: AppColors.lightSurfaceContainer,
      onSecondaryContainer: AppColors.lightOnSurface,
      tertiary: AppColors.lightTertiary,
      onTertiary: AppColors.lightOnPrimary,
      tertiaryContainer: AppColors.lightTertiaryContainer,
      onTertiaryContainer: AppColors.lightOnSurface,
      error: AppColors.lightError,
      onError: AppColors.lightOnPrimary,
      errorContainer: AppColors.lightErrorContainer,
      onErrorContainer: AppColors.lightError,
      surface: AppColors.lightSurface,
      onSurface: AppColors.lightOnSurface,
      onSurfaceVariant: AppColors.lightOnSurfaceVariant,
      outline: AppColors.lightOutline,
      outlineVariant: AppColors.lightOutlineVariant,
      shadow: Colors.black,
      scrim: Colors.black,
      inverseSurface: AppColors.lightOnSurface,
      onInverseSurface: AppColors.lightSurface,
      inversePrimary: AppColors.lightPrimary,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: cs,
      scaffoldBackgroundColor: AppColors.lightSurface,
      textTheme: _textTheme(AppColors.lightOnSurface, AppColors.lightOnSurfaceVariant),
      appBarTheme: AppBarTheme(
        backgroundColor: AppColors.lightSurface,
        foregroundColor: AppColors.lightPrimary,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        centerTitle: false,
        systemOverlayStyle: SystemUiOverlayStyle.dark,
        titleTextStyle: GoogleFonts.manrope(
          fontSize: 20, fontWeight: FontWeight.w700,
          color: AppColors.lightPrimary, letterSpacing: 1.0,
        ),
        iconTheme: const IconThemeData(color: AppColors.lightPrimary),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: AppColors.lightSurfaceHigh,
        indicatorColor: AppColors.lightPrimary,
        labelTextStyle: WidgetStateProperty.resolveWith((states) => GoogleFonts.manrope(
          fontSize: 10, fontWeight: FontWeight.w600,
          color: states.contains(WidgetState.selected) ? AppColors.lightPrimary : AppColors.lightSecondary,
        )),
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
          color: states.contains(WidgetState.selected) ? AppColors.lightOnPrimary : AppColors.lightSecondary,
        )),
        elevation: 0,
      ),
      cardTheme: CardThemeData(
        color: AppColors.lightSurfaceHigh,
        elevation: 0,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(18),
          side: const BorderSide(color: AppColors.lightOutlineVariant, width: 0.7),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: AppColors.lightSurfaceHigh,
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: const BorderSide(color: AppColors.lightOutlineVariant)),
        enabledBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: const BorderSide(color: AppColors.lightOutlineVariant)),
        focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(14), borderSide: const BorderSide(color: AppColors.lightPrimary, width: 1.4)),
        hintStyle: GoogleFonts.manrope(color: AppColors.lightSecondary),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.lightPrimary,
          foregroundColor: AppColors.lightOnPrimary,
          disabledBackgroundColor: AppColors.lightSurfaceContainer,
          disabledForegroundColor: AppColors.lightSecondary,
          elevation: 0,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 14),
          textStyle: GoogleFonts.manrope(fontSize: 14, fontWeight: FontWeight.w700),
        ),
      ),
      extensions: const [
        MasarColors(
          surfaceLow: AppColors.lightSurfaceLow,
          surfaceContainer: AppColors.lightSurfaceContainer,
          surfaceHigh: AppColors.lightSurfaceHigh,
          surfaceHighest: AppColors.lightSurfaceHighest,
          tertiaryContainer: AppColors.lightTertiaryContainer,
          gradientStart: AppColors.lightPrimary,
          gradientEnd: AppColors.lightPrimary,
          btnText: AppColors.lightOnPrimary,
        ),
      ],
    );
  }

  // Keep the app beige regardless of the phone's system dark-mode setting.
  static ThemeData dark() => light();
}

/// Retains the API used throughout the existing app.
class MasarColors extends ThemeExtension<MasarColors> {
  final Color surfaceLow;
  final Color surfaceContainer;
  final Color surfaceHigh;
  final Color surfaceHighest;
  final Color tertiaryContainer;
  final Color gradientStart;
  final Color gradientEnd;
  final Color btnText;

  const MasarColors({
    required this.surfaceLow,
    required this.surfaceContainer,
    required this.surfaceHigh,
    required this.surfaceHighest,
    required this.tertiaryContainer,
    required this.gradientStart,
    required this.gradientEnd,
    required this.btnText,
  });

  // Existing gradient references now render a single flat burgundy color.
  LinearGradient get primaryGradient => LinearGradient(
    colors: [gradientStart, gradientEnd],
  );

  @override
  MasarColors copyWith({
    Color? surfaceLow, Color? surfaceContainer, Color? surfaceHigh,
    Color? surfaceHighest, Color? tertiaryContainer,
    Color? gradientStart, Color? gradientEnd, Color? btnText,
  }) => MasarColors(
    surfaceLow: surfaceLow ?? this.surfaceLow,
    surfaceContainer: surfaceContainer ?? this.surfaceContainer,
    surfaceHigh: surfaceHigh ?? this.surfaceHigh,
    surfaceHighest: surfaceHighest ?? this.surfaceHighest,
    tertiaryContainer: tertiaryContainer ?? this.tertiaryContainer,
    gradientStart: gradientStart ?? this.gradientStart,
    gradientEnd: gradientEnd ?? this.gradientEnd,
    btnText: btnText ?? this.btnText,
  );

  @override
  MasarColors lerp(MasarColors? other, double t) {
    if (other == null) return this;
    return MasarColors(
      surfaceLow: Color.lerp(surfaceLow, other.surfaceLow, t)!,
      surfaceContainer: Color.lerp(surfaceContainer, other.surfaceContainer, t)!,
      surfaceHigh: Color.lerp(surfaceHigh, other.surfaceHigh, t)!,
      surfaceHighest: Color.lerp(surfaceHighest, other.surfaceHighest, t)!,
      tertiaryContainer: Color.lerp(tertiaryContainer, other.tertiaryContainer, t)!,
      gradientStart: Color.lerp(gradientStart, other.gradientStart, t)!,
      gradientEnd: Color.lerp(gradientEnd, other.gradientEnd, t)!,
      btnText: Color.lerp(btnText, other.btnText, t)!,
    );
  }
}
