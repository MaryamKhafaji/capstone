import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:google_fonts/google_fonts.dart';

class AppColors {
  // ─── Dark Mode ───────────────────────────────────────────────────────────────
  static const darkPrimary = Color(0xFFB6DFFF);
  static const darkPrimaryContainer = Color(0xFF77C6FC);
  static const darkOnPrimary = Color(0xFF00344D);
  static const darkOnPrimaryContainer = Color(0xFF005277);
  static const darkSecondary = Color(0xFFB0CBCF);
  static const darkTertiary = Color(0xFFFFD29C);
  static const darkTertiaryContainer = Color(0xFFF6B051);
  static const darkSurface = Color(0xFF101126);
  static const darkSurfaceLow = Color(0xFF191A2F);
  static const darkSurfaceContainer = Color(0xFF1D1E33);
  static const darkSurfaceHigh = Color(0xFF27283E);
  static const darkSurfaceHighest = Color(0xFF323349);
  static const darkOnSurface = Color(0xFFE1E0FD);
  static const darkOnSurfaceVariant = Color(0xFFBFC7D0);
  static const darkOutline = Color(0xFF89929A);
  static const darkOutlineVariant = Color(0xFF40484F);
  static const darkError = Color(0xFFFFB4AB);
  static const darkErrorContainer = Color(0xFF93000A);

  // ─── Light Mode ──────────────────────────────────────────────────────────────
  static const lightPrimary = Color(0xFF006491);
  static const lightPrimaryContainer = Color(0xFF77C6FC);
  static const lightOnPrimary = Color(0xFFFFFFFF);
  static const lightOnPrimaryContainer = Color(0xFF005277);
  static const lightSecondary = Color(0xFF5C5C75);
  static const lightTertiary = Color(0xFF006A62);
  static const lightTertiaryContainer = Color(0xFF2CD4C4);
  static const lightSurface = Color(0xFFEBFDFF);
  static const lightSurfaceLow = Color(0xFFDDF9FD);
  static const lightSurfaceContainer = Color(0xFFD7F3F7);
  static const lightSurfaceHigh = Color(0xFFD1EDF1);
  static const lightSurfaceHighest = Color(0xFFCCE8EC);
  static const lightOnSurface = Color(0xFF041F22);
  static const lightOnSurfaceVariant = Color(0xFF40484F);
  static const lightOutline = Color(0xFF707880);
  static const lightOutlineVariant = Color(0xFFBFC7D0);
  static const lightError = Color(0xFFBA1A1A);
  static const lightErrorContainer = Color(0xFFFFDAD6);
}

class AppTheme {
  static TextTheme _textTheme(Color primary, Color secondary) {
    return TextTheme(
      displayLarge: GoogleFonts.spaceGrotesk(
        fontSize: 57, fontWeight: FontWeight.w800, color: primary,
      ),
      displayMedium: GoogleFonts.spaceGrotesk(
        fontSize: 45, fontWeight: FontWeight.w700, color: primary,
      ),
      displaySmall: GoogleFonts.spaceGrotesk(
        fontSize: 36, fontWeight: FontWeight.w700, color: primary,
      ),
      headlineLarge: GoogleFonts.spaceGrotesk(
        fontSize: 32, fontWeight: FontWeight.w800, color: primary,
      ),
      headlineMedium: GoogleFonts.spaceGrotesk(
        fontSize: 26, fontWeight: FontWeight.w700, color: primary,
      ),
      headlineSmall: GoogleFonts.spaceGrotesk(
        fontSize: 22, fontWeight: FontWeight.w700, color: primary,
      ),
      titleLarge: GoogleFonts.spaceGrotesk(
        fontSize: 20, fontWeight: FontWeight.w700, color: primary,
      ),
      titleMedium: GoogleFonts.spaceGrotesk(
        fontSize: 16, fontWeight: FontWeight.w600, color: primary,
      ),
      titleSmall: GoogleFonts.spaceGrotesk(
        fontSize: 14, fontWeight: FontWeight.w600, color: primary,
      ),
      bodyLarge: GoogleFonts.manrope(
        fontSize: 16, fontWeight: FontWeight.w400, color: primary,
      ),
      bodyMedium: GoogleFonts.manrope(
        fontSize: 14, fontWeight: FontWeight.w400, color: secondary,
      ),
      bodySmall: GoogleFonts.manrope(
        fontSize: 12, fontWeight: FontWeight.w400, color: secondary,
      ),
      labelLarge: GoogleFonts.manrope(
        fontSize: 11, fontWeight: FontWeight.w700, color: secondary,
        letterSpacing: 0.8,
      ),
      labelMedium: GoogleFonts.manrope(
        fontSize: 10, fontWeight: FontWeight.w700, color: secondary,
        letterSpacing: 0.8,
      ),
      labelSmall: GoogleFonts.manrope(
        fontSize: 9, fontWeight: FontWeight.w700, color: secondary,
        letterSpacing: 1.0,
      ),
    );
  }

