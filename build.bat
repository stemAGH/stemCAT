@echo off
rem Builds stemCAT.exe with the C# compiler that ships inside Windows (.NET Framework 4.x).
rem Nothing is downloaded or installed.
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe from .NET Framework 4. It is part of Windows 10/11 - turn it on in
  echo "Turn Windows features on or off" ^> ".NET Framework 4.8 Advanced Services".
  pause
  exit /b 1
)
taskkill /im stemCAT.exe /f >nul 2>&1
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:stemCAT.exe /win32icon:stemCAT.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll src\*.cs
if errorlevel 1 (
  echo.
  echo Build failed - see the messages above.
  pause
  exit /b 1
)
echo Built stemCAT.exe - starting your cat...
start "" "%~dp0stemCAT.exe"
