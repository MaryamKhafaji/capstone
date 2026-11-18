import 'package:cloud_firestore/cloud_firestore.dart';
import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/material.dart';
import '../services/freej_service.dart';

/// Additive Freej membership screen. No changes to steps or rewards.
class FreejScreen extends StatefulWidget {
  const FreejScreen({super.key});

  @override
  State<FreejScreen> createState() => _FreejScreenState();
}

class _FreejScreenState extends State<FreejScreen> {
  final _service = FreejService();
  final _name = TextEditingController();
  final _code = TextEditingController();
  bool _busy = false;

  @override
  void dispose() {
    _name.dispose();
    _code.dispose();
    super.dispose();
  }

  Future<void> _perform(Future<void> Function() action) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      await action();
    } on FirebaseException catch (e) {
      if (mounted) _message(e.message ?? e.code);
    } catch (e) {
      if (mounted) _message('Unable to complete request: $e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _message(String message) {
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  Future<bool> _confirmLeave() async =>
      await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Leave Freej?'),
          content: const Text('You will no longer be part of this Freej.'),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Leave')),
          ],
        ),
      ) ?? false;

  @override
  Widget build(BuildContext context) {
    if (FirebaseAuth.instance.currentUser == null) {
      return const Scaffold(body: Center(child: Text('Sign in to use Freej.')));
    }
    return Scaffold(
      appBar: AppBar(title: const Text('Freej')),
      body: StreamBuilder<DocumentSnapshot<Map<String, dynamic>>>(
        stream: _service.myMembership(),
        builder: (context, snapshot) {
          if (snapshot.hasError) return Center(child: Text('Cannot load Freej: ${snapshot.error}'));
          if (!snapshot.hasData) return const Center(child: CircularProgressIndicator());
          final membership = snapshot.data!.data();
          if (membership == null) return _notInFreej();
          final id = membership['freejId'];
          if (id is! String || id.isEmpty) return const Center(child: Text('Invalid membership record.'));
          return _inFreej(id);
        },
      ),
    );
  }

  Widget _notInFreej() => ListView(
    padding: const EdgeInsets.all(20),
    children: [
      const Text('Walk together. Compete as a Freej.', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold)),
      const SizedBox(height: 24),
      const Text('Create a Freej', style: TextStyle(fontWeight: FontWeight.bold)),
      TextField(controller: _name, maxLength: 40, decoration: const InputDecoration(labelText: 'Freej name')),
      FilledButton(
        onPressed: _busy ? null : () => _perform(() async {
          final result = await _service.create(_name.text);
          if (mounted) _message('Created! Invite code: ${result['inviteCode']}');
        }),
        child: const Text('Create Freej'),
      ),
      const SizedBox(height: 32),
      const Text('Join a Freej', style: TextStyle(fontWeight: FontWeight.bold)),
      TextField(controller: _code, textCapitalization: TextCapitalization.characters,
          maxLength: 10, decoration: const InputDecoration(labelText: '10-character invite code')),
      OutlinedButton(
        onPressed: _busy ? null : () => _perform(() => _service.join(_code.text.trim())),
        child: const Text('Join Freej'),
      ),
      if (_busy) const Padding(padding: EdgeInsets.all(12), child: Center(child: CircularProgressIndicator())),
    ],
  );

  Widget _inFreej(String id) => StreamBuilder<DocumentSnapshot<Map<String, dynamic>>>(
    stream: _service.freej(id),
    builder: (context, snapshot) {
      if (snapshot.hasError) return Center(child: Text('Cannot load team: ${snapshot.error}'));
      if (!snapshot.hasData) return const Center(child: CircularProgressIndicator());
      final data = snapshot.data!.data();
      if (data == null) return const Center(child: Text('Freej not found.'));
      final captain = data['captainUid'] == FirebaseAuth.instance.currentUser?.uid;
      final count = (data['memberCount'] as num?)?.toInt() ?? 0;
      return ListView(padding: const EdgeInsets.all(20), children: [
        Text('${data['name'] ?? 'Freej'}', style: const TextStyle(fontSize: 26, fontWeight: FontWeight.bold)),
        const SizedBox(height: 12),
        SelectableText('Invite code: ${data['inviteCode'] ?? ''}'),
        const SizedBox(height: 8),
        Text('$count member${count == 1 ? '' : 's'}'),
        if (count < 2) const Padding(
          padding: EdgeInsets.only(top: 12),
          child: Text('At least 2 members are needed to qualify for weekly rankings.'),
        ),
        const SizedBox(height: 24),
        const Text('Members', style: TextStyle(fontWeight: FontWeight.bold)),
        StreamBuilder<QuerySnapshot<Map<String, dynamic>>>(
          stream: _service.members(id),
          builder: (context, members) {
            if (members.hasError) return Text('Cannot load members: ${members.error}');
            if (!members.hasData) return const LinearProgressIndicator();
            return Column(children: members.data!.docs.map((doc) {
              final isCaptain = doc.id == data['captainUid'];
              return ListTile(
                title: Text(doc.id == FirebaseAuth.instance.currentUser?.uid ? 'You' : 'Member ${doc.id.substring(0, doc.id.length < 6 ? doc.id.length : 6)}'),
                subtitle: isCaptain ? const Text('Captain') : null,
                trailing: captain && !isCaptain
                    ? TextButton(
                        onPressed: _busy ? null : () => _perform(() => _service.transferCaptain(doc.id)),
                        child: const Text('Make captain'),
                      )
                    : null,
              );
            }).toList());
          },
        ),
        const SizedBox(height: 24),
        OutlinedButton(
          onPressed: _busy ? null : () async {
            if (await _confirmLeave()) await _perform(() => _service.leave());
          },
          child: Text(captain && count > 1 ? 'Transfer captain before leaving' : 'Leave Freej'),
        ),
        if (_busy) const Center(child: CircularProgressIndicator()),
      ]);
    },
  );
}
