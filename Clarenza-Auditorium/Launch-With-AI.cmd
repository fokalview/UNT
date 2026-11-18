@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Clarenza.ps1" -EnableAI
if errorlevel 1 pause
