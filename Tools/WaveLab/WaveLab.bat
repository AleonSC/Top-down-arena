@echo off
rem Double-click on Windows to launch Wave Lab. Needs Mono (https://www.mono-project.com/download/stable/).
cd /d "%~dp0"
set "PATH=%PATH%;C:\Program Files\Mono\bin;C:\Program Files (x86)\Mono\bin"
where mono >nul 2>nul || (echo Mono is not installed. Get it from https://www.mono-project.com/download/stable/ & pause & exit /b 1)
if not exist WaveLab.exe (
  echo Building, first run takes a few seconds...
  call mcs -langversion:7.2 -nologo -out:WaveLab.exe Server.cs ..\..\Assets\Scripts\Core\*.cs
  if not exist WaveLab.exe (pause & exit /b 1)
)
start "" http://localhost:8080
mono WaveLab.exe 8080
pause
