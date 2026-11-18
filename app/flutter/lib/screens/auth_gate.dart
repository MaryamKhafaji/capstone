
import 'package:flutter/material.dart';
import 'package:firebase_auth/firebase_auth.dart';

import 'login_screen.dart';
import 'main_shell.dart';

class AuthGate extends StatelessWidget {
  const AuthGate({super.key});

  @override
  Widget build(BuildContext context) {
    return StreamBuilder<User?>(
      stream: FirebaseAuth.instance.authStateChanges(),
      builder: (context, snapshot) {
        debugPrint(
          'AUTH GATE: state=${snapshot.connectionState}, '
          'user=${snapshot.data?.email}, '
          'error=${snapshot.error}',
        );

        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Scaffold(
            body: Center(child: CircularProgressIndicator()),
          );
        }

        if (snapshot.hasError) {
          return const Scaffold(
            body: Center(child: Text('Authentication error')),
          );
        }

        return Navigator(
          key: ValueKey(snapshot.data?.uid ?? 'logged-out'),
          onGenerateRoute: (settings) {
            return MaterialPageRoute(
              builder: (_) => snapshot.data != null
                  ? const MainShell()
                  : const LoginScreen(),
            );
          },
        );
      },
    );
  }
}
