import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';

class AppEntry {
  String path;
  String name;
  String args;
  String workingDir;
  bool enabled;

  AppEntry({
    required this.path,
    required this.name,
    this.args = '',
    this.workingDir = '',
    this.enabled = true,
  });

  factory AppEntry.fromJson(Map<String, dynamic> j) => AppEntry(
        path: (j['path'] ?? '') as String,
        name: (j['name'] ?? '') as String,
        args: (j['args'] ?? '') as String,
        workingDir: (j['workingDir'] ?? '') as String,
        enabled: (j['enabled'] ?? true) as bool,
      );

  Map<String, dynamic> toJson() => {
        'path': path,
        'name': name,
        'args': args,
        'workingDir': workingDir,
        'enabled': enabled,
      };
}

class Config {
  String targetProcess = 'otzaria';
  bool autostart = false;
  bool showTray = true;
  String theme = 'system'; // light | dark | system
  bool leaveRunning = true;
  int gracefulTimeoutSeconds = 5;
  int maxRestarts = 3;
  bool learning = true;
  List<AppEntry> apps = [];

  Config();

  factory Config.fromJson(Map<String, dynamic> j) {
    final c = Config();
    c.targetProcess = (j['targetProcess'] ?? c.targetProcess) as String;
    c.autostart = (j['autostart'] ?? c.autostart) as bool;
    c.showTray = (j['showTray'] ?? c.showTray) as bool;
    c.theme = (j['theme'] ?? c.theme) as String;
    c.leaveRunning = (j['leaveRunning'] ?? c.leaveRunning) as bool;
    c.gracefulTimeoutSeconds =
        (j['gracefulTimeoutSeconds'] ?? c.gracefulTimeoutSeconds) as int;
    c.maxRestarts = (j['maxRestarts'] ?? c.maxRestarts) as int;
    c.learning = (j['learning'] ?? c.learning) as bool;
    c.apps = ((j['apps'] ?? []) as List)
        .map((e) => AppEntry.fromJson(e as Map<String, dynamic>))
        .toList();
    return c;
  }

  Map<String, dynamic> toJson() => {
        'targetProcess': targetProcess,
        'autostart': autostart,
        'showTray': showTray,
        'theme': theme,
        'leaveRunning': leaveRunning,
        'gracefulTimeoutSeconds': gracefulTimeoutSeconds,
        'maxRestarts': maxRestarts,
        'learning': learning,
        'apps': apps.map((a) => a.toJson()).toList(),
      };
}

/// Reads/writes %APPDATA%\OtzariaCompanionTracker\config.json.
/// The background guard polls this file and applies changes by itself.
class ConfigStore extends ChangeNotifier {
  Config config = Config();

  static File get file {
    final appData = Platform.environment['APPDATA'] ?? '.';
    return File('$appData\\OtzariaCompanionTracker\\config.json');
  }

  Future<void> load() async {
    try {
      if (await file.exists()) {
        config = Config.fromJson(
            jsonDecode(await file.readAsString()) as Map<String, dynamic>);
      }
    } catch (_) {
      config = Config();
    }
  }

  Future<void> _save() async {
    final f = file;
    await f.parent.create(recursive: true);
    final tmp = File('${f.path}.tmp');
    await tmp.writeAsString(
        const JsonEncoder.withIndent('  ').convert(config.toJson()));
    await tmp.rename(f.path);
  }

  /// Apply a change, notify the UI and persist.
  void update(void Function(Config c) change) {
    change(config);
    notifyListeners();
    _save();
  }
}
