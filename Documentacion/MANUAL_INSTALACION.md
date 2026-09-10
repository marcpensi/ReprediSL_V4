# Manual de Instalación y Despliegue - ReprediSL_V4

**Versión del Sistema:** 4.3.0  
**Fecha:** Septiembre 2026  
**Ámbito:** Entorno Local de Desarrollo, Servidor ERP Windows y Producción Web

---

## 1. Introducción y Arquitectura del Sistema

`ReprediSL_V4` es una plataforma comercial híbrida *Offline-First* diseñada para la fuerza de ventas de REPREDISL. Conecta la gestión local del ERP (Microsoft Access / PsGest) con una aplicación web progresiva (PWA) de alta velocidad mediante sincronización bidireccional y una API REST desacoplada.

El sistema se compone de 4 subsistemas principales:

```
┌─────────────────────────┐          ┌─────────────────────────┐
│     ERP Local PsGest    │          │  ReprediTrayDaemon.exe  │
│  (gestion.mdb + VBA)    │◄────────►│   (C# WinForms .NET 10) │
└────────────┬────────────┘   ODBC   └────────────┬────────────┘
             │                                    │
             ▼ (Sincronización DDL/DML)           ▼ (Monitoreo/Logs)
┌─────────────────────────┐          ┌─────────────────────────┐
│      PostgreSQL 16      │◄────────►│      PostgREST 16       │
│    (Base de Datos)      │          │      (API REST Local)   │
└─────────────────────────┘          └────────────┬────────────┘
                                                  │ HTTPS / JSON
                                                  ▼
                                     ┌─────────────────────────┐
                                     │   Frontend React PWA    │
                                     │ (IndexedDB v2 + jsPDF)  │
                                     └─────────────────────────┘
```

---

## 2. Requisitos Previos del Sistema

### Hardware Mínimo Recomendado
- **Procesador:** 4 núcleos (x64)
- **Memoria RAM:** 8 GB (16 GB recomendado para entornos combinados con PostgreSQL y compilación)
- **Disco:** 5 GB de espacio libre (SSD recomendado)
- **Sistema Operativo:** Windows 10 (versión 21H2 o superior), Windows 11, o Windows Server 2019/2022

