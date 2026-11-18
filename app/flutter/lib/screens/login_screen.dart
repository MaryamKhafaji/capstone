import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';
import 'main_shell.dart';
import 'register_screen.dart';
import 'package:flutter/gestures.dart';
import 'package:firebase_auth/firebase_auth.dart';
import '../services/auth_service.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _emailCtrl = TextEditingController();
  final _passCtrl = TextEditingController();
  bool _loading = false;



Future<void> _login() async {
  if (_loading) return;

  setState(() => _loading = true);

  try {
    await AuthService().login(
      email: _emailCtrl.text.trim(),
      password: _passCtrl.text,
    );

    // AuthGate automatically displays MainShell
    // after successful Firebase authentication.

  } on FirebaseAuthException catch (e) {
    if (!mounted) return;

    String message;

    switch (e.code) {
      case 'invalid-email':
        message = 'Please enter a valid email address.';
        break;
      case 'invalid-credential':
      case 'wrong-password':
      case 'user-not-found':
        message = 'Incorrect email or password.';
        break;
      case 'too-many-requests':
        message = 'Too many attempts. Please try again later.';
        break;
      default:
        message = 'Login failed. Please try again.';
    }

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  } catch (e) {
    if (!mounted) return;

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Unable to log in. Check your connection.'),
      ),
    );
  } finally {
    if (mounted) {
      setState(() => _loading = false);
    }
  }
}



  @override
  void dispose() {
    _emailCtrl.dispose();
    _passCtrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Stack(
        children: [
          // Background
          Positioned.fill(
            child: Container(color: cs.surface),
          ),
          // Decorative glow
          Positioned(
            top: -100, right: -100,
            child: Container(
              width: 300, height: 300,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: cs.primaryContainer.withValues(alpha: isDark ? 0.06 : 0.15),
              ),
            ),
          ),
          Positioned(
            bottom: -80, left: -80,
            child: Container(
              width: 250, height: 250,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: cs.tertiary.withValues(alpha: isDark ? 0.05 : 0.08),
              ),
            ),
          ),
          // Content
          SafeArea(
            child: Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    // Logo
                    Text(
                      'MASAR+',
                      style: GoogleFonts.spaceGrotesk(
                        fontSize: 40, fontWeight: FontWeight.w900,
                        color: cs.primary, letterSpacing: 6,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'HIGH PERFORMANCE NAVIGATION',
                      style: GoogleFonts.manrope(
                        fontSize: 10, fontWeight: FontWeight.w700,
                        color: cs.secondary, letterSpacing: 2,
                      ),
                    ),
                    const SizedBox(height: 48),

                    // Login Card
                    Container(
                      constraints: const BoxConstraints(maxWidth: 400),
                      padding: const EdgeInsets.all(28),
                      decoration: BoxDecoration(
                        color: isDark
                            ? mc.surfaceHigh.withValues(alpha: 0.6)
                            : mc.surfaceContainer.withValues(alpha: 0.7),
                        borderRadius: BorderRadius.circular(20),
                        border: Border.all(
                          color: cs.outlineVariant.withValues(alpha: 0.3),
                        ),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Welcome Back!',
                            style: GoogleFonts.spaceGrotesk(
                              fontSize: 22, fontWeight: FontWeight.w700,
                              color: cs.onSurface,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Text('ENTER YOUR CREDENTIALS',
                            style: GoogleFonts.manrope(
                              fontSize: 10, fontWeight: FontWeight.w700,
                              color: cs.secondary, letterSpacing: 1,
                            ),
                          ),
                          const SizedBox(height: 32),

                          // Email
                          Text('EMAIL OR PHONE',
                            style: GoogleFonts.manrope(
                              fontSize: 10, fontWeight: FontWeight.w700,
                              color: cs.secondary, letterSpacing: 1,
                            ),
                          ),
                          const SizedBox(height: 8),
                          TextField(
                            controller: _emailCtrl,
                            style: GoogleFonts.manrope(
                              fontSize: 15, color: cs.onSurface,
                            ),
                            decoration: InputDecoration(
                              prefixIcon: Icon(Icons.person_outline,
                                color: cs.outline, size: 20),
                              hintText: 'Enter email or phone number',
                            ),
                          ),
                          const SizedBox(height: 24),

                          // Password
                          Text('PASSWORD',
                            style: GoogleFonts.manrope(
                              fontSize: 10, fontWeight: FontWeight.w700,
                              color: cs.secondary, letterSpacing: 1,
                            ),
                          ),
                          const SizedBox(height: 8),
                          TextField(
                            controller: _passCtrl,
                            obscureText: true,
                            style: GoogleFonts.manrope(
                              fontSize: 15, color: cs.onSurface,
                            ),
                            decoration: InputDecoration(
                              prefixIcon: Icon(Icons.lock_outline,
                                color: cs.outline, size: 20),
                              hintText: '••••••••',
                            ),
                          ),
                          const SizedBox(height: 32),

                          // Login Button
                          _loading
                              ? Center(
                                  child: CircularProgressIndicator(
                                    color: cs.primaryContainer,
                                  ),
                                )
                              : GradientButton(
                                  label: 'Login',
                                  icon: Icons.arrow_forward,
                                  onPressed: _login,
                                ),
                          const SizedBox(height: 24),

                          // Footer
                          Center(
                            child: TextButton(
                              onPressed: () {},
                              child: Text('Reset Password',
                                style: GoogleFonts.manrope(
                                  fontSize: 12, fontWeight: FontWeight.w700,
                                  color: cs.primary, letterSpacing: 1,
                                ),
                              ),
                            ),
                          ),
                          const Divider(height: 1),
                          const SizedBox(height: 12),
                          
                          Center(
                              child: RichText(
                                text: TextSpan(
                                  style: GoogleFonts.manrope(
                                    fontSize: 13,
                                    color: cs.onSurfaceVariant,
                                  ),
                               children: [
                                   const TextSpan(text: 'New user? '),
                                 TextSpan(
                                   text: 'Register',
                                     style: TextStyle(
                                      color: cs.primary,
                                      fontWeight: FontWeight.w700,
                                   ),
                                    recognizer: TapGestureRecognizer()
                                        ..onTap = () {
                                          Navigator.push(
                                            context,
                                            MaterialPageRoute(
                                             builder: (context) => const RegisterScreen(),
                                           ),
                                       );
                                     },
                                 ),
                                ],
                               ),
                                ),
                                ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 32),

                    // Status bar
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Row(
                          children: [
                            PulseDot(color: cs.primaryContainer),
                            const SizedBox(width: 6),
                            Text('SYSTEM ONLINE',
                              style: GoogleFonts.manrope(
                                fontSize: 10, fontWeight: FontWeight.w700,
                                color: cs.secondary, letterSpacing: 1,
                              ),
                            ),
                          ],
                        ),
                        Text('v2.4.0-MASAR+',
                          style: GoogleFonts.manrope(
                            fontSize: 10, color: cs.outline,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
