@echo off
setlocal
for /f "usebackq delims=" %%A in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$l = Join-Path '%~dp0' '..\Despliegue\Load-PsForceConfig.ps1'; if (-not (Test-Path $l)) { $l = Join-Path '%~dp0' '..\Load-PsForceConfig.ps1' }; $c = & $l; if ($c.PostgreSQL.PsqlExe) { $bin = Split-Path -Parent $c.PostgreSQL.PsqlExe; Write-Output ('CREATEDB_EXE=' + (Join-Path $bin 'createdb.exe')) }; Write-Output ('PG_HOST=' + $c.PostgreSQL.Host); Write-Output ('PG_PORT=' + $c.PostgreSQL.Port); Write-Output ('PG_USER=' + $c.PostgreSQL.User); Write-Output ('PG_DB=' + $c.PostgreSQL.Database); Write-Output ('PG_ENC=' + $c.PostgreSQL.ClientEncoding);"`) do (
    set "%%A"
)

if "%CREATEDB_EXE%"=="" set "CREATEDB_EXE=createdb.exe"
if "%PG_HOST%"=="" set "PG_HOST=localhost"
if "%PG_PORT%"=="" set "PG_PORT=5433"
if "%PG_USER%"=="" set "PG_USER=postgres"
if "%PG_DB%"=="" set "PG_DB=repredisl_api"
if "%PG_ENC%"=="" set "PG_ENC=UTF8"

"%CREATEDB_EXE%" -h %PG_HOST% -p %PG_PORT% -U %PG_USER% -E %PG_ENC% --template=template0 %PG_DB%