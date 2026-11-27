import 'dart:async';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:flutter/material.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:cloud_firestore/cloud_firestore.dart';
import '../services/walking_insights_service.dart';
import '../theme/app_theme.dart';
import '../widgets/shared_widgets.dart';
import 'package:google_fonts/google_fonts.dart';

class WalkingInsightsScreen extends StatefulWidget {
  const WalkingInsightsScreen({super.key});
  @override
  State<WalkingInsightsScreen> createState() => _WalkingInsightsScreenState();
}

class _WalkingInsightsScreenState extends State<WalkingInsightsScreen> {
  final service = WalkingInsightsService.instance;
  int period = 0, offset = 0;
  int? selected;
  int goal = 0;
  Future<void> _loadGoal() async {
    final id = uid;
    final prefs = await SharedPreferences.getInstance();
    if (mounted && uid == id) {
      setState(() => goal = prefs.getInt('insights_goal_$id') ?? 0);
    }
  }

  Future<void> _editGoal() async {
    final id = uid;
    final controller = TextEditingController(text: goal == 0 ? '' : '$goal');
    final value = await showDialog<int>(
        context: context,
        builder: (ctx) => AlertDialog(
                title: const Text('Your optional daily goal'),
                content: TextField(
                    controller: controller,
                    keyboardType: TextInputType.number,
                    decoration: const InputDecoration(
                        hintText: 'Steps (0 to disable)')),
                actions: [
                  TextButton(
                      onPressed: () => Navigator.pop(ctx),
                      child: const Text('Cancel')),
                  TextButton(
                      onPressed: () {
                        final n = int.tryParse(controller.text);
                        if (n != null && n >= 0 && n <= 100000)
                          Navigator.pop(ctx, n);
                      },
                      child: const Text('Save'))
                ]));
    controller.dispose();
    if (value == null || !mounted || uid != id) return;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setInt('insights_goal_$id', value);
    if (mounted && uid == id) setState(() => goal = value);
  }

  late DateTime start, end;
  Stream<QuerySnapshot<Map<String, dynamic>>>? stream;
  Stream<QuerySnapshot<Map<String, dynamic>>>? huntStream;
  String? uid;
  StreamSubscription<User?>? _auth;
  static const months = [
    'Jan',
    'Feb',
    'Mar',
    'Apr',
    'May',
    'Jun',
    'Jul',
    'Aug',
    'Sep',
    'Oct',
    'Nov',
    'Dec'
  ];
  static const days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  @override
  void initState() {
    super.initState();
    uid = FirebaseAuth.instance.currentUser?.uid;
    _bindHunts();
    _range();
    _loadGoal();
    _auth = FirebaseAuth.instance.authStateChanges().listen((user) {
      if (!mounted || uid == user?.uid) return;
      setState(() {
        uid = user?.uid;
        _bindHunts();
        offset = 0;
        goal = 0;
        _range();
      });
      _loadGoal();
    });
  }

  @override
  void dispose() {
    _auth?.cancel();
    super.dispose();
  }

  void _bindHunts() {
    final id = uid;
    huntStream = id == null ? null : FirebaseFirestore.instance
        .collection('walking_sessions').where('userId', isEqualTo: id).snapshots();
  }

  void _range() {
    final n = DateTime.now();
    switch (period) {
      case 0:
        start = DateTime(n.year, n.month, n.day + offset);
        end = DateTime(start.year, start.month, start.day + 1);
        break;
      case 1:
        start = DateTime(n.year, n.month, n.day - n.weekday % 7 + offset * 7);
        end = DateTime(start.year, start.month, start.day + 7);
        break;
      case 2:
        start = DateTime(n.year, n.month + offset);
        end = DateTime(start.year, start.month + 1);
        break;
      default:
        start = DateTime(n.year + offset);
        end = DateTime(start.year + 1);
    }
    selected = null;
    stream = uid == null ? null : service.history(uid!, start, end);
  }

