Write-Host "Publishing self-contained Windows x64 release..." -ForegroundColor Cyan
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src/FigmaDesktopApp/FigmaDesktopApp.csproj -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true

$cleanDist = "dist\FigmaDesktopApp"
if (Test-Path $cleanDist) { Remove-Item $cleanDist -Recurse -Force }
New-Item -ItemType Directory -Path "$cleanDist\app" -Force | Out-Null

Copy-Item -Path "src\FigmaDesktopApp\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish\*" -Destination "$cleanDist\app" -Recurse -Force
New-Item -ItemType Directory -Path "$cleanDist\plugin" -Force | Out-Null
Copy-Item -Path "plugin\*" -Destination "$cleanDist\plugin" -Recurse -Force

Write-Host "Compiling root launcher executable with app icon..." -ForegroundColor Cyan
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:winexe /win32icon:"src\FigmaDesktopApp\Assets\AppIcon.ico" /out:"$cleanDist\FigmaDesktopApp.exe" "scripts\Launcher.cs"

@"
==================================================
  Gemini Studio for Figma - Quick Start Guide
==================================================

1. Run the App:
   Double-click "FigmaDesktopApp.exe" in this folder.

2. Connect to Figma (One-time setup):
   - In Figma Desktop, right-click the canvas -> Plugins -> Development -> Import plugin from manifest...
   - Select the "plugin\manifest.json" file included in this folder.
   - Click "Run".

3. Design with AI:
   Type your prompt in Gemini Studio (or tap the microphone for voice dictation),
   and watch native Auto-Layout components appear live on your Figma canvas!
"@ | Out-File -FilePath "$cleanDist\QUICK_START.txt" -Encoding utf8

Write-Host "Compressing to FigmaDesktopApp.zip..." -ForegroundColor Cyan
Compress-Archive -Path "$cleanDist\*" -DestinationPath "FigmaDesktopApp.zip" -Force
Write-Host "Done! FigmaDesktopApp.zip is ready for distribution." -ForegroundColor Green