### Software y Dependencias Obligatorias
| Componente | Versión Mínima | Propósito |
| :--- | :--- | :--- |
| **.NET SDK** | `10.0` (o .NET 10 Desktop Runtime) | Ejecución y compilación del demonio de bandeja `ReprediTrayDaemon.exe` |
| **Node.js & npm** | Node `v18.0+` o `v20.0+` / npm `v9.0+` | Entorno de ejecución y empaquetado del Frontend React + Vite |
| **PostgreSQL** | `16.x` | Base de datos relacional central para la sincronización y la API |
| **Controlador psqlODBC** | PostgreSQL ODBC Driver (32-bit / 64-bit) | Conectividad entre Microsoft Access (DAO/ODBC) y PostgreSQL |
| **Microsoft Access** | Access 2010 o superior / Access Database Engine | Motor ERP local para `gestion.mdb` |
| **PowerShell** | PowerShell 5.1 / PowerShell 7+ | Ejecución de scripts de automatización en `Scripts\` |

---

## 3. Paso 1: Clonación y Estructura del Repositorio

1. Clonar el repositorio oficial:
   ```bash
   git clone https://github.com/marcpensi/ReprediSL_V4.git D:\programacio\repredi\ReprediSL_V4
   cd D:\programacio\repredi\ReprediSL_V4
   ```

2. Estructura fundamental de directorios:
   - `src\Frontend\`: Código fuente de la PWA (React 18, Vite, IndexedDB v2).
   - `src\API\`: Binario de PostgREST y archivos de configuración (`postgrest.conf`).
   - `src\Daemon\`: Código fuente y binarios del demonio nativo C# WinForms (.NET 10).
   - `src\Access\`: Archivos del ERP (`gestion.mdb`), módulo VBA `modActBdApi.bas` y logs.
   - `Scripts\`: Suite de automatización (Desarrollo, BaseDatos, Migración, Diagnóstico).
   - `Documentacion\`: Manuales técnicos, especificaciones y registros de estado.

---

## 4. Paso 2: Instalación y Configuración de PostgreSQL 16

1. **Instalar PostgreSQL:** Descargar e instalar PostgreSQL 16 desde [postgresql.org](https://www.postgresql.org/download/windows/). Durante la instalación, anotar la contraseña del superusuario `postgres` y conservar el puerto por defecto (`5432`).
2. **Crear la Base de Datos:**
   Abrir `psql` o `pgAdmin` y crear la base de datos:
   ```sql
   CREATE DATABASE "repredisl_api";
   ```
3. **Crear Roles y Esquema de PostgREST:**
   Conectar a la base de datos `repredisl_api` y ejecutar:
   ```sql
   -- Crear esquema api
   CREATE SCHEMA IF NOT EXISTS api;

   -- Crear rol anónimo para consultas de lectura pública / autenticada
   CREATE ROLE web_anon NOLOGIN;
   GRANT USAGE ON SCHEMA api TO web_anon;

   -- Crear rol authenticator
   CREATE ROLE authenticator NOINHERIT LOGIN PASSWORD 'Marc';
   GRANT web_anon TO authenticator;

   -- Conceder permisos de lectura sobre futuras tablas en api
   ALTER DEFAULT PRIVILEGES IN SCHEMA api GRANT SELECT ON TABLES TO web_anon;
   ```
4. **Instalar y Configurar el Driver ODBC (psqlODBC):**
   - Descargar e instalar el driver oficial **psqlODBC** correspondiente a la arquitectura de Microsoft Office (normalmente 32-bit para Access clásico, o 64-bit).
   - En Windows, abrir **Orígenes de datos ODBC (ODBC Data Sources)**.
   - En la pestaña **DSN de sistema**, agregar un nuevo origen usando `PostgreSQL Unicode`:
     - **Data Source:** `PostgreSQL35W` (o el nombre configurado en `modActBdApi.bas`)
     - **Database:** `repredisl_api`
     - **Server:** `localhost`
     - **Port:** `5432`
     - **User Name:** `postgres` o `authenticator`

---

## 5. Paso 3: Configuración y Puesta en Marcha de la API (PostgREST)

1. **Configurar el archivo de PostgREST:**
   Revisar o editar el archivo `src\API\postgrest.conf`:
   ```ini
   db-uri = "postgres://authenticator:Marc@localhost:5432/repredisl_api"
   db-schemas = "api"
   db-anon-role = "web_anon"

   server-host = "127.0.0.1"
   server-port = 3000

   server-cors-allowed-origins = "https://pedidos.repredisl.com,http://localhost:5173"
   ```

2. **Arrancar PostgREST en Desarrollo:**
   Hacer doble clic en:
   ```cmd
   Scripts\Desarrollo\ARRANCAR_POSTGREST.bat
   ```
   O ejecutar por línea de comandos:
   ```cmd
   cd D:\programacio\repredi\ReprediSL_V4\src\API
   postgrest.exe postgrest.conf
   ```

3. **Verificar la Conectividad de la API:**
   Ejecutar el script de diagnóstico:
   ```cmd
   Scripts\Desarrollo\PROBAR_API_LOCAL.bat
   ```
   O verificar directamente en el navegador accediendo a:
   `http://127.0.0.1:3000/clientes?limit=1`

