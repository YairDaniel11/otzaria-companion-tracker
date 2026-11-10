import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';

import 'config.dart';
import 'picker_dialog.dart';

class HomePage extends StatelessWidget {
  final ConfigStore store;
  const HomePage({super.key, required this.store});

  static String _baseName(String path) {
    final f = path.split(RegExp(r'[\\/]')).last;
    return f.toLowerCase().endsWith('.exe') ? f.substring(0, f.length - 4) : f;
  }

  void _addPrograms(Iterable<({String path, String name})> items) {
    store.update((c) {
      final have = c.apps.map((a) => a.path.toLowerCase()).toSet();
      for (final i in items) {
        if (have.add(i.path.toLowerCase())) {
          c.apps.add(AppEntry(path: i.path, name: i.name));
        }
      }
    });
  }

  Future<void> _add(BuildContext context) async {
    final choice = await showDialog<String>(
      context: context,
      builder: (c) => SimpleDialog(
        title: const Text('איך להוסיף תוכנות?'),
        children: [
          _option(c, 'running', Icons.memory, 'תוכנות שרצות כרגע',
              'רשימת התהליכים הפעילים במחשב'),
          _option(c, 'installed', Icons.inventory_2_outlined, 'תוכנות מותקנות',
              'סריקת התוכנות הרשומות בווינדוס'),
          _option(c, 'related', Icons.link, 'תוכנות הקשורות לאוצריא',
              'תהליכי בן, קבצים בתיקיית אוצריא ולמידה מהפעלות קודמות'),
          _option(c, 'manual', Icons.folder_open, 'בחירה ידנית של קובץ EXE',
              'עיון בתיקיות המחשב'),
        ],
      ),
    );
    if (choice == null || !context.mounted) return;

    if (choice == 'manual') {
      final r = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['exe'],
        allowMultiple: true,
        dialogTitle: 'בחר תוכנות להוספה',
      );
      if (r == null) return;
      _addPrograms(r.files
          .where((f) => f.path != null)
          .map((f) => (path: f.path!, name: _baseName(f.path!))));
      return;
    }

    final titles = {
      'running': 'תוכנות שרצות כרגע',
      'installed': 'תוכנות מותקנות',
      'related': 'תוכנות הקשורות לאוצריא',
    };
    final picked = await showDialog<List<ProgramPick>>(
      context: context,
      builder: (_) => PickerDialog(
        mode: choice,
        title: titles[choice]!,
        existing: store.config.apps.map((a) => a.path.toLowerCase()).toSet(),
      ),
    );
    if (picked != null) {
      _addPrograms(picked.map((p) => (path: p.path, name: p.name)));
    }
  }

  static Widget _option(BuildContext c, String value, IconData icon,
      String title, String subtitle) {
    return SimpleDialogOption(
      onPressed: () => Navigator.pop(c, value),
      child: ListTile(
        leading: Icon(icon),
        title: Text(title),
        subtitle: Text(subtitle),
      ),
    );
  }

  Future<void> _edit(BuildContext context, AppEntry a) async {
    final name = TextEditingController(text: a.name);
    final args = TextEditingController(text: a.args);
    final dir = TextEditingController(text: a.workingDir);
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('הגדרות מתקדמות לתוכנה'),
        content: SizedBox(
          width: 460,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                  controller: name,
                  decoration: const InputDecoration(labelText: 'שם לתצוגה')),
              const SizedBox(height: 12),
              TextField(
                controller: args,
                textDirection: TextDirection.ltr,
                decoration: const InputDecoration(
                  labelText: 'פרמטרים להפעלה (אופציונלי)',
                  helperText: 'לדוגמה: --port 5000',
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: dir,
                textDirection: TextDirection.ltr,
                decoration: const InputDecoration(
                  labelText: 'תיקיית עבודה (אופציונלי)',
                  helperText: 'ריק = התיקייה שבה נמצא קובץ ה-EXE',
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
              onPressed: () => Navigator.pop(c, false),
              child: const Text('ביטול')),
          FilledButton(
              onPressed: () => Navigator.pop(c, true),
              child: const Text('שמור')),
        ],
      ),
    );
    if (ok == true) {
      store.update((_) {
        a.name = name.text.trim().isEmpty ? a.name : name.text.trim();
        a.args = args.text.trim();
        a.workingDir = dir.text.trim();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final apps = store.config.apps;
        final cs = Theme.of(context).colorScheme;
        return Scaffold(
          backgroundColor: Colors.transparent,
          floatingActionButton: FloatingActionButton.extended(
            onPressed: () => _add(context),
            icon: const Icon(Icons.add),
            label: const Text('הוסף תוכנות'),
          ),
          body: Padding(
            padding: const EdgeInsets.fromLTRB(32, 28, 32, 0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('תוכנות נלוות',
                    style: Theme.of(context)
                        .textTheme
                        .headlineLarge
                        ?.copyWith(fontWeight: FontWeight.w700)),
                const SizedBox(height: 6),
                Text(
                  'התוכנות המסומנות יופעלו כשאוצריא נפתחת ויכבו כשהיא נסגרת.',
                  style: TextStyle(color: cs.onSurfaceVariant),
                ),
                const SizedBox(height: 20),
                Expanded(
                  child: apps.isEmpty
                      ? Center(
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(Icons.layers_outlined,
                                  size: 64, color: cs.outline),
                              const SizedBox(height: 12),
                              const Text('עדיין לא נבחרו תוכנות נלוות'),
                              const SizedBox(height: 4),
                              Text('לחץ על "הוסף תוכנות" כדי להתחיל',
                                  style: TextStyle(color: cs.onSurfaceVariant)),
                            ],
                          ),
                        )
                      : ListView.separated(
                          padding: const EdgeInsets.only(bottom: 96),
                          itemCount: apps.length,
                          separatorBuilder: (_, __) =>
                              const SizedBox(height: 10),
                          itemBuilder: (context, i) {
                            final a = apps[i];
                            return Card(
                              child: ListTile(
                                contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 16, vertical: 6),
                                leading: CircleAvatar(
                                  backgroundColor: cs.primaryContainer,
                                  child: Icon(Icons.terminal,
                                      color: cs.onPrimaryContainer),
                                ),
                                title: Text(a.name,
                                    style: const TextStyle(
                                        fontWeight: FontWeight.w600)),
                                subtitle: Text(
                                  a.args.isEmpty ? a.path : '${a.path}  ${a.args}',
                                  textDirection: TextDirection.ltr,
                                  textAlign: TextAlign.right,
                                  overflow: TextOverflow.ellipsis,
                                ),
                                trailing: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    IconButton(
                                      tooltip: 'הגדרות מתקדמות',
                                      icon: const Icon(Icons.tune),
                                      onPressed: () => _edit(context, a),
                                    ),
                                    IconButton(
                                      tooltip: 'הסר מהרשימה',
                                      icon: const Icon(Icons.delete_outline),
                                      onPressed: () => store
                                          .update((c) => c.apps.remove(a)),
                                    ),
                                    Switch(
                                      value: a.enabled,
                                      onChanged: (v) =>
                                          store.update((_) => a.enabled = v),
                                    ),
                                  ],
                                ),
                              ),
                            );
                          },
                        ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
