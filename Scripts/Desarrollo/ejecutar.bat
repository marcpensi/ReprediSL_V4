@echo off
cd /d "%~dp0..\..\src\Frontend"
if not exist node_modules (
  echo Instalando dependencias del Frontend...
  call npm install
  if errorlevel 1 pause & exit /b 1
)
echo Iniciando servidor de desarrollo Vite...
call npm run dev
pause
