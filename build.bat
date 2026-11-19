@echo off
setlocal
echo Building SkyTweaks...

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Error: .NET Framework 64-bit C# compiler not found at "%CSC%".
    exit /b 1
)

"%CSC%" /nologo /optimize+ /target:winexe /win32manifest:"%~dp0app.manifest" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:System.ServiceProcess.dll /r:System.Web.Extensions.dll /out:"%~dp0SkyTweaks.exe" "%~dp0src\Program.cs" "%~dp0src\Core\Models.cs" "%~dp0src\Core\NativeInterop.cs" "%~dp0src\Core\HardwareDetector.cs" "%~dp0src\Core\BackupAndRollbackEngine.cs" "%~dp0src\Core\TweakCatalog.cs" "%~dp0src\Core\MemoryCleanerEngine.cs" "%~dp0src\Core\GameBoosterEngine.cs" "%~dp0src\Core\GameConfigOptimizer.cs" "%~dp0src\Core\SystemTelemetryEngine.cs" "%~dp0src\Core\TweakerController.cs" "%~dp0src\UI\Theme.cs" "%~dp0src\UI\TacticalControls.cs" "%~dp0src\UI\MainForm.cs"
if errorlevel 1 (
    echo Build failed.
    exit /b 1
)

echo Build succeeded: "%~dp0SkyTweaks.exe"
exit /b 0
