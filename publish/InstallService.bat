@ECHO OFF

REM The following directory is for .NET 4.0
set DOTNETFX2=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319
set PATH=%PATH%;%DOTNETFX2%

echo Installing FolderCopyService Win Service...
echo ---------------------------------------------------
C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil "%~dp0WindowsServiceFolderCopy.exe"
echo ---------------------------------------------------
net start FolderCopyService
pause
echo Done.