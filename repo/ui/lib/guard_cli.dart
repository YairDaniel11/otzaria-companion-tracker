import 'dart:convert';
import 'dart:io';

class ProgramInfo {
  final String name;
  final String path;
  final String reason;
  final double memoryMb;

  ProgramInfo({
    required this.name,
    required this.path,
    this.reason = '',
    this.memoryMb = 0,
  });

  factory ProgramInfo.fromJson(Map<String, dynamic> j) => ProgramInfo(
        name: (j['name'] ?? '') as String,
        path: (j['path'] ?? '') as String,
        reason: (j['reason'] ?? '') as String,
        memoryMb: ((j['memoryMb'] ?? 0) as num).toDouble(),
      );
}

/// Asks the guard exe (which has all the Windows-specific logic) for program lists.
class GuardCli {
  static String? _findGuard() {
    final env = Platform.environment['OTZARIA_GUARD_PATH'];
    if (env != null && File(env).existsSync()) return env;
    final dir = File(Platform.resolvedExecutable).parent;
    for (final p in [
      '${dir.parent.path}\\OtzariaCompanionGuard.exe',
      '${dir.path}\\OtzariaCompanionGuard.exe',
    ]) {
      if (File(p).existsSync()) return p;
    }
    return null;
  }

  /// mode: running | installed | related
  static Future<List<ProgramInfo>> list(String mode) async {
    final guard = _findGuard();
    if (guard == null) {
      throw Exception('לא נמצא הקובץ OtzariaCompanionGuard.exe ליד תוכנת ההגדרות');
    }
    final tmp = File(
        '${Directory.systemTemp.path}\\oct_${DateTime.now().microsecondsSinceEpoch}.json');
    try {
      await Process.run(guard, ['--list-$mode', '--out', tmp.path]);
      if (!await tmp.exists()) throw Exception('הסריקה לא החזירה תוצאה');
      final data = jsonDecode(await tmp.readAsString()) as List;
      return data
          .map((e) => ProgramInfo.fromJson(e as Map<String, dynamic>))
          .toList();
    } finally {
      try {
        if (await tmp.exists()) await tmp.delete();
      } catch (_) {}
    }
  }
}