4. *(Opcional en Servidor)* **Instalar como Servicio Windows persistente:**
   Utilizar [NSSM (Non-Sucking Service Manager)](https://nssm.cc/):
   ```cmd
   nssm install ReprediPostgrest "D:\programacio\repredi\ReprediSL_V4\src\API\postgrest.exe" "postgrest.conf"
   nssm set ReprediPostgrest AppDirectory "D:\programacio\repredi\ReprediSL_V4\src\API"
   nssm set ReprediPostgrest Start SERVICE_AUTO_START
   nssm start ReprediPostgrest
   ```

---

## 6. Paso 4: Instalación y Despliegue del Frontend (React 18 PWA)

1. **Instalar Dependencias de Node:**
   Navegar al directorio del frontend e instalar las dependencias:
   ```bash
   cd D:\programacio\repredi\ReprediSL_V4\src\Frontend
   npm install
   ```

2. **Ejecutar Suite de Pruebas Unitarias:**
   Validar que todos los tests pasen satisfactoriamente (5/5 tests):
   ```bash
   npm test
   ```

3. **Iniciar Servidor de Desarrollo Local:**
   ```bash
   npm run dev
   ```
   Abrir en el navegador la URL indicada (habitualmente `http://localhost:5173`).

4. **Compilar para Producción:**
   Para empaquetar la aplicación optimizada para el servidor web:
   ```bash
   npm run build
   ```
   *(O ejecutar el script `Scripts\Desarrollo\BUILD_PRODUCCION.bat`)*.
   Los archivos estáticos optimizados se generarán en `src\Frontend\dist\`.

5. **Variables de Entorno (`.env`):**
   - En desarrollo: `VITE_API_URL=http://localhost:3000`
   - En producción: `VITE_API_URL=https://api.repredisl.com` (apuntando al dominio público seguro).

---

## 7. Paso 5: Configuración de Microsoft Access y Módulo de Sincronización

1. **Ubicación de la Base de Datos ERP:**
   El demonio y los scripts buscan `gestion.mdb` en las siguientes ubicaciones prioritarias:
   - `D:\programacio\repredi\ReprediSL_V4\src\Access\E0012026\gestion.mdb`
   - `D:\programacio\repredi\ReprediSL_V4\src\Access\gestion.mdb`
   - `C:\Pensi\PsGest\E0012026\gestion.mdb`

2. **Actualizar Consultas e Inyectar Código VBA:**
   Para aplicar automáticamente las 5 consultas API (`QryClientesApi`, `QryProductosApi`, `QryTarifasApi`, etc.) e inyectar `modActBdApi.bas`:
   ```cmd
   Scripts\Desarrollo\MIGRAR_Y_ACTUALIZAR_ACCESS.bat
   ```

3. **Ejecutar la Primera Exportación Masiva:**
   Para volcar todos los catálogos y tarifas desde Access hacia PostgreSQL:
   ```cmd
   Scripts\Desarrollo\EJECUTAR_EXPORTACION_POSTGRES.bat
   ```
   El proceso registrará el progreso paso a paso en `src\Access\sync_progress.log`.

---

## 8. Paso 6: Compilación y Ejecución del Demonio C# WinForms (.NET 10)

El demonio `ReprediTrayDaemon` reside en la bandeja del sistema de Windows junto al reloj, ofreciendo un panel moderno de telemetría, monitoreo de salud del pipeline y control de pedidos comerciales.

1. **Compilar el Demonio:**
   ```bash
   cd D:\programacio\repredi\ReprediSL_V4\src\Daemon\ReprediTrayDaemon
   dotnet build -c Release
   ```
   El binario ejecutable se ubica directamente en:
   `D:\programacio\repredi\ReprediSL_V4\src\Daemon\bin\ReprediTrayDaemon.exe`

2. **Arrancar el Demonio:**
   Hacer doble clic en:
   ```cmd
   Scripts\Desarrollo\ARRANCAR_DEMONIO_BANDEJA.bat
   ```
   El icono `🔄` aparecerá en la bandeja del sistema junto al reloj.

3. **Características y Operación del Demonio:**
   - **Doble clic en icono del reloj:** Muestra/oculta la consola principal.
   - **Conmutador de Modo (Segmented Pill Switch):** Permite alternar entre **Auto-Aceptar** (inserción directa en el ERP sin intervención) y **Confirmación Manual** (los pedidos quedan en espera para revisión).
   - **Diagrama de Pipeline:** Indica el estado en tiempo real de cada nodo (`PostgreSQL` ⇄ `PsSyncBridge` ⇄ `Access ERP`).
   - **4 Tarjetas Métricas:** Reloj de sincronización, Tiempo de actividad (*Uptime*), Pedidos procesados hoy y Pedidos pendientes.
   - **Botón "📋 Ver Pedidos":** Abre un visor detallado con todos los pedidos del día y el botón rápido `✅ Confirmar todos los pendientes`.
   - **Control de Ráfagas de Errores:** Alerta emergente en caso de registrarse fallos continuados (>3 incidencias en 60s) con opción de pausar o continuar en silencio.
   - **Detener el Demonio:** Menú contextual del botón derecho → `Salir`, o ejecutar:
     ```cmd
     Scripts\Desarrollo\PARAR_DEMONIO_BANDEJA.bat
     ```

4. **Configurar Arranque Automático al Iniciar Windows:**
   - Presionar `Win + R`, escribir `shell:startup` y pulsar Enter.
   - Crear un acceso directo a `Scripts\Desarrollo\ARRANCAR_DEMONIO_BANDEJA.bat` o a `src\Daemon\bin\ReprediTrayDaemon.exe` en dicha carpeta.

---

## 9. Paso 7: Checklist de Verificación Integral

Antes de dar por completada la instalación, comprobar cada uno de los siguientes puntos:

- [ ] **Pruebas Unitarias:** `npm test` en `src\Frontend` devuelve `5 pass, 0 fail`.
- [ ] **Compilación C#:** `dotnet build` en `src\Daemon\ReprediTrayDaemon` finaliza con `0 Advertencias, 0 Errores`.
- [ ] **Codificación C# (Regla Crítica):** Archivos `.cs` guardados en **UTF-8 con BOM** (verificable con `[System.IO.File]::ReadAllBytes`).
- [ ] **API Local Operativa:** `http://127.0.0.1:3000/clientes?limit=1` responde un JSON con datos válidos.
- [ ] **Sincronización Access:** El archivo `src\Access\sync_progress.log` reporta `Exportación finalizada con éxito`.
- [ ] **Demonio Residente:** `ReprediTrayDaemon.exe` visible en la bandeja del sistema, consumiendo ~25 MB de RAM.
- [ ] **Frontend Web:** La aplicación abre en el navegador, sincroniza el catálogo a IndexedDB y permite crear un pedido de prueba exportable a PDF.

---

## 10. Resolución de Problemas Frecuentes (Troubleshooting)

### 1. Error de conexión ODBC en Access ("No se encuentra el origen de datos...")
- **Causa:** El driver `psqlODBC` no coincide con la arquitectura de Access (32 bits vs 64 bits), o el nombre del DSN no coincide.
- **Solución:** Si Microsoft Office es de 32 bits, instalar `psqlODBC 32-bit` y crear el DSN en el Administrador ODBC de 32 bits (`C:\Windows\SysWOW64\odbcad32.exe`).

### 2. PostgREST se cierra inmediatamente al arrancar
- **Causa:** Contraseña incorrecta en `db-uri`, PostgreSQL apagado o puerto 5432 inaccesible.
- **Solución:** Verificar que el servicio PostgreSQL esté activo en Windows (`services.msc`) y probar la conexión con `psql -U authenticator -d repredisl_api -h localhost`.

### 3. Emojis o caracteres extraños en el Demonio WinForms (`ðŸ—„ï¸` o `Ã±`)
- **Causa:** Archivo `MainForm.cs` guardado sin BOM en UTF-8 estándar o ANSI.
- **Solución:** Guardar el archivo en Visual Studio con `Archivo → Guardar con codificación → UTF-8 con firma (BOM)` (Codepage 65001).

### 4. Error de CORS en el navegador al consultar la API
- **Causa:** El origen de la aplicación web no figura en `server-cors-allowed-origins` de `postgrest.conf`.
- **Solución:** Añadir la URL del cliente (ej. `http://localhost:5173`) separada por comas en `server-cors-allowed-origins` y reiniciar PostgREST.

---

*Manual mantenido y validado bajo los estándares de [AGENTS.md](file:///d:/programacio/repredi/ReprediSL_V4/AGENTS.md).*