  String fmt(int n) => n
      .toString()
      .replaceAllMapped(RegExp(r'\B(?=(\d{3})+(?!\d))'), (_) => ',');
  String date(DateTime d) => '${d.day} ${months[d.month - 1]}';
  String get heading => period == 0
      ? '${date(start)} ${start.year}'
      : period == 1
          ? '${date(start)} - ${date(end.subtract(const Duration(days: 1)))}'
          : period == 2
              ? '${months[start.month - 1]} ${start.year}'
              : '${start.year}';
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final cs = theme.colorScheme;
    return Scaffold(
        appBar: AppBar(title: const Text('Insights')),
        body: SafeArea(
            child: ListView(padding: const EdgeInsets.fromLTRB(16, 16, 16, 32), children: [
          Text('My Activity',
              style: GoogleFonts.spaceGrotesk(fontSize: 28, fontWeight: FontWeight.w800, color: cs.onSurface)),
          const SizedBox(height: 18),
          Row(
              children: List.generate(
                  4,
                  (i) => Expanded(
                      child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 2),
                          child: ChoiceChip(
                              label: Center(
                                  child: Text(
                                      ['Day', 'Week', 'Month', 'Year'][i])),
                              selected: period == i,
                              showCheckmark: false,
                              selectedColor: cs.primary,
                              backgroundColor: theme.extension<MasarColors>()!.surfaceContainer,
                              labelStyle: theme.textTheme.labelLarge?.copyWith(color: period == i ? cs.onPrimary : cs.secondary),
                              side: BorderSide.none,
                              onSelected: (_) => setState(() {
                                    period = i;
                                    offset = 0;
                                    _range();
                                  })))))),
          const SizedBox(height: 18),
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            IconButton(
                onPressed: () => setState(() {
                      offset--;
                      _range();
                    }),
                icon: const Icon(Icons.chevron_left)),
            Flexible(child: Text(heading, textAlign: TextAlign.center)),
            IconButton(
                onPressed: offset == 0
                    ? null
                    : () => setState(() {
                          offset++;
                          _range();
                        }),
                icon: const Icon(Icons.chevron_right))
          ]),
          if (uid == null)
            const Text('Sign in to view your steps.')
          else
            StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
                key: ValueKey('$uid/$period/$offset'),
                stream: stream,
                builder: (context, snapshot) {
                  return StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
                    key: ValueKey(uid),
                    stream: huntStream,
                    builder: (context, hunts) => ListenableBuilder(
                      listenable: service,
                      builder: (context, _) => FutureBuilder<
                              Map<DateTime, int>>(
                          future: service.totals(
                              uid!, snapshot.data?.docs ?? [], start, end,
                              hunts: hunts.data?.docs ?? []),
                          builder: (context, result) {
                            if (!result.hasData) {
                              return result.hasError
                                  ? const Text('Unable to read step history.')
                                  : const Center(
                                      child: CircularProgressIndicator());
                            }
                            return Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  if (hunts.hasError || hunts.data?.metadata.isFromCache == true || snapshot.hasError ||
                                      snapshot.data?.metadata.isFromCache ==
                                          true)
                                    const Text(
                                        'Cloud history may be incomplete. Showing available saved data.'),
                                  if (hunts.connectionState == ConnectionState.waiting || snapshot.connectionState ==
                                      ConnectionState.waiting)
                                    const LinearProgressIndicator(),
                                  _chart(context, result.data!)
                                ]);
                          })));
                }),
          const SizedBox(height: 20),
          Text('Footwear steps and completed AR hunts. Hunt steps are grouped by completion time.',
              textAlign: TextAlign.center, style: theme.textTheme.bodySmall),
        ])));
  }

  Widget _chart(BuildContext context, Map<DateTime, int> data) {
    final theme = Theme.of(context);
    final cs = theme.colorScheme;
    final count = period == 0
        ? 24
        : period == 1
            ? 7
            : period == 2
                ? DateTime(start.year, start.month + 1, 0).day
                : 12;
    final values = List<int>.filled(count, 0);
    final daily = <String, int>{};
    for (final e in data.entries) {
      final d = e.key.toLocal();
      if (d.isBefore(start) || !d.isBefore(end)) continue;
      final i = period == 0
          ? d.hour
          : period == 1
              ? (d.weekday % 7)
              : period == 2
                  ? d.day - 1
                  : d.month - 1;
      if (i >= 0 && i < count) values[i] += e.value;
      final k = '${d.year}-${d.month}-${d.day}';
      daily[k] = (daily[k] ?? 0) + e.value;
    }
    final total = values.fold<int>(0, (a, b) => a + b);
    final rhythm = List<int>.filled(4, 0);
    for (final e in data.entries) {
      final h = e.key.toLocal().hour;
      rhythm[h >= 6 && h < 12
          ? 0
          : h >= 12 && h < 17
              ? 1
              : h >= 17 && h < 22
                  ? 2
                  : 3] += e.value;
    }
    final peak = rhythm.indexOf(rhythm.fold<int>(0, (a, b) => a > b ? a : b));
    final names = [
      'Early Explorer',
      'Daytime Adventurer',
      'Sunset Walker',
      'Late-hour Walker'
    ];
    final times = ['morning', 'afternoon', 'evening', 'night'];
    final max = values.fold<int>(1, (a, b) => a > b ? a : b);
    final best = daily.values.fold<int>(0, (a, b) => a > b ? a : b);
    final now = DateTime.now();
    final last =
        now.isBefore(end) ? now : end.subtract(const Duration(seconds: 1));
    final elapsed = DateTime.utc(last.year, last.month, last.day)
            .difference(DateTime.utc(start.year, start.month, start.day))
            .inDays +
        1;
    String label(int i) => period == 0
        ? '$i:00'
        : period == 1
            ? days[i]
            : period == 2
                ? '${i + 1}'
                : months[i];
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      if (period == 0 && offset == 0) ...[
        Card(
          margin: EdgeInsets.zero,
          color: cs.primary,
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Row(children: [
              Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text("TODAY'S STEPS", style: theme.textTheme.labelMedium?.copyWith(color: cs.onPrimary)),
                const SizedBox(height: 8),
                Text(fmt(total), style: GoogleFonts.spaceGrotesk(fontSize: 38, fontWeight: FontWeight.w800, color: cs.onPrimary)),
                if (goal > 0) Text('of ${fmt(goal)} steps', style: theme.textTheme.bodySmall?.copyWith(color: cs.onPrimary)),
                TextButton(onPressed: _editGoal, style: TextButton.styleFrom(foregroundColor: cs.onPrimary, padding: EdgeInsets.zero), child: Text(goal == 0 ? 'Set optional goal' : 'Edit goal')),
              ])),
              if (goal > 0) ...[
                const SizedBox(width: 12),
                SizedBox(width: 88, height: 88, child: Stack(alignment: Alignment.center, children: [
                  SizedBox.expand(child: CircularProgressIndicator(value: (total / goal).clamp(0.0, 1.0).toDouble(), strokeWidth: 7, color: cs.onPrimary, backgroundColor: cs.onPrimary.withValues(alpha: .25))),
                  Text('${(total / goal * 100).clamp(0, 100).round()}%', style: GoogleFonts.spaceGrotesk(fontSize: 19, fontWeight: FontWeight.w700, color: cs.onPrimary)),
                ])),
              ],
            ]),
          ),
        ),
        const SizedBox(height: 12),
      ],
      Card(
          margin: EdgeInsets.zero,
          elevation: 0,
          child: Padding(
              padding: const EdgeInsets.all(18),
              child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(selected == null ? 'TOTAL STEPS' : label(selected!),
                        style: theme.textTheme.labelMedium),
                    Text(
                        '${fmt(selected == null ? total : values[selected!])} steps',
                        style: GoogleFonts.spaceGrotesk(fontSize: 26, color: cs.primary, fontWeight: FontWeight.w700)),
                    const SizedBox(height: 24),
                    Text(fmt(max), style: theme.textTheme.labelSmall),
                    SizedBox(
                        height: 155,
                        child: Row(
                            crossAxisAlignment: CrossAxisAlignment.end,
                            children: List.generate(
                                count,
                                (i) => Expanded(
                                    child: GestureDetector(
                                        behavior: HitTestBehavior.opaque,
                                        onTap: () => setState(() => selected =
                                            selected == i ? null : i),
                                        child: Align(
                                            alignment: Alignment.bottomCenter,
                                            child: AnimatedContainer(
                                                duration: const Duration(
                                                    milliseconds: 250),
                                                margin:
                                                    const EdgeInsets.symmetric(
                                                        horizontal: 2),
                                                height: values[i] == 0
                                                    ? 2
                                                    : 150 * values[i] / max,
                                                decoration: BoxDecoration(
                                                    color: values[i] == 0
                                                        ? cs.outlineVariant
                                                        : selected == i
                                                            ? cs.primary
                                                            : cs.primary.withValues(
                                                                alpha:
                                                                    selected ==
                                                                            null
                                                                        ? 0.75
                                                                        : 0.35),
                                                    borderRadius:
                                                        BorderRadius.circular(
                                                            4))))))))),
                    const Divider(),
                    SizedBox(height: 20, child: Row(children: List.generate(count, (i) {
                      final show = period == 1 || (period == 0 && i % 6 == 0) || (period == 2 && i % 7 == 0) || (period == 3 && i % 3 == 0);
                      return Expanded(child: show ? OverflowBox(maxWidth: 42, child: Text(period == 0 ? '$i' : label(i), textAlign: TextAlign.center, style: theme.textTheme.labelSmall)) : const SizedBox.shrink());
                    }))),
                    const SizedBox(height: 24),
                    if (total == 0)
                      const Text(
                          'No recorded steps for this period. Connect your footwear to start.')
                    else ...[
                      const Divider(),
                      Row(children: [
                        Expanded(
                            child: _stat(
                                context,
                                period == 0 ? 'Peak hourly steps' : 'Daily average',
                                period == 0
                                    ? max
                                    : (total / (elapsed > 0 ? elapsed : 1))
                                        .round())),
                        Expanded(
                            child: _stat(context, period == 0 ? 'Active hours' : 'Best day in period', period == 0 ? values.where((v) => v > 0).length : best))
                      ])
                    ],
                  ]))),
      if (total > 0) ...[
        const SizedBox(height: 12),
        Card(
            margin: EdgeInsets.zero,
            child: Padding(
                padding: const EdgeInsets.all(18),
                child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SectionHeader(title: period == 0 ? 'Activity by time of day' : 'Your walking style'),
                      if (period != 0 && daily.length >= 3 && rhythm.where((v) => v == rhythm[peak]).length == 1) ...[
                        Icon(
                            [
                              Icons.wb_sunny_outlined,
                              Icons.wb_sunny_outlined,
                              Icons.wb_sunny_outlined,
                              Icons.nights_stay_outlined
                            ][peak],
                            color: cs.primary,
                            size: 36),
                        Text(names[peak],
                            style: theme.textTheme.headlineSmall
                                ?.copyWith(color: cs.primary)),
                        Text(
                            '${(rhythm[peak] * 100 / total).round()}% of recorded steps in the ${times[peak]}.'),
                      ] else if (period != 0)
                        Text(daily.length < 3 ? 'Walking style appears after three recorded days in this period.' : 'Your activity is balanced across your busiest times.'),
                      for (var i = 0; i < 4; i++) ...[
                        const SizedBox(height: 10),
                        Text(
                            '${times[i]} · ${(rhythm[i] * 100 / total).round()}%'),
                        MasarProgressBar(value: rhythm[i] / total, height: 7)
                      ],
                    ]))),
      ],
    ]);
  }

  Widget _stat(BuildContext context, String label, int value) =>
      Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Text(label, style: Theme.of(context).textTheme.bodySmall),
        Text(fmt(value), style: GoogleFonts.spaceGrotesk(fontSize: 20, fontWeight: FontWeight.w700, color: Theme.of(context).colorScheme.onSurface))
      ]);
}
