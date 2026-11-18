import 'package:flutter/material.dart';
import 'package:google_fonts/google_fonts.dart';
import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';

class WalkSessionScreen extends StatelessWidget {
  const WalkSessionScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final mc = Theme.of(context).extension<MasarColors>()!;

    return Scaffold(
      appBar: AppBar(
        title: Text('Session Summary',
          style: GoogleFonts.spaceGrotesk(
            fontSize: 16, fontWeight: FontWeight.w700, color: cs.onSurface,
          ),
        ),
        leading: IconButton(
          icon: Icon(Icons.arrow_back_rounded, color: cs.primary),
          onPressed: () => Navigator.pop(context),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 32),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // ── Hero ──
            Center(
              child: Column(
                children: [
                  SectionLabel('Session Completed'),
                  const SizedBox(height: 4),
                  Text('Great Walk.',
                    style: GoogleFonts.spaceGrotesk(
                      fontSize: 48, fontWeight: FontWeight.w800,
                      color: cs.onSurface, height: 1,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // ── Distance + Points ──
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  flex: 2,
                  child: Container(
                    padding: const EdgeInsets.all(20),
                    decoration: BoxDecoration(
                      color: mc.surfaceHigh,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        SectionLabel('Distance'),
                        const SizedBox(height: 10),
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.end,
                          children: [
                            Text('4.82',
                              style: GoogleFonts.spaceGrotesk(
                                fontSize: 52, fontWeight: FontWeight.w800,
                                color: cs.onSurface, height: 1,
                              ),
                            ),
                            Padding(
                              padding: const EdgeInsets.only(bottom: 8, left: 4),
                              child: Text('KM',
                                style: GoogleFonts.spaceGrotesk(
                                  fontSize: 22, fontWeight: FontWeight.w700,
                                  color: cs.primaryContainer,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: BoxDecoration(
                      color: mc.surfaceHigh,
                      borderRadius: BorderRadius.circular(16),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Icon(Icons.workspace_premium_rounded,
                          color: cs.primaryContainer, size: 28),
                        const SizedBox(height: 8),
                        Text('+120',
                          style: GoogleFonts.spaceGrotesk(
                            fontSize: 28, fontWeight: FontWeight.w800,
                            color: cs.primary,
                          ),
                        ),
                        Text('POINTS',
                          style: GoogleFonts.manrope(
                            fontSize: 9, fontWeight: FontWeight.w700,
                            color: cs.secondary, letterSpacing: 0.8,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),

            // ── Metrics Grid ──
            GridView.count(
              crossAxisCount: 2,
              mainAxisSpacing: 10,
              crossAxisSpacing: 10,
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              childAspectRatio: 1.8,
              children: [
                StatCard(icon: Icons.timer_rounded, iconColor: cs.secondary, value: '00:42:15', label: 'Total Time'),
                StatCard(icon: Icons.directions_walk_rounded, iconColor: cs.secondary, value: '6,420', label: 'Steps Taken'),
                StatCard(icon: Icons.speed_rounded, iconColor: cs.secondary, value: '8:45', label: 'Min/KM Pace'),
                StatCard(icon: Icons.local_fire_department_rounded, iconColor: cs.secondary, value: '342', label: 'Kcal Burned'),
              ],
            ),
            const SizedBox(height: 12),

            // ── Energy ──
            GlassCard(
              child: Column(
                children: [
                  Row(
                    children: [
                      Container(
                        width: 44, height: 44,
                        decoration: BoxDecoration(
                          color: mc.tertiaryContainer.withValues(alpha: 0.2),
                          shape: BoxShape.circle,
                        ),
                        child: Icon(Icons.bolt_rounded,
                          color: cs.tertiary, size: 22),
                      ),
                      const SizedBox(width: 12),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('1.2 Wh',
                            style: GoogleFonts.spaceGrotesk(
                              fontSize: 20, fontWeight: FontWeight.w700,
                              color: cs.onSurface,
                            ),
                          ),
                          Text('Energy Generated via Kinetic Step',
                            style: GoogleFonts.manrope(
                              fontSize: 11, color: cs.secondary,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  MasarProgressBar(value: 0.67, color: mc.tertiaryContainer),
                ],
              ),
            ),
            const SizedBox(height: 10),

            // ── Quest Progress ──
            GlassCard(
              child: Column(
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 40, height: 40,
                            decoration: BoxDecoration(
                              color: cs.primaryContainer.withValues(alpha: 0.15),
                              shape: BoxShape.circle,
                            ),
                            child: Icon(Icons.explore_rounded,
                              color: cs.primaryContainer, size: 20),
                          ),
                          const SizedBox(width: 10),
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Quest Progress',
                                style: GoogleFonts.spaceGrotesk(
                                  fontSize: 16, fontWeight: FontWeight.w700,
                                  color: cs.onSurface,
                                ),
                              ),
                              Text('Urban Explorer IV',
                                style: GoogleFonts.manrope(
                                  fontSize: 11, color: cs.secondary,
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                      Text('85%',
                        style: GoogleFonts.spaceGrotesk(
                          fontSize: 22, fontWeight: FontWeight.w700,
                          color: cs.primary,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  MasarProgressBar(value: 0.85),
                ],
              ),
            ),
            const SizedBox(height: 20),

            GradientButton(
              label: 'Done',
              icon: Icons.arrow_forward_rounded,
              onPressed: () => Navigator.pop(context),
            ),
          ],
        ),
      ),
    );
  }
}
