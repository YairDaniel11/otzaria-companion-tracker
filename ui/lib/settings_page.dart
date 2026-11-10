import 'package:flutter/material.dart';

import 'config.dart';

class SettingsPage extends StatelessWidget {
  final ConfigStore store;
  const SettingsPage({super.key, required this.store});

  Widget _section(BuildContext context, String title, List<Widget> children) {
    final cs = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.only(bottom: 22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(bottom: 8, right: 4),
            child: Text(title,
                style: TextStyle(
                    color: cs.primary,
                    fontWeight: FontWeight.w700,
                    fontSize: 16)),
          ),
          Card(child: Column(children: children)),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final c = store.config;
        return ListView(
          padding: const EdgeInsets.fromLTRB(32, 28, 32, 32),
          children: [
            Text('הגדרות',
                style: Theme.of(context)
                    .textTheme
                    .headlineLarge
                    ?.copyWith(fontWeight: FontWeight.w700)),
            const SizedBox(height: 20),
            _section(context, 'מראה', [
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                child: Row(
                  children: [
                    const Expanded(
                      child: Text('ערכת נושא', style: TextStyle(fontSize: 16)),
                    ),
                    SegmentedButton<String>(
                      showSelectedIcon: false,
                      segments: const [
                        ButtonSegment(value: 'light', label: Text('בהיר')),
                        ButtonSegment(value: 'system', label: Text('מערכת')),
                        ButtonSegment(value: 'dark', label: Text('כהה')),
                      ],
                      selected: {c.theme},
                      onSelectionChanged: (s) =>
                          store.update((x) => x.theme = s.first),
                    ),
                  ],
                ),
              ),
            ]),
            _section(context, 'הפעלה', [
              SwitchListTile(
                title: const Text('הפעלה אוטומטית עם ווינדוס'),
                subtitle: const Text('מומלץ - כדי שהמעקב יעבוד בלי שתצטרך לפתוח אותו'),
                value: c.autostart,
                onChanged: (v) => store.update((x) => x.autostart = v),
              ),
              const Divider(height: 1),
              SwitchListTile(
                title: const Text('הצג אייקון בשורת המשימות'),
                subtitle: const Text(
                    'כשכבוי, המעקב ממשיך לפעול ברקע ואפשר לפתוח את ההגדרות מתפריט "התחל"'),
                value: c.showTray,
                onChanged: (v) => store.update((x) => x.showTray = v),
              ),
            ]),
            _section(context, 'התנהגות', [
              SwitchListTile(
                title: const Text('השאר תוכנה שכבר הייתה פועלת'),
                subtitle: const Text(
                    'אם תוכנה נלווית כבר רצה כשאוצריא נפתחה - לא לכבות אותה כשאוצריא נסגרת'),
                value: c.leaveRunning,
                onChanged: (v) => store.update((x) => x.leaveRunning = v),
              ),
              const Divider(height: 1),
              ListTile(
                title: const Text('זמן המתנה לסגירה עדינה'),
                subtitle: const Text('אחריו התוכנה נסגרת בכוח, כולל תהליכי הבן שלה'),
                trailing: DropdownButton<int>(
                  value: [3, 5, 10, 20].contains(c.gracefulTimeoutSeconds)
                      ? c.gracefulTimeoutSeconds
                      : 5,
                  items: [3, 5, 10, 20]
                      .map((s) =>
                          DropdownMenuItem(value: s, child: Text('$s שניות')))
                      .toList(),
                  onChanged: (v) =>
                      store.update((x) => x.gracefulTimeoutSeconds = v ?? 5),
                ),
              ),
              const Divider(height: 1),
              ListTile(
                title: const Text('הפעלה מחדש אחרי קריסה'),
                subtitle: const Text('מספר ניסיונות מקסימלי לכל תוכנה'),
                trailing: DropdownButton<int>(
                  value: c.maxRestarts.clamp(0, 5).toInt(),
                  items: [0, 1, 2, 3, 4, 5]
                      .map((s) => DropdownMenuItem(
                          value: s, child: Text(s == 0 ? 'ללא' : '$s')))
                      .toList(),
                  onChanged: (v) =>
                      store.update((x) => x.maxRestarts = v ?? 3),
                ),
              ),
            ]),
            _section(context, 'מתקדם', [
              ListTile(
                title: const Text('שם התהליך של אוצריא'),
                subtitle: const Text('ברירת מחדל: otzaria (ללא .exe)'),
                trailing: SizedBox(
                  width: 180,
                  child: TextFormField(
                    key: ValueKey(c.targetProcess),
                    initialValue: c.targetProcess,
                    textDirection: TextDirection.ltr,
                    decoration: const InputDecoration(isDense: true),
                    onFieldSubmitted: (v) => store.update((x) =>
                        x.targetProcess = v.trim().isEmpty ? 'otzaria' : v.trim()),
                  ),
                ),
              ),
              const Divider(height: 1),
              SwitchListTile(
                title: const Text('למידה אוטומטית'),
                subtitle: const Text(
                    'המעקב מזהה אילו תוכנות עולות בסמוך להפעלת אוצריא, ומציע אותן ברשימת "הקשורות לאוצריא"'),
                value: c.learning,
                onChanged: (v) => store.update((x) => x.learning = v),
              ),
            ]),
          ],
        );
      },
    );
  }
}
