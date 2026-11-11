#!/bin/sh
# Usage: Tools/WaveLab/package.sh [out-dir]   Builds a ready-to-run Wave Lab zip (WaveLab.exe + web page + launchers).
# On Windows the .exe runs as is (the built-in .NET Framework); Mac/Linux need mono (the launcher says how).
set -e
cd "$(dirname "$0")"
OUT="${1:-out}"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"
STAGE="$(mktemp -d)/WaveLab"
mkdir -p "$STAGE/web"
mcs -langversion:7.2 -nologo -optimize+ -out:"$STAGE/WaveLab.exe" Server.cs ../../Assets/Scripts/Core/*.cs 2>&1 | grep -v warning || true
[ -f "$STAGE/WaveLab.exe" ] || exit 1
cp web/index.html "$STAGE/web/"
cat > "$STAGE/Start-Windows.bat" <<'BAT'
@echo off
cd /d "%~dp0"
start "" http://localhost:8080
WaveLab.exe 8080
pause
BAT
cat > "$STAGE/Start-Mac-Linux.command" <<'SH'
#!/bin/sh
cd "$(dirname "$0")"
if ! command -v mono >/dev/null 2>&1; then
  echo "Mono is needed on Mac/Linux. Mac: brew install mono   Debian/Ubuntu: sudo apt-get install mono-runtime libmono-system-core4.0-cil libmono-system4.0-cil"
  exit 2
fi
( sleep 2; (xdg-open http://localhost:8080 || open http://localhost:8080) >/dev/null 2>&1 ) &
exec mono WaveLab.exe 8080
SH
chmod +x "$STAGE/Start-Mac-Linux.command"
cat > "$STAGE/README.txt" <<'TXT'
Wave Lab: play and bot-test wave designs in your browser.
Windows: double-click Start-Windows.bat (allow the firewall prompt if you want to play from a phone).
Mac/Linux: install mono once, then double-click (or run) Start-Mac-Linux.command.
It prints the phone address (same Wi-Fi) when it starts. Ctrl+C or close the window to stop.
TXT
rm -f "$OUT/WaveLab.zip"
(cd "$(dirname "$STAGE")" && zip -qr "$OUT/WaveLab.zip" WaveLab)
echo "Built $OUT/WaveLab.zip"
