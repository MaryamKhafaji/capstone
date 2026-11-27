import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';
import 'main_shell.dart';
import 'hunt_badges_screen.dart';
import 'settings_screen.dart';
import 'walking_insights_screen.dart';
import 'package:provider/provider.dart';
import '../services/footwear_ble_service.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  void _openSettings(BuildContext context) {
    final footwear = context.read<FootwearBleService>();

    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider<FootwearBleService>.value(
          value: footwear,
          child: const SettingsScreen(),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final user = FirebaseAuth.instance.currentUser;

    if (user == null) {
      return const Scaffold(
        body: Center(
          child: Text('No user is signed in.'),
        ),
      );
    }

    final userDoc =
        FirebaseFirestore.instance.collection('users').doc(user.uid);

    return StreamBuilder<DocumentSnapshot<Map<String, dynamic>>>(
      stream: userDoc.snapshots(),
      builder: (context, snapshot) {
        if (!snapshot.hasData) {
          return const Scaffold(
            body: Center(
              child: CircularProgressIndicator(),
            ),
          );
        }

        final data = snapshot.data!.data() ?? {};

        final name = (data['name'] ?? '').toString();
        final level = data['level'] ?? 1;
        final totalSteps = data['totalSteps'] ?? 0;
        final streak = data['streak'] ?? 0;

        final cs = Theme.of(context).colorScheme;
        final mc = Theme.of(context).extension<MasarColors>()!;

        return Scaffold(
          appBar: MasarAppBar(
            actions: [
              IconButton(
                icon: Icon(Icons.settings_rounded, color: cs.primary),
                onPressed: () => _openSettings(context),
              ),
            ],
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
            child: Column(
              children: [
                // ── Profile Hero ──
                Center(
                  child: Column(
                    children: [
                      Stack(
                        children: [
                          Container(
                            width: 100,
                            height: 100,
                            decoration: BoxDecoration(
                              borderRadius: BorderRadius.circular(24),
                              color: mc.surfaceHigh,
                              border: Border.all(
                                color:
                                    cs.primaryContainer.withValues(alpha: 0.5),
                                width: 2.5,
                              ),
                            ),
                            child: Icon(
                              Icons.person_rounded,
                              color: cs.secondary,
                              size: 56,
                            ),
                          ),
                          Positioned(
                            bottom: -4,
                            right: -4,
                            child: Container(
                              width: 30,
                              height: 30,
                              decoration: BoxDecoration(
                                gradient: mc.primaryGradient,
                                borderRadius: BorderRadius.circular(10),
                              ),
                              child: Icon(
                                Icons.verified_rounded,
                                color: mc.btnText,
                                size: 16,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 14),
                      Text(
                        name,
                        style: GoogleFonts.spaceGrotesk(
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          color: cs.onSurface,
                        ),
                      ),
                      const SizedBox(height: 16),
                    ],
                  ),
                ),
                const SizedBox(height: 24),

                // ── Distance Card ──
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(20),
                  decoration: BoxDecoration(
                    color: mc.surfaceHigh,
                    borderRadius: BorderRadius.circular(16),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const SectionLabel('All time Steps'),
                      const SizedBox(height: 8),
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text(
                            totalSteps.toString(),
                            style: GoogleFonts.spaceGrotesk(
                              fontSize: 40,
                              fontWeight: FontWeight.w800,
                              color: cs.primary,
                              height: 1,
                            ),
                          ),
                          Padding(
                            padding: const EdgeInsets.only(bottom: 6, left: 6),
                            child: Text(
                              'STEPS',
                              style: GoogleFonts.spaceGrotesk(
                                fontSize: 18,
                                fontWeight: FontWeight.w600,
                                color: cs.secondary,
                              ),
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      const MasarProgressBar(value: 0.75),
                    ],
                  ),
                ),
                const SizedBox(height: 12),

                // ── Stats Grid ──
                Row(
                  children: [
                    Expanded(
                      child: StatCard(
                        icon: Icons.military_tech_rounded,
                        iconColor: cs.primary,
                        value: 'Level $level',
                        label: 'Current Rank',
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: StatCard(
                        icon: Icons.bolt_rounded,
                        iconColor: cs.tertiary,
                        value: '$streak ${streak == 1 ? 'Day' : 'Days'}',
                        label: 'Active Streak',
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 20),
                MasarListItem(
                  icon: Icons.bar_chart_rounded,
                  title: 'Insights',
                  subtitle: 'Explore your walking activity',
                  onTap: () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const WalkingInsightsScreen(),
                    ),
                  ),
                ),
                const SizedBox(height: 16),

                // ── Achievements ──
                const SectionHeader(title: 'Achievements'),
                ProfileAchievements(userId: user.uid),
              ],
            ),
          ),
        );
      },
    );
  }
}
