import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';
import 'theme/app_theme.dart';
import 'theme/theme_provider.dart';
import 'screens/login_screen.dart';
import 'package:firebase_core/firebase_core.dart';
import 'firebase_options.dart';
import 'package:flutter/widgets.dart' as widgets;
import 'screens/auth_gate.dart';
import 'setup_database.dart';



Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await Firebase.initializeApp(
    options: DefaultFirebaseOptions.currentPlatform,
  );
 // await DatabaseSetup.createCollections();

  await SystemChrome.setPreferredOrientations([
    DeviceOrientation.portraitUp,
    DeviceOrientation.portraitDown,
    DeviceOrientation.landscapeLeft,
    DeviceOrientation.landscapeRight,
  ]);

  widgets.runApp(
  ChangeNotifierProvider<ThemeProvider>(
    create: (_) => ThemeProvider(),
    child: const MasarPlusApp(),
  ),
);
}


class MasarPlusApp extends StatelessWidget {
  const MasarPlusApp({super.key});

  @override
  Widget build(BuildContext context) {
    final themeProvider = context.watch<ThemeProvider>();

    return AnimatedTheme(
      data: themeProvider.isDark ? AppTheme.dark() : AppTheme.light(),
      duration: const Duration(milliseconds: 300),
      child: MaterialApp(
        title: 'MASAR+',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light(),
        darkTheme: AppTheme.dark(),
        themeMode: themeProvider.themeMode,
        home: const AuthGate(),
        // Smooth page transitions
        builder: (context, child) {
          return MediaQuery(
            // Prevent font scaling from system settings breaking the UI
            data: MediaQuery.of(context).copyWith(
              textScaler: TextScaler.linear(
                MediaQuery.of(context).textScaleFactor.clamp(0.85, 1.1),
              ),
            ),
            child: child!,
          );
        },
      ),
    );
  }
}
