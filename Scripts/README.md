# Índice de Scripts - ReprediSL_V4

Este directorio contiene la suite de scripts utilitarios y de automatización para `ReprediSL_V4`, organizados por categorías:

## Categorías
- **Bootstrap:** Scripts de inicialización del entorno.
- **Desarrollo:** Scripts para ejecución y prueba local.
- **BaseDatos:** Scripts de gestión de esquemas SQL y exportación desde Access.
- **Migracion:** Utilidades de migración de datos.
- **Despliegue:** Scripts de empaquetado y despliegue a producción.
- **Mantenimiento:** Tareas de limpieza y mantenimiento.
- **Diagnostico:** Scripts de salud e inspección de servicios.
- **Utilidades:** Herramientas de soporte y demonio de la barra de tareas.

---

## Scripts Disponibles

### 1. [`DemonioBarraTareas.ps1`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Utilidades/DemonioBarraTareas.ps1) & [`ARRANCAR_DEMONIO_BANDEJA.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/ARRANCAR_DEMONIO_BANDEJA.bat)
- **Objetivo:** Demonio alojado en la bandeja del sistema (System Tray de Windows junto al reloj).
- **Características:**
  - **Sincronización en Tiempo Real:** Muestra el log en vivo (`Conectando a postgres ...`, `Actualizando Clientes (x de y) ...`).
  - **Alertas Escalonadas y Reintentos Configurables:** 
    - 1er aviso: Inmediato al recibir el pedido.
    - 2º aviso: A los 1 min si no se ha marcado como leído (configurable).
    - 3er aviso y siguientes: Cada 5 min si sigue sin leerse (configurable).
    - Máximo de avisos por pedido configurable (`MaxAvisosPorPedido = 3`).
  - **Monitoreo y Alertas en ROJO:**
    - Resaltado visual en **ROJO** para errores y **NARANJA** para advertencias en la consola en tiempo real (`RichTextBox`).
    - Guardado paralelo en log especial de incidencias (`src/Access/sync_errors.log`).
    - Modal emergente de revisión de incidencias (`[ 🚨 VER ERRORES (X) ]`).
    - Detección de ráfagas/incrementos elevados de errores (alertas de nivel crítico si ocurren 3 o más incidencias en 60 segundos).
  - **Inserción Automática en ERP PsGest:**
    - Creación e inserción de pedidos confirmados directamente en `RutaPsgest\E0012026\gestion.mdb`.
  - **Gestión de Registros:**
    - Exportación de registro a `.txt`/`.log`.
    - Impresión directa del registro.
    - Limpieza con pregunta y copia de seguridad previa.
  - **Herramienta de Detención de Emergencia:** Incluye [`PARAR_DEMONIO_BANDEJA.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/PARAR_DEMONIO_BANDEJA.bat) para forzar la detención limpia e instantánea de cualquier proceso demonio colgado o en ejecución.
- **Uso:** Hacer doble clic en `Scripts\Desarrollo\ARRANCAR_DEMONIO_BANDEJA.bat` para iniciar o `Scripts\Desarrollo\PARAR_DEMONIO_BANDEJA.bat` para detener.

### 2. [`EjecutarExportacionAccess.ps1`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/BaseDatos/EjecutarExportacionAccess.ps1) & [`EJECUTAR_EXPORTACION_POSTGRES.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/EJECUTAR_EXPORTACION_POSTGRES.bat)
- **Objetivo:** Ejecuta la rutina masiva VBA `ExportarTablas()` desde fuera de Access (sin necesidad de abrir Access a mano).

