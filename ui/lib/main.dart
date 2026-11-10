import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'config.dart';
import 'home_page.dart';
import 'settings_page.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final store = ConfigStore();
  await store.load();
  runApp(CompanionApp(store: store));
}

ThemeData _buildTheme(Brightness b) {
  const seed = Color(0xFF7F5218); // Otzaria-like warm brown
  var scheme = ColorScheme.fromSeed(seedColor: seed, brightness: b);
  if (b == Brightness.light) {
    scheme = scheme.copyWith(
      surface: const Color(0xFFF6EDE3),
      primary: const Color(0xFF7F5218),
      primaryContainer: const Color(0xFFFADDBB),
      onPrimaryContainer: const Color(0xFF3F2706),
    );
  }
  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    fontFamily: 'Segoe UI',
    scaffoldBackgroundColor: scheme.surface,
    cardTheme: CardThemeData(
      elevation: 0,
      color: b == Brightness.light ? const Color(0xFFFFF8F3) : null,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: BorderSide(color: scheme.outlineVariant),
      ),
    ),
  );
}

class CompanionApp extends StatelessWidget {
  final ConfigStore store;
  const CompanionApp({super.key, required this.store});

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: store,
      builder: (context, _) {
        final mode = switch (store.config.theme) {
          'light' => ThemeMode.light,
          'dark' => ThemeMode.dark,
          _ => ThemeMode.system,
        };
        return MaterialApp(
          title: 'מעקב תוכנות נלוות לאוצריא',
          debugShowCheckedModeBanner: false,
          locale: const Locale('he'),
          supportedLocales: const [Locale('he')],
          localizationsDelegates: const [
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          builder: (context, child) =>
              Directionality(textDirection: TextDirection.rtl, child: child!),
          theme: _buildTheme(Brightness.light),
          darkTheme: _buildTheme(Brightness.dark),
          themeMode: mode,
          home: Shell(store: store),
        );
      },
    );
  }
}

class Shell extends StatefulWidget {
  final ConfigStore store;
  const Shell({super.key, required this.store});

  @override
  State<Shell> createState() => _ShellState();
}

class _ShellState extends State<Shell> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            selectedIndex: _index,
            labelType: NavigationRailLabelType.all,
            backgroundColor: Theme.of(context).brightness == Brightness.light
                ? const Color(0xFFF2E6D8)
                : null,
            onDestinationSelected: (i) => setState(() => _index = i),
            destinations: const [
              NavigationRailDestination(
                icon: Icon(Icons.apps_outlined),
                selectedIcon: Icon(Icons.apps),
                label: Text('תוכנות'),
              ),
              NavigationRailDestination(
                icon: Icon(Icons.settings_outlined),
                selectedIcon: Icon(Icons.settings),
                label: Text('הגדרות'),
              ),
            ],
          ),
          const VerticalDivider(width: 1),
          Expanded(
            child: IndexedStack(
              index: _index,
              children: [
                HomePage(store: widget.store),
                SettingsPage(store: widget.store),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
