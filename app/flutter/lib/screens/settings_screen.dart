import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:provider/provider.dart';
import '../theme/app_theme.dart';
import '../theme/theme_provider.dart';
import '../widgets/shared_widgets.dart';
import 'package:firebase_auth/firebase_auth.dart';
import '../services/footwear_ble_service.dart';
import '../services/unity_hunt_bridge.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;
    final themeProvider = context.watch<ThemeProvider>();
    final isDark = themeProvider.isDark;
    final footwear = context.watch<FootwearBleService>();


    return Scaffold(
      appBar: AppBar(
        title: Text('Settings',
          style: GoogleFonts.spaceGrotesk(
            fontSize: 18, fontWeight: FontWeight.w700, color: cs.onSurface,
          ),
        ),
        leading: IconButton(
          icon: Icon(Icons.arrow_back_rounded, color: cs.primary),
          onPressed: () => Navigator.pop(context),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 40),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header
            Text('Settings',
              style: GoogleFonts.spaceGrotesk(
                fontSize: 32, fontWeight: FontWeight.w800, color: cs.onSurface,
              ),
            ),
            const SizedBox(height: 4),
            Text('Configure your preferred settings',
              style: GoogleFonts.manrope(fontSize: 13, color: cs.secondary),
            ),
            const SizedBox(height: 24),

            // ── THEME TOGGLE ──────────────────────────────────────────────────
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                color: mc.surfaceHigh,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: cs.outlineVariant.withValues(alpha: 0.2),
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('System Appearance',
                    style: GoogleFonts.spaceGrotesk(
                      fontSize: 18, fontWeight: FontWeight.w700, color: cs.primary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    isDark ? 'Dark Mode Active' : 'Light Mode Active',
                    style: GoogleFonts.manrope(
                      fontSize: 10, fontWeight: FontWeight.w700,
                      color: cs.secondary,
                      letterSpacing: 0.8,
                    ),
                  ),
                  const SizedBox(height: 20),

                  // Toggle buttons
                  Container(
                    padding: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      color: mc.surfaceContainer,
                      borderRadius: BorderRadius.circular(50),
                    ),
                    child: Row(
                      children: [
                        // Light button
                        Expanded(
                          child: GestureDetector(
                            onTap: () => context.read<ThemeProvider>().setTheme(false),
                            child: AnimatedContainer(
                              duration: const Duration(milliseconds: 250),
                              curve: Curves.easeInOut,
                              padding: const EdgeInsets.symmetric(vertical: 12),
                              decoration: BoxDecoration(
                                gradient: !isDark ? mc.primaryGradient : null,
                                color: !isDark ? null : Colors.transparent,
                                borderRadius: BorderRadius.circular(50),
                              ),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(
                                    Icons.light_mode_rounded,
                                    size: 18,
                                    color: !isDark ? mc.btnText : cs.secondary,
                                  ),
                                  const SizedBox(width: 6),
                                  Text('Light',
                                    style: GoogleFonts.spaceGrotesk(
                                      fontSize: 13, fontWeight: FontWeight.w700,
                                      color: !isDark ? mc.btnText : cs.secondary,
                                      letterSpacing: 0.4,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                        // Dark button
                        Expanded(
                          child: GestureDetector(
                            onTap: () => context.read<ThemeProvider>().setTheme(true),
                            child: AnimatedContainer(
                              duration: const Duration(milliseconds: 250),
                              curve: Curves.easeInOut,
                              padding: const EdgeInsets.symmetric(vertical: 12),
                              decoration: BoxDecoration(
                                gradient: isDark ? mc.primaryGradient : null,
                                color: isDark ? null : Colors.transparent,
                                borderRadius: BorderRadius.circular(50),
                              ),
                              child: Row(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(
                                    Icons.dark_mode_rounded,
                                    size: 18,
                                    color: isDark ? mc.btnText : cs.secondary,
                                  ),
                                  const SizedBox(width: 6),
                                  Text('Dark',
                                    style: GoogleFonts.spaceGrotesk(
                                      fontSize: 13, fontWeight: FontWeight.w700,
                                      color: isDark ? mc.btnText : cs.secondary,
                                      letterSpacing: 0.4,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Quick toggle row
                  const SizedBox(height: 16),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Quick Toggle',
                        style: GoogleFonts.manrope(
                          fontSize: 13, color: cs.onSurfaceVariant,
                        ),
                      ),
                      Switch.adaptive(
                        value: isDark,
                        onChanged: (_) => context.read<ThemeProvider>().toggleTheme(),
                        activeColor: cs.primaryContainer,
                        activeTrackColor: cs.primaryContainer.withValues(alpha: 0.3),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            // ─────────────────────────────────────────────────────────────────

            const SizedBox(height: 16),
            const _CloseRangeHuntToggle(),
            const SizedBox(height: 12),
            
            _SettingsCard(
              icon: Icons.bluetooth_connected_rounded,
              iconBg: cs.primary.withValues(alpha: 0.1),
              iconColor: cs.primary,
              title: 'Smart Footwear',
              subtitle: footwear.status,
              onTap: () {
                showModalBottomSheet(
                  context: context,
                  showDragHandle: true,
                  builder: (sheetContext) {
                    return SafeArea(
                      child: Padding(
                        padding: const EdgeInsets.all(20),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            const Text(
                              'Manage Smart Footwear',
                              style: TextStyle(
                                fontSize: 20,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                            const SizedBox(height: 16),
                            Text('Status: ${footwear.status}'),
                            const SizedBox(height: 16),
                            ElevatedButton.icon(
                              onPressed: () async {
                                Navigator.of(sheetContext).pop();
                                await footwear.connect();
                              },
                              icon: const Icon(Icons.bluetooth_searching),
                              label: const Text('Connect Shoe'),
                            ),
                            OutlinedButton.icon(
                              onPressed: () async {
                                Navigator.of(sheetContext).pop();
                                await footwear.disconnect();
                              },
                              icon: const Icon(Icons.bluetooth_disabled),
                              label: const Text('Disconnect Shoe'),
                            ),
                            TextButton.icon(
                              onPressed: () async {
                                Navigator.of(sheetContext).pop();
                                await footwear.forgetShoe();
                              },
                              icon: const Icon(Icons.delete_outline),
                              label: const Text('Forget Shoe'),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                );
              },
            ),

            const SizedBox(height: 12),


            // General Prefs
            _SettingsCard(
              icon: Icons.tune_rounded,
              iconBg: cs.primary.withValues(alpha: 0.1),
              iconColor: cs.primary,
              title: 'General Preferences',
              subtitle: 'Units, Language, and Regional settings',
              onTap: () {},
            ),

            // Notifications
            _SettingsCard(
              icon: Icons.notifications_active_rounded,
              iconBg: mc.tertiaryContainer.withValues(alpha: 0.15),
              iconColor: mc.tertiaryContainer,
              title: 'Notifications',
              subtitle: 'Smart alerts, haptic feedback, audio cues',
              trailing: MasarBadge(
                text: '4 Active',
                bgColor: mc.tertiaryContainer.withValues(alpha: 0.15),
                textColor: mc.tertiaryContainer,
              ),
              onTap: () {},
            ),

            // Account Security — featured
            Container(
              margin: const EdgeInsets.only(bottom: 10),
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: mc.surfaceContainer,
                borderRadius: BorderRadius.circular(14),
                border: Border(
                  left: BorderSide(
                    color: cs.primaryContainer, width: 3,
                  ),
                ),
              ),
              child: Row(
                children: [
                  Container(
                    width: 44, height: 44,
                    decoration: BoxDecoration(
                      color: mc.surfaceHigh,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(Icons.shield_rounded,
                      color: cs.primaryContainer, size: 22),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Account Security',
                          style: GoogleFonts.manrope(
                            fontSize: 14, fontWeight: FontWeight.w700,
                            color: cs.onSurface,
                          ),
                        ),
                        Text('Biometric, 2FA, Hardware binding',
                          style: GoogleFonts.manrope(
                            fontSize: 11, color: cs.secondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text('Verified',
                        style: GoogleFonts.manrope(
                          fontSize: 11, fontWeight: FontWeight.w700,
                          color: cs.primaryContainer,
                        ),
                      ),
                      const SizedBox(width: 4),
                      Icon(Icons.verified_rounded,
                        color: cs.primaryContainer, size: 18),
                    ],
                  ),
                ],
              ),
            ),

            // Two small tiles
            Row(
              children: [
                Expanded(
                  child: _SmallTile(
                    icon: Icons.description_rounded,
                    title: 'Privacy Policy',
                    onTap: () {},
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: _SmallTile(
                    icon: Icons.info_outline_rounded,
                    title: 'About MASAR+',
                    trailing: Text('v4.2.0',
                      style: GoogleFonts.manrope(
                        fontSize: 10, color: cs.outline,
                      ),
                    ),
                    onTap: () {},
                  ),
                ),
              ],
            ),

            const SizedBox(height: 20),

            // Logout / Terminate
            GestureDetector(
              onTap: () {
                showDialog(
                  context: context,
                  builder: (dialogContext) => AlertDialog(
                    backgroundColor: Theme.of(context).extension<MasarColors>()!.surfaceHigh,
                    title: Text('Log out',
                      style: GoogleFonts.spaceGrotesk(
                        fontWeight: FontWeight.w700,
                        color: cs.onSurface,
                      ),
                    ),
                    content: Text('Are you sure you want to log out?',
                      style: GoogleFonts.manrope(color: cs.secondary),
                    ),

                    actions: [
                     TextButton(
                        onPressed: () {
                          Navigator.of(dialogContext).pop();
                       },
                       child: Text(
                        'Cancel',
                        style: TextStyle(color: cs.secondary),
                       ),
                    ),
                    TextButton(
                      onPressed: () async {
                       // Close the confirmation dialog first.
                       Navigator.of(dialogContext).pop();

                       // Sign out from Firebase.
                       // AuthGate automatically switches to LoginScreen.
                       await FirebaseAuth.instance.signOut();
                      },
                      child: Text(
                      'Logout',
                      style: TextStyle(color: cs.error),
                       ),
                     ),
                  ],

                  ),
                );
              },
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(vertical: 16),
                decoration: BoxDecoration(
                  color: cs.error.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: cs.error.withValues(alpha: 0.2)),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.logout_rounded, color: cs.error, size: 18),
                    const SizedBox(width: 8),
                    Text('Log out',
                      style: GoogleFonts.spaceGrotesk(
                        fontSize: 12, fontWeight: FontWeight.w700,
                        color: cs.error, letterSpacing: 1,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CloseRangeHuntToggle extends StatefulWidget {
  const _CloseRangeHuntToggle();

  @override
  State<_CloseRangeHuntToggle> createState() => _CloseRangeHuntToggleState();
}

class _CloseRangeHuntToggleState extends State<_CloseRangeHuntToggle> {
  bool? _enabled;

  @override
  void initState() {
    super.initState();
    UnityHuntBridge.closeRangeDebugEnabled().then((value) {
      if (mounted) setState(() => _enabled = value);
    });
  }

  @override
  Widget build(BuildContext context) => SwitchListTile.adaptive(
    title: const Text('Close-range AR testing'),
    subtitle: const Text('Spawn monsters nearby for testing. Turn off to hunt at real-world locations.'),
    value: _enabled ?? false,
    onChanged: _enabled == null ? null : (value) async {
      await UnityHuntBridge.setCloseRangeDebug(value);
      if (mounted) setState(() => _enabled = value);
    },
  );
}

class _SettingsCard extends StatelessWidget {
  final IconData icon;
  final Color iconBg;
  final Color iconColor;
  final String title;
  final String subtitle;
  final Widget? trailing;
  final VoidCallback onTap;

  const _SettingsCard({
    required this.icon,
    required this.iconBg,
    required this.iconColor,
    required this.title,
    required this.subtitle,
    this.trailing,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;

    return GestureDetector(
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: mc.surfaceContainer,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          children: [
            Container(
              width: 44, height: 44,
              decoration: BoxDecoration(color: iconBg, borderRadius: BorderRadius.circular(12)),
              child: Icon(icon, color: iconColor, size: 22),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title,
                    style: GoogleFonts.manrope(
                      fontSize: 14, fontWeight: FontWeight.w700, color: cs.onSurface,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(subtitle,
                    style: GoogleFonts.manrope(fontSize: 11, color: cs.secondary),
                  ),
                ],
              ),
            ),
            trailing ?? Icon(Icons.chevron_right_rounded, color: cs.outline, size: 20),
          ],
        ),
      ),
    );
  }
}

class _SmallTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final Widget? trailing;
  final VoidCallback onTap;

  const _SmallTile({
    required this.icon,
    required this.title,
    this.trailing,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;

    return GestureDetector(
      onTap: onTap,
      child: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: mc.surfaceContainer,
          borderRadius: BorderRadius.circular(14),
        ),
        child: Row(
          children: [
            Icon(icon, color: cs.outline, size: 18),
            const SizedBox(width: 8),
            Expanded(
              child: Text(title,
                style: GoogleFonts.manrope(
                  fontSize: 13, fontWeight: FontWeight.w600, color: cs.onSurface,
                ),
              ),
            ),
            if (trailing != null) trailing!
            else Icon(Icons.chevron_right_rounded, color: cs.outline, size: 18),
          ],
        ),
      ),
    );
  }
}