### 3. [`MigrarYActualizarAccess.ps1`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Migracion/MigrarYActualizarAccess.ps1) & [`MIGRAR_Y_ACTUALIZAR_ACCESS.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/MIGRAR_Y_ACTUALIZAR_ACCESS.bat)
- **Objetivo:** Copia la base origen a `bddestino.mdb`, genera las 5 consultas `Qry*Api` e inyecta [`modActBdApi.bas`](file:///d:/programacio/repredi/ReprediSL_V4/src/Access/modActBdApi.bas).

### 4. `ARRANCAR_POSTGREST.bat`
- **Objetivo:** Inicia el servidor PostgREST 16 empleando la configuración de `src/API/postgrest.conf`.

### 5. `BUILD_PRODUCCION.bat`
- **Objetivo:** Ejecuta la compilación de Vite en `src/Frontend`.

### 6. [`PUBLICAR_HOSTINGER.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Despliegue/PUBLICAR_HOSTINGER.bat) & [`PublicarHostingerMcp.mjs`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Despliegue/PublicarHostingerMcp.mjs)
- **Objetivo:** Empaqueta y despliega automáticamente el contenido de `src/Frontend/dist` en el subdominio `https://pedidos.repredisl.com` utilizando el servidor oficial MCP de Hostinger (`hostinger-hosting-mcp`).

### 7. [`ARRANCAR_CADDY.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/ARRANCAR_CADDY.bat) & [`PARAR_CADDY.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/PARAR_CADDY.bat)
- **Objetivo:** Inicia el proxy inverso Caddy Server para exponer `api.repredisl.com` con terminación HTTPS segura (puerto 443) y certificado Let's Encrypt automático, redirigiendo el tráfico a PostgREST (`127.0.0.1:3000`).

### 8. [`ACTUALIZAR_DNS_HOSTINGER.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Despliegue/ACTUALIZAR_DNS_HOSTINGER.bat) & [`ActualizarDnsApiHostinger.mjs`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Despliegue/ActualizarDnsApiHostinger.mjs)
- **Objetivo:** Actualiza automáticamente el registro DNS tipo A de `api.repredisl.com` en Hostinger con la IP pública actual.

### 9. [`INICIAR_SISTEMA_COMPLETO.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/INICIAR_SISTEMA_COMPLETO.bat) & [`ARRANCAR_TODO.bat`](file:///d:/programacio/repredi/ReprediSL_V4/ARRANCAR_TODO.bat)
- **Objetivo:** Script maestro que arranca de un solo clic los 4 servicios necesarios: PostgREST (API 3000), Caddy (HTTPS 443), Sincronizador de Pedidos a Access y el Demonio de Bandeja de Windows (`ReprediTrayDaemon.exe`).

### 10. [`PARAR_SISTEMA_COMPLETO.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/PARAR_SISTEMA_COMPLETO.bat) & [`PARAR_TODO.bat`](file:///d:/programacio/repredi/ReprediSL_V4/PARAR_TODO.bat)
- **Objetivo:** Script de parada limpia que detiene de golpe todos los servicios (PostgREST, Caddy, Sincronizador PowerShell y Demonio de Bandeja).

### 11. [`SincronizarPedidosEntrantes.ps1`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/BaseDatos/SincronizarPedidosEntrantes.ps1) & [`ARRANCAR_SYNC_PEDIDOS.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/ARRANCAR_SYNC_PEDIDOS.bat)
- **Objetivo:** Proceso en segundo plano que consulta pedidos nuevos en PostgreSQL (`public.pedidos_nuevos`), notifica en tiempo real a `sync_progress.log` para disparar las alertas en `ReprediTrayDaemon.exe` e inserta automáticamente los registros en `PedidosCab` y `PedidosLin` de Microsoft Access (`gestion.mdb`).

### 12. [`CONFIGURAR_HOSTS_LOCAL.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/CONFIGURAR_HOSTS_LOCAL.bat)
- **Objetivo:** Añade `127.0.0.1 api.repredisl.com` al archivo `hosts` de Windows (`C:\Windows\System32\drivers\etc\hosts`) ejecutado como Administrador, permitiendo que navegadores en el mismo equipo donde se ejecuta el servidor conecten directamente a la API HTTPS sin verse bloqueados por la falta de NAT Loopback en el router local.
