import 'package:flutter/material.dart';

import 'guard_cli.dart';

class ProgramPick {
  final String path;
  final String name;
  ProgramPick(this.path, this.name);
}

/// Loads a program list from the guard and lets the user tick what to add.
class PickerDialog extends StatefulWidget {
  final String mode; // running | installed | related
  final String title;
  final Set<String> existing; // lower-cased paths already in the list

  const PickerDialog({
    super.key,
    required this.mode,
    required this.title,
    required this.existing,
  });

  @override
  State<PickerDialog> createState() => _PickerDialogState();
}

class _PickerDialogState extends State<PickerDialog> {
  List<ProgramInfo>? _all;
  String? _error;
  String _query = '';
  final Set<String> _selected = {};

  @override
  void initState() {
    super.initState();
    GuardCli.list(widget.mode).then((l) {
      if (mounted) setState(() => _all = l);
    }).catchError((Object e) {
      if (mounted) setState(() => _error = e.toString());
    });
  }

  @override
  Widget build(BuildContext context) {
    final cs = Theme.of(context).colorScheme;
    final items = (_all ?? [])
        .where((p) => !widget.existing.contains(p.path.toLowerCase()))
        .where((p) =>
            _query.isEmpty ||
            p.name.toLowerCase().contains(_query) ||
            p.path.toLowerCase().contains(_query))
        .toList();

    Widget body;
    if (_error != null) {
      body = Center(child: Text(_error!));
    } else if (_all == null) {
      body = const Center(child: CircularProgressIndicator());
    } else if (items.isEmpty) {
      body = Center(
        child: Text(
          widget.mode == 'related'
              ? 'לא נמצאו תוכנות קשורות. כשאוצריא פתוחה הסריקה מדויקת יותר.'
              : 'לא נמצאו תוכנות',
          style: TextStyle(color: cs.onSurfaceVariant),
        ),
      );
    } else {
      body = ListView.builder(
        itemCount: items.length,
        itemBuilder: (context, i) {
          final p = items[i];
          final sub = [
            if (p.reason.isNotEmpty) p.reason,
            if (p.memoryMb > 0) '${p.memoryMb.toStringAsFixed(0)} MB',
          ].join(' · ');
          return CheckboxListTile(
            value: _selected.contains(p.path),
            onChanged: (v) => setState(
                () => v == true ? _selected.add(p.path) : _selected.remove(p.path)),
            title: Text(p.name),
            subtitle: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (sub.isNotEmpty) Text(sub),
                Text(p.path,
                    textDirection: TextDirection.ltr,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(fontSize: 11, color: cs.outline)),
              ],
            ),
            controlAffinity: ListTileControlAffinity.leading,
          );
        },
      );
    }

    return AlertDialog(
      title: Text(widget.title),
      content: SizedBox(
        width: 640,
        height: 480,
        child: Column(
          children: [
            TextField(
              decoration: const InputDecoration(
                prefixIcon: Icon(Icons.search),
                hintText: 'חיפוש לפי שם או נתיב',
                border: OutlineInputBorder(),
                isDense: true,
              ),
              onChanged: (v) => setState(() => _query = v.trim().toLowerCase()),
            ),
            const SizedBox(height: 8),
            Expanded(child: body),
          ],
        ),
      ),
      actions: [
        TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ביטול')),
        FilledButton(
          onPressed: _selected.isEmpty
              ? null
              : () {
                  final byPath = {for (final p in _all!) p.path: p};
                  Navigator.pop(
                    context,
                    _selected
                        .map((s) => ProgramPick(s, byPath[s]?.name ?? s))
                        .toList(),
                  );
                },
          child: Text('הוסף (${_selected.length})'),
        ),
      ],
    );
  }
}
