@echo off
cd /d "%~dp0"
dotnet publish CajaLocal.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
pause
