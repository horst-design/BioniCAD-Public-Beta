@echo off
cd /d "%~dp0"
title BioniCAD Server
start "BioniCAD Server" cmd /k lattice-fraktal.exe serve --url http://localhost:5151/
timeout /t 2 /nobreak >nul
start "" http://localhost:5151/
