# Generates the Flutter Windows runner (windows/ folder) and applies small patches.
# Used by CI and for local development:  pwsh ./scripts/prepare-ui.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ui   = Join-Path $root 'ui'
$bak  = Join-Path $root '.ui-bak'

Push-Location $ui
try {
  if (Test-Path $bak) { Remove-Item $bak -Recurse -Force }
  New-Item $bak -ItemType Directory | Out-Null
  Copy-Item lib $bak -Recurse
  Copy-Item pubspec.yaml $bak

  flutter create --platforms=windows --project-name otzaria_companion_settings --org il.otzaria.companion .
  if ($LASTEXITCODE -ne 0) { throw 'flutter create failed' }

  # restore our sources (flutter create may touch them)
  Remove-Item lib -Recurse -Force
  Copy-Item (Join-Path $bak 'lib') . -Recurse
  Copy-Item (Join-Path $bak 'pubspec.yaml') . -Force
  Remove-Item test -Recurse -Force -ErrorAction SilentlyContinue

  # app icon
  Copy-Item (Join-Path $root 'assets\app.ico') 'windows\runner\resources\app_icon.ico' -Force

  # Hebrew window title (as C++ unicode escapes) + window size
  $main = 'windows\runner\main.cpp'
  $c = Get-Content $main -Raw
  $title = '\u05de\u05e2\u05e7\u05d1 \u05ea\u05d5\u05db\u05e0\u05d5\u05ea \u05e0\u05dc\u05d5\u05d5\u05ea \u05dc\u05d0\u05d5\u05e6\u05e8\u05d9\u05d0'
  $c = $c.Replace('L"otzaria_companion_settings"', 'L"' + $title + '"')
  $c = $c -replace 'Size size\(1280, 720\)', 'Size size(980, 720)'
  Set-Content $main $c -Encoding UTF8
}
finally {
  if (Test-Path $bak) { Remove-Item $bak -Recurse -Force }
  Pop-Location
}
