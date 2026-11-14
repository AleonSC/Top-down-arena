#!/bin/sh
# Usage: Tools/EnemyForge/package.sh [out-dir]   Builds the single-file Enemy Forge app: EnemyForge.exe. On Windows it opens
# its own app window (WebView2, the Edge engine in Windows 10/11; its DLLs are inside the exe) and quits when it's closed;
# with no WebView2, or on Mac/Linux (mono EnemyForge.exe), it opens a browser window instead. Needs curl and unzip the
# first time (fetches the WebView2 NuGet package into out/).
set -e
cd "$(dirname "$0")"
OUT="${1:-out}"
mkdir -p "$OUT" out
OUT="$(cd "$OUT" && pwd)"
WV=1.0.4191.47
P="out/webview2-$WV"
if [ ! -f "$P/lib/net462/Microsoft.Web.WebView2.WinForms.dll" ]; then
  rm -rf "$P"; mkdir -p "$P"
  curl -sSLf --retry 4 -o "$P/p.nupkg" "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$WV/microsoft.web.webview2.$WV.nupkg"
  (cd "$P" && unzip -qo p.nupkg 'lib/net462/*' 'runtimes/*/native/WebView2Loader.dll' && rm p.nupkg)
fi
# Build against .NET Framework 4.8's own reference assemblies (not mono's, which have extra methods Windows lacks).
REF="out/net48-ref"
if [ ! -f "$REF/build/.NETFramework/v4.8/mscorlib.dll" ]; then
  rm -rf "$REF"; mkdir -p "$REF"
  curl -sSLf --retry 4 -o "$REF/r.nupkg" "https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/1.0.3/microsoft.netframework.referenceassemblies.net48.1.0.3.nupkg"
  (cd "$REF" && unzip -qo r.nupkg 'build/.NETFramework/v4.8/*' && rm r.nupkg)
fi
FW="$REF/build/.NETFramework/v4.8"
CORE="$P/lib/net462/Microsoft.Web.WebView2.Core.dll"; WF="$P/lib/net462/Microsoft.Web.WebView2.WinForms.dll"
rm -f "$OUT/EnemyForge.exe"
mcs -langversion:7.2 -nologo -optimize+ -target:winexe -define:WINDOW -nostdlib -noconfig -lib:"$FW" -lib:"$FW/Facades" \
  -r:mscorlib.dll -r:System.dll -r:System.Core.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Runtime.dll -r:"$CORE" -r:"$WF" \
  -resource:web/index.html,index.html -resource:"$CORE",wv2/Microsoft.Web.WebView2.Core.dll -resource:"$WF",wv2/Microsoft.Web.WebView2.WinForms.dll \
  -resource:"$P/runtimes/win-x64/native/WebView2Loader.dll",wv2/x64/WebView2Loader.dll \
  -resource:"$P/runtimes/win-x86/native/WebView2Loader.dll",wv2/x86/WebView2Loader.dll \
  -resource:"$P/runtimes/win-arm64/native/WebView2Loader.dll",wv2/arm64/WebView2Loader.dll \
  -out:"$OUT/EnemyForge.exe" Server.cs Window.cs ../../Assets/Scripts/Core/*.cs 2>&1 | grep -v warning || true
[ -f "$OUT/EnemyForge.exe" ] || exit 1
echo "Built $OUT/EnemyForge.exe"
