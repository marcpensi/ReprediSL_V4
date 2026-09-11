# Manual de Instalación en Cliente - ReprediSL V4

**Ruta Oficial de Despliegue:** `C:\pensi\psforce\`  
**Versión:** 4.9.2  
**Fecha:** 11-09-2026  

---

## 1. Estructura de Directorios Desplegada

```text
C:\pensi\psforce\
│
├── INICIAR_TODO.bat              <- Arranca el Centro de Control y todos los servicios
├── DETENER_TODO.bat              <- Cierra limpiamente todos los procesos
├── CREAR_ACCESOS_DIRECTOS.bat    <- Crea acceso en Escritorio e Inicio de Windows (Startup)
├── LEEME_INSTALACION.txt         <- Instrucciones rápidas locales
│
├── ReprediTrayDaemon\            <- Centro de Control en segundo plano (Bandeja del sistema)
│   ├── ReprediTrayDaemon.exe     <- Ejecutable principal (.NET 10 WinForms)
│   ├── *.dll                     <- Dependencias y runtime de Npgsql
│   ├── config.json               <- Configuración central de rutas, puertos y servicios
│   ├── postgrest.exe             <- API REST de PostgREST
│   └── postgrest.conf            <- Parámetros de conexión a PostgreSQL
│
├── Sync\                         <- Sincronizador de Pedidos a Access ERP
│   ├── sincronizador.exe         <- Ejecutable nativo (.NET 10 Console)
│   ├── config.json               <- Configuración de sincronización y cadencia
│   ├── SincronizarPedidosEntrantes.ps1 <- Script canónico de inserción DAO/OleDb
│   └── ARRANCAR_SINCRONIZADOR.bat <- Lanzador manual individual
│
├── Caddy\                        <- Proxy Inverso HTTPS (Solo si se requiere certificado SSL local)
│   ├── caddy.exe                 <- Binario de Caddy
│   ├── Caddyfile                 <- Regla de proxy a api.repredisl.com -> 127.0.0.1:3000
│   └── ARRANCAR_CADDY.bat        <- Lanzador manual individual
│
├── Logs\                         <- Registros históricos de actividad y errores
│   ├── sync_progress.log         <- Actividad en tiempo real de sincronización
│   └── sync_errors.log           <- Registro detallado de incidencias
│
└── Backup\                       <- Copias de seguridad de la base de datos
    └── HACER_BACKUP_ACCESS.bat   <- Script para generar copia fechada de gestion.mdb
```

---

## 2. Requisitos Previos en la Máquina Cliente

1. **Sistema Operativo:** Windows 10 / Windows 11 o Windows Server 2019/2022 (x64).
2. **.NET 10 Runtime:**
   - [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) para `ReprediTrayDaemon.exe`.
3. **Microsoft Access / Driver OLEDB:**
   - Microsoft Office Access (2016/2019/365) o **Microsoft Access Database Engine 2010/2016 Redistributable** (`Microsoft.ACE.OLEDB.12.0` o `16.0`).
   - Ubicación de la base de datos ERP: `C:\pensi\psgestw\e0012026\gestion.mdb` (o la configurada en `config.json`).
4. **PostgreSQL 16:**
   - Servicio local en puerto `5432` con base de datos `repredisl_api` y usuario `postgres`.

---

## 3. Configuración (`ReprediTrayDaemon\config.json`)

El archivo `C:\pensi\psforce\ReprediTrayDaemon\config.json` permite personalizar todas las rutas sin necesidad de recompilar:

```json
{
  "appName": "ReprediSL Centro de Control",
  "version": "4.9.2",
  "baseDir": "C:\\pensi\\psforce",
  "accessMdbPath": "C:\\pensi\\psgestw\\e0012026\\gestion.mdb",
  "syncExecutable": "..\\Sync\\sincronizador.exe",
  "syncScript": "..\\Sync\\SincronizarPedidosEntrantes.ps1",
  "syncIntervalSeconds": 3,
  "caddyExecutable": "..\\Caddy\\caddy.exe",
  "caddyfile": "..\\Caddy\\Caddyfile",
  "postgrestExecutable": "postgrest.exe",
  "postgrestConfig": "postgrest.conf",
  "logsDir": "..\\Logs",
  "backupDir": "..\\Backup",
  "postgresHost": "127.0.0.1",
  "postgresPort": 5432,
  "postgresDatabase": "repredisl_api",
  "postgresUser": "postgres",
  "postgrestPort": 3000,
  "caddyPort": 80,
  "autoStartServices": true
}
```

---

## 4. Pasos de Puesta en Marcha

1. **Copiar la carpeta:**
   Copiar el contenido de `C:\pensi\psforce\` al equipo cliente (o descomprimir `dist_cliente\ReprediSL`).
2. **Crear accesos directos:**
   Hacer clic derecho sobre `C:\pensi\psforce\CREAR_ACCESOS_DIRECTOS.bat` y seleccionar **Ejecutar como Administrador**.  
   Esto colocará el acceso directo en el Escritorio y en la carpeta de Inicio de Windows.
3. **Iniciar el sistema:**
   Hacer doble clic en `INICIAR_TODO.bat` o en el acceso del Escritorio.
   - El icono de Repredi aparecerá en la barra de tareas junto al reloj.
   - Al hacer doble clic sobre el icono se abrirá el panel de telemetría y control.
   - Los 5 servicios quedarán monitorizados con sus indicadores de salud (🟢).

---

## 5. Mantenimiento y Backups

- **Logs de Auditoría:** Consultar la carpeta `C:\pensi\psforce\Logs\` para inspeccionar pedidos sincronizados (`sync_progress.log`) o incidencias (`sync_errors.log`).
- **Copias de Seguridad:** Ejecutar `C:\pensi\psforce\Backup\HACER_BACKUP_ACCESS.bat` antes de actualizaciones masivas para generar una copia fechada instantánea de `gestion.mdb`.