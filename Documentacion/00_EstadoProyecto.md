# Estado del Proyecto - ReprediSL_V4

**Fecha de actualización:** 11-09-2026  
**Versión:** 4.8.1  
**Estado General:** Operativo, Auditado y Desplegado en Producción (Consolidación V4.8.1 - Rediseño de KPIs de Procesos Anti-Solapamiento, Responsividad Integral del Centro de Control WinForms C# .NET 10, Auditoría Técnica Completa, Pruebas 5/5 OK y Despliegue en Hostinger)

---

## 1. Resumen Ejecutivo

`ReprediSL_V4` es la evolución consolidada de la aplicación web comercial para la fuerza de ventas de REPREDISL. El sistema permite a los comerciales gestionar clientes, realizar búsquedas rápidas con debounce, consultar fichas técnicas detalladas y generar pedidos comerciales exportables instantáneamente a PDF mediante `jsPDF`, operando tanto Online como Offline gracias a IndexedDB.

---

## 2. Hitos Completados

- [x] **Consolidación V4:** Integración del código Frontend React 18.3.1 + Vite 6.0.5 y binarios de la API PostgREST 16.0 en la estructura estandarizada de V4.
- [x] **Verificación de Compilación:** Compilación de producción probada con éxito (`npm run build` ejecutado sin errores).
- [x] **Capacidad PWA Offline:** Service Worker (`sw.js`), `manifest.json`, detección de conexión online/offline en vivo en la interfaz de usuario.
- [x] **Catálogo de Productos y Tarifas:** Normalización de esquemas PostgREST a IndexedDB v2 (store `productos`) con consultas offline.
- [x] **Suite de Pruebas Unitarias:** 5/5 pruebas unitarias automatizadas con la suite nativa de Node.js (`npm test`).
- [x] **Actualizador Automático MDB y Sync en Lote:** Módulo VBA `modActBdApi.bas` optimizado con `BATCH_SIZE = 500`, DDL dinámico para campos/consultas API, y logging en vivo.
- [x] **Demonio Nativo C# WinForms (.NET 10):** Migración a ejecutable nativo `.exe` en `src/Daemon/bin/ReprediTrayDaemon.exe` (.NET 10), protección de instancia única con Mutex local, consumo mínimo de RAM (~25MB), monitoreo visual en tiempo real con resaltado de logs en ROJO/NARANJA/VERDE y diálogo de control de ráfagas de errores (>3) con opciones de cancelación o continuación en silencio.
- [x] **Dashboard de Telemetría y Pipeline Visual (V4.3.0):** Rediseño completo de la interfaz WinForms con Segmented Pill Switch (Auto vs Manual), diagrama visual reactivo de Pipeline de datos (Postgres 16 ⇄ PsSyncBridge ⇄ Access ERP), 4 tarjetas métricas (Reloj sinc, Uptime, Procesados, Pendientes), barra de sub-métricas, visor modal de pedidos y estandarización estricta UTF-8 con BOM en código fuente C#.
- [x] **Integración con Servidor MCP-Access:** Configuración oficial del servidor MCP (`luna-soft.access-explorer`) en `.vscode/mcp.json` para consulta e inspección de `gestion.mdb` asistida por IA.
- [x] **Sincronización Dinámica Postgres (`DROP CASCADE`):** Recreación automática de tablas en PostgreSQL con `DROP TABLE ... CASCADE` en `modActBdApi.bas` para garantizar coincidencia total de esquemas con las consultas API.
- [x] **Pipeline Completo de Pedidos PWA -> API -> Access ERP:** Implementada la inserción HTTP POST en la PWA (`https://pedidos.repredisl.com`), endpoint `/pedidos` en PostgREST (`https://api.repredisl.com`), persistencia en PostgreSQL (`public.pedidos_nuevos`) y sincronizador en tiempo real (`SincronizarPedidosEntrantes.ps1` / `ARRANCAR_SYNC_PEDIDOS.bat`) que alimenta `sync_progress.log`, alerta al demonio de bandeja `ReprediTrayDaemon` e inserta cabecera y líneas en `PedidosCab` y `PedidosLin` de Microsoft Access.
- [x] **Configuración Base ERP Producción PsGest (`C:\pensi\psgestw\e0012026\gestion.mdb`):** Conexión prioritaria configurada en `DbSyncService.cs`, `SincronizarPedidosEntrantes.ps1` y scripts de exportación para insertar directamente en la base de datos real del ERP de la empresa.
- [x] **Identificación de Serie Comercial Real (`VD`):** Corrección del prefijo en la numeración para reflejar la serie del vendedor (`VD-2988` en vez de `P-2988`), capturando el importe real y asociando correctamente `Serie` y `NumPedido` en las tablas `PedidosCab` y `PedidosLin`.
- [x] **Script de Migración .MDB Externo:** Automatización completa que clona `dborigen.mdb` -> `bddestino.mdb`, actualiza esquemas/consultas DAO e inyecta el código VBA sin intervención manual.
- [x] **Centro de Control Unificado de Procesos y Servicios (V4.5.0):** Centralización del arranque, parada y monitorización en vivo de todos los procesos del sistema (PostgreSQL 16, PostgREST API, Caddy Reverse Proxy, Sincronizador de Pedidos y Access ERP) en un único módulo visual (`ReprediTrayDaemon`). Incluye botonera maestra («Arrancar Todo», «Detener Todo», «Reiniciar»), tiles con estado en vivo (🟢/🔴/🟡) y control individual, integración con `ARRANCAR_TODO.bat` mediante `--start-all` (sin consolas CMD sueltas), salida unificada de logs en consola y acceso rápido en la bandeja del sistema.
- [x] **Sincronización Canónica Estricta Access `qry*api` (V4.8.0):** Supresión total de rutinas de creación/alteración arbitraria de consultas y campos en Access (`ActualizarEstructuraAccess`). Exportación 100% dinámica de las consultas cuyo nombre comience por `qry` y finalice por `api`. Mapeo canónico a PostgreSQL: `qryuventasapi` $\rightarrow$ `historial`, `qrypreciosapi` $\rightarrow$ `catalogo` (tarifa 1 o configurable), `qrytarifasapi` $\rightarrow$ `tarifas`, y el resto por nombre base (`clientes`, `vendedores`...).
- [x] **Gestión Local de Pedidos y Filtrado de Historial (V4.8.0):** Los pedidos se crean, persisten y mantienen en el cliente remoto (IndexedDB de la PWA) y se transmiten a la API mediante HTTP POST. La consulta de historial en la PWA se filtra en tiempo real por el código del cliente seleccionado.
- [x] **Corrección de Visibilidad UI en Modal de Artículos (V4.8.0):** Solucionado el problema de contraste en el botón «Aceptar» de la ventana modal de añadir artículos (`AddModal`), estableciendo estilos de alta especificidad para estado normal y deshabilitado.
- [x] **Rediseño de KPIs de Procesos Anti-Solapamiento (V4.8.1):** Rediseño visual de las 5 tarjetas de control de servicios (`PostgreSQL`, `PostgREST API`, `Caddy Proxy`, `Sync Pedidos`, `Access ERP`) con contenedores de iconos independientes (`36x36px`), títulos y subtítulos con desplazamiento seguro (`X = 52px`) para eliminar todo recorte o solapamiento, y botones de control de 72px («⏹️ Parar», «▶️ Iniciar») completamente legibles.
- [x] **Responsividad Total del Centro de Control C# WinForms (V4.8.1):** Implementación de cuadrícula `TableLayoutPanel` (5 columnas proporcionales del 20%) y controlador de eventos `UpdateResponsiveLayout` para escalado dinámico de los 8 contenedores principales ante cualquier ancho de pantalla o redimensionamiento de ventana.
- [x] **Auditoría Integral del Proyecto (V4.8.1):** Elaboración del informe técnico oficial [`AUDITORIA_V4.md`](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/AUDITORIA_V4.md) documentando qué hace el proyecto, qué no hace y los objetivos conseguidos.

---

## 3. Documentación Histórica y Detallada

- **Auditoría Integral V4:** [`AUDITORIA_V4.md`](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/AUDITORIA_V4.md)
- Auditoría Inicial: [ESTADO_INICIAL.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ESTADO_INICIAL.md)
- Roadmap de Evolución: [ROADMAP_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ROADMAP_V3.md)
- Estado de Fases: [PENDIENTES_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/PENDIENTES_V3.md)