  static ThemeData dark() {
    const cs = ColorScheme(
      brightness: Brightness.dark,
      primary: AppColors.darkPrimary,
      onPrimary: AppColors.darkOnPrimary,
      primaryContainer: AppColors.darkPrimaryContainer,
      onPrimaryContainer: AppColors.darkOnPrimaryContainer,
      secondary: AppColors.darkSecondary,
      onSecondary: AppColors.darkSurface,
      secondaryContainer: Color(0xFF344D51),
      onSecondaryContainer: AppColors.darkSecondary,
      tertiary: AppColors.darkTertiary,
      onTertiary: Color(0xFF462A00),
      tertiaryContainer: AppColors.darkTertiaryContainer,
      onTertiaryContainer: Color(0xFF6C4400),
      error: AppColors.darkError,
      onError: AppColors.darkSurface,
      errorContainer: AppColors.darkErrorContainer,
      onErrorContainer: AppColors.darkError,
      surface: AppColors.darkSurface,
      onSurface: AppColors.darkOnSurface,
      onSurfaceVariant: AppColors.darkOnSurfaceVariant,
      outline: AppColors.darkOutline,
      outlineVariant: AppColors.darkOutlineVariant,
      shadow: Colors.black,
      scrim: Colors.black,
      inverseSurface: AppColors.darkOnSurface,
      onInverseSurface: AppColors.darkSurface,
      inversePrimary: Color(0xFF006491),
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: cs,
      scaffoldBackgroundColor: AppColors.darkSurface,
      textTheme: _textTheme(AppColors.darkOnSurface, AppColors.darkSecondary),
      appBarTheme: AppBarTheme(
        backgroundColor: const Color(0xCC101126),
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        systemOverlayStyle: SystemUiOverlayStyle.light,
        titleTextStyle: GoogleFonts.spaceGrotesk(
          fontSize: 20, fontWeight: FontWeight.w900,
          color: AppColors.darkPrimaryContainer, letterSpacing: 3,
        ),
        iconTheme: const IconThemeData(color: AppColors.darkPrimary),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: const Color(0xEA27283E),
        indicatorColor: Colors.transparent,
        labelTextStyle: WidgetStateProperty.resolveWith((s) =>
          GoogleFonts.manrope(
            fontSize: 9, fontWeight: FontWeight.w700, letterSpacing: 0.6,
            color: s.contains(WidgetState.selected)
                ? AppColors.darkSurface
                : AppColors.darkSecondary,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith((s) => IconThemeData(
          color: s.contains(WidgetState.selected)
              ? AppColors.darkSurface
              : AppColors.darkSecondary,
        )),
        elevation: 0,
      ),
      cardTheme: CardThemeData(
        color: AppColors.darkSurfaceHigh,
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        border: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.darkOutlineVariant),
        ),
        enabledBorder: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.darkOutlineVariant),
        ),
        focusedBorder: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.darkPrimaryContainer, width: 2),
        ),
        hintStyle: GoogleFonts.manrope(color: AppColors.darkOutline),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.darkPrimaryContainer,
          foregroundColor: AppColors.darkSurface,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(50)),
          padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 14),
          textStyle: GoogleFonts.spaceGrotesk(
            fontSize: 14, fontWeight: FontWeight.w700, letterSpacing: 0.8,
          ),
          elevation: 0,
        ),
      ),
      extensions: const [
        MasarColors(
          surfaceLow: AppColors.darkSurfaceLow,
          surfaceContainer: AppColors.darkSurfaceContainer,
          surfaceHigh: AppColors.darkSurfaceHigh,
          surfaceHighest: AppColors.darkSurfaceHighest,
          tertiaryContainer: AppColors.darkTertiaryContainer,
          gradientStart: AppColors.darkPrimary,
          gradientEnd: AppColors.darkPrimaryContainer,
          btnText: AppColors.darkSurface,
        ),
      ],
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
      secondaryContainer: Color(0xFFDEDDFA),
      onSecondaryContainer: Color(0xFF191A2F),
      tertiary: AppColors.lightTertiary,
      onTertiary: AppColors.lightOnPrimary,
      tertiaryContainer: AppColors.lightTertiaryContainer,
      onTertiaryContainer: Color(0xFF005750),
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
      inversePrimary: AppColors.lightPrimaryContainer,
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: cs,
      scaffoldBackgroundColor: AppColors.lightSurface,
      textTheme: _textTheme(AppColors.lightOnSurface, AppColors.lightSecondary),
      appBarTheme: AppBarTheme(
        backgroundColor: const Color(0xE0EBFDFF),
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        systemOverlayStyle: SystemUiOverlayStyle.dark,
        titleTextStyle: GoogleFonts.spaceGrotesk(
          fontSize: 20, fontWeight: FontWeight.w900,
          color: AppColors.lightPrimary, letterSpacing: 3,
        ),
        iconTheme: const IconThemeData(color: AppColors.lightPrimary),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: const Color(0xE8EBFDFF),
        indicatorColor: Colors.transparent,
        labelTextStyle: WidgetStateProperty.resolveWith((s) =>
          GoogleFonts.manrope(
            fontSize: 9, fontWeight: FontWeight.w700, letterSpacing: 0.6,
            color: s.contains(WidgetState.selected)
                ? AppColors.lightOnPrimary
                : AppColors.lightSecondary,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith((s) => IconThemeData(
          color: s.contains(WidgetState.selected)
              ? AppColors.lightOnPrimary
              : AppColors.lightSecondary,
        )),
        elevation: 0,
      ),
      cardTheme: CardThemeData(
        color: AppColors.lightSurfaceHigh,
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        border: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.lightOutlineVariant),
        ),
        enabledBorder: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.lightOutlineVariant),
        ),
        focusedBorder: const UnderlineInputBorder(
          borderSide: const BorderSide(color: AppColors.lightPrimary, width: 2),
        ),
        hintStyle: GoogleFonts.manrope(color: AppColors.lightOutline),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: AppColors.lightPrimary,
          foregroundColor: AppColors.lightOnPrimary,
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(50)),
          padding: const EdgeInsets.symmetric(horizontal: 28, vertical: 14),
          textStyle: GoogleFonts.spaceGrotesk(
            fontSize: 14, fontWeight: FontWeight.w700, letterSpacing: 0.8,
          ),
          elevation: 0,
        ),
      ),
      extensions: const [
        MasarColors(
          surfaceLow: AppColors.lightSurfaceLow,
          surfaceContainer: AppColors.lightSurfaceContainer,
          surfaceHigh: AppColors.lightSurfaceHigh,
          surfaceHighest: AppColors.lightSurfaceHighest,
          tertiaryContainer: AppColors.lightTertiaryContainer,
          gradientStart: AppColors.lightPrimaryContainer,
          gradientEnd: AppColors.lightPrimary,
          btnText: AppColors.lightOnPrimary,
        ),
      ],
    );
  }
}

// Custom theme extension for MASAR-specific colors
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

  LinearGradient get primaryGradient => LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
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
