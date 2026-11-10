# מעקב תוכנות נלוות לאוצריא

כלי קטן ל-Windows 10/11 שמפעיל את התוכנות הנלוות (ה"גב" של התוספים) רק כשאוצריא פתוחה, ומכבה אותן כשהיא נסגרת - כדי שלא יתפסו זיכרון כל היום.

## איך זה בנוי

| רכיב | טכנולוגיה | תפקיד | זיכרון |
|---|---|---|---|
| `guard/` - **OtzariaCompanionGuard.exe** | C# + Native AOT, ללא framework | רץ ברקע: מזהה את `otzaria.exe`, מפעיל/מכבה תוכנות, אייקון מגש, סריקות | כמה מגה בודדים |
| `ui/` - **otzaria_companion_settings.exe** | Flutter (Material 3, RTL) | חלון הגדרות. נפתח רק לפי דרישה ומסתיים לגמרי כשסוגרים אותו | 0 כשסגור |

התקשורת ביניהם היא קובץ `%APPDATA%\OtzariaCompanionTracker\config.json` (השומר מזהה שינוי בעצמו), והחלון מבקש מהשומר רשימות תוכנות דרך שורת פקודה (`--list-running`, `--list-installed`, `--list-related`).

### פתיחת ההגדרות
* מאייקון המגש (לחיצה או תפריט ימני).
* מתפריט "התחל": הקיצור מריץ `OtzariaCompanionGuard.exe --show`. אם המעקב כבר רץ - הוא רק פותח/מקדם את החלון הקיים ולא יוצר עותק נוסף. זה עובד גם כשאייקון המגש מוסתר בהגדרות.

### שלוש דרכי הבחירה
1. **תוכנות שרצות כרגע** - כל התהליכים הפעילים (מחוץ לתיקיית Windows).
2. **תוכנות מותקנות** - מתוך רישום Windows (Uninstall + App Paths).
3. **הקשורות לאוצריא** - תהליכי בן של אוצריא, תהליכים וקבצים בתיקיית אוצריא או עם שם דומה, ו"למידה": השומר זוכר אילו תוכנות עולות 20 שניות אחרי הפעלת אוצריא, ומציג אחרי שתי הפעלות.
4. בנוסף: בחירה ידנית של קובץ EXE.

### התנהגות
* אוצריא נפתחת -> כל תוכנה מסומנת מופעלת (אם אוצריא כבר רצה כשהמעקב עולה - מיד).
* אם תוכנה כבר רצה: נשארת, ואם "השאר תוכנה שכבר הייתה פועלת" כבוי - נסגרת גם היא בסוף.
* תוכנה שקורסת בזמן שאוצריא פתוחה -> עד 3 ניסיונות הפעלה מחדש (ניתן לשינוי).
* אוצריא נסגרת -> סגירה עדינה (`CloseMainWindow`), ואחרי זמן ההמתנה סגירה בכוח כולל תהליכי הבן.
* כל תוכנה רצה מתיקיית ה-EXE שלה (ניתן לשנות תיקיית עבודה ופרמטרים ב"הגדרות מתקדמות" - רוב התוכנות לא צריכות).

## בנייה

### ב-GitHub (מומלץ)
דחיפה ל-`main` או הרצה ידנית של ה-Workflow בונה הכול ומעלה Artifacts: zip נייד + מתקין.
שחרור גרסה: `git tag v1.0.0 && git push --tags` - נוצר Release עם הקבצים.

### מקומית (Windows)
דרישות: .NET 8 SDK + "Desktop development with C++" (ל-AOT), Flutter SDK, ו-Inno Setup אם רוצים מתקין.
```powershell
dotnet publish guard/OtzariaCompanionGuard.csproj -c Release -r win-x64 -o out/guard
./scripts/prepare-ui.ps1
cd ui; flutter pub get; flutter build windows --release; cd ..
```
בפיתוח: `$env:OTZARIA_GUARD_PATH="C:\...\OtzariaCompanionGuard.exe"; flutter run -d windows`.

### מבנה התיקייה המוכנה
```
OtzariaCompanionTracker/
  OtzariaCompanionGuard.exe
  ui/otzaria_companion_settings.exe  (+ קבצי Flutter)
```
בגרסה הניידת מפעילים את `OtzariaCompanionGuard.exe --show`.

## קובץ ההגדרות
```json
{
  "targetProcess": "otzaria",
  "autostart": false,
  "showTray": true,
  "theme": "system",
  "leaveRunning": true,
  "gracefulTimeoutSeconds": 5,
  "maxRestarts": 3,
  "learning": true,
  "apps": [{ "path": "C:\\...\\helper.exe", "name": "helper", "args": "", "workingDir": "", "enabled": true }]
}
```
