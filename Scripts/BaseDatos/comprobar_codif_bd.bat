@echo off
setlocal
for /f "usebackq delims=" %%A in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$l = Join-Path '%~dp0' '..\Despliegue\Load-PsForceConfig.ps1'; if (-not (Test-Path $l)) { $l = Join-Path '%~dp0' '..\Load-PsForceConfig.ps1' }; $c = & $l; Write-Output ('PSQL_EXE=' + $c.PostgreSQL.PsqlExe); Write-Output ('PG_HOST=' + $c.PostgreSQL.Host); Write-Output ('PG_PORT=' + $c.PostgreSQL.Port); Write-Output ('PG_USER=' + $c.PostgreSQL.User); Write-Output ('PG_DB=' + $c.PostgreSQL.Database);"`) do (
    set "%%A"
)

if "%PSQL_EXE%"=="" set "PSQL_EXE=psql.exe"
if "%PG_HOST%"=="" set "PG_HOST=localhost"
if "%PG_PORT%"=="" set "PG_PORT=5433"
if "%PG_USER%"=="" set "PG_USER=postgres"
if "%PG_DB%"=="" set "PG_DB=repredisl_api"

"%PSQL_EXE%" -h %PG_HOST% -p %PG_PORT% -U %PG_USER% -d %PG_DB% -c "SHOW server_encoding; SHOW client_encoding;"