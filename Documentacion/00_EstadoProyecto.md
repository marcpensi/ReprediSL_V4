# Estado del Proyecto - ReprediSL_V4

**Fecha de actualización:** 09-09-2026  
**Versión:** 4.3.0  
**Estado General:** Estable y Operativo (Consolidación V4.3.0 - Dashboard de Telemetría Avanzado, Pipeline Visual, Segmented Pill Switch y Estandarización UTF-8 BOM en Demonio C# .NET 10)

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
- [x] **Script de Migración .MDB Externo:** Automatización completa que clona `dborigen.mdb` -> `bddestino.mdb`, actualiza esquemas/consultas DAO e inyecta el código VBA sin intervención manual.

---

## 3. Documentación Histórica y Detallada

- Auditoría Inicial: [ESTADO_INICIAL.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ESTADO_INICIAL.md)
- Roadmap de Evolución: [ROADMAP_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ROADMAP_V3.md)
- Estado de Fases: [PENDIENTES_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/PENDIENTES_V3.md)
