@echo off
REM verify.bat — Quality gate wrapper for Command Prompt
powershell -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
