# RESUMEN EJECUTIVO Y TÉCNICO DE PSAGENTS (REPREDISL_V4)
> **Propósito:** Documento de contexto integral estructurado para su importación como fuente en **NotebookLM** (Generación de Audio Overviews, Preguntas/Respuestas y Guías de Estudio).

---

## 1. INTRODUCCIÓN Y VISIÓN GENERAL DE PSAGENTS

**PsAgents** es la metodología y framework de gestión multagente impulsado por Inteligencia Artificial utilizado en el desarrollo y mantenimiento del proyecto **ReprediSL_V4**.

El proyecto `ReprediSL_V4` es una **PWA (Progressive Web App) Móvil Comercial Offline-First** diseñada para la fuerza de ventas de REPREDISL. Permite la gestión de clientes, consulta de catálogo y precios, normalización de productos y generación instantánea de pedidos en PDF.

La arquitectura se fundamenta en un **desacoplamiento total**:
1. **Frontend Móvil (React 18 + Vite + IndexedDB v2):** Funciona 100% offline en dispositivos móviles.
2. **Backend API (PostgREST 16.0 + Caddy Proxy):** Expone la base de datos PostgreSQL mediante API REST de alto rendimiento.
3. **ERP Legado (Microsoft Access `.mdb`):** Base de datos origen en los sistemas centrales de la empresa.
4. **Demonio de Sincronización C# .NET 10 (`ReprediTrayDaemon`):** Aplicación nativa en la bandeja de sistema (System Tray) de Windows que supervisa la sincronización Access -> PostgreSQL en tiempo real y gestiona incidencias.

---

## 2. ESTRUCTURA Y ROLES DEL EQUIPO DE AGENTES (PSAGENTS)

El sistema PsAgents divide la responsabilidad del proyecto en 5 roles especializados de IA, coordinados bajo un reglamento estricto de gobernanza (`AGENTS.md`):

### 🧠 2.1 Agente Coordinador Principal
- **Responsabilidad:** Planificación de hitos, integración de módulos y consolidación de fases.
- **Funciones:**
  - Garantiza que ninguna tarea modifique proyectos de origen sin autorización.
  - Dictamina la entrada en producción de nuevas versiones (`v4.0.0`, `v4.1.0`).
  - Ejecuta la estrategia de tagging y lanzamientos en Git.

### 🏛️ 2.2 Agente de Arquitectura
- **Responsabilidad:** Diseño de patrones escalables, desacoplamiento y estrategia Offline-First.
- **Funciones:**
  - Mantiene la separación de responsabilidades: `src/db.js` (IndexedDB), `src/clientApi.js` (PostgREST API) y `src/main.jsx` (Interfaz React).
  - Diseña los esquemas de caché local y la estrategia de Service Worker (`sw.js`).
  - Estandariza la migración del demonio de fondo a C# .NET 10 WinForms nativo con Mutex `Local\ReprediSL_V4_Daemon_Mutex`.

### 🔍 2.3 Agente Auditor de Código
- **Responsabilidad:** Inspección de diffs, calidad de software y prevención de regresiones.
- **Funciones:**
  - Audita cambios en el módulo VBA de Access (`modActBdApi.bas`) asegurando compatibilidad con PostgreSQL (`DROP TABLE ... CASCADE`).
  - Valida el rendimiento y la seguridad del código (ausencia de secretos, gestión de memoria RAM).
  - Supervisa que los scripts utilitarios no dupliquen herramientas existentes.

### 📝 2.4 Agente de Documentación
- **Responsabilidad:** Mantenimiento de la documentación viva numerada y trazabilidad del proyecto.
- **Funciones:**
  - Mantiene los directorios `Documentacion/` y `Scripts/` actualizados con sus respectivos índices `README.md`.
  - Registra las decisiones técnicas en `06_Decisiones/DECISIONES_TECNICAS.md` y el estado general en `00_EstadoProyecto.md`.

### 🧪 2.5 Agente de Testing y Verificación
- **Responsabilidad:** Control de calidad de compilación, ejecución de tests y validación de sincronización.
- **Funciones:**
  - Ejecuta la suite de pruebas unitarias (`npm test`).
  - Verifica las compilaciones estáticas de Vite (`npm run build`).
  - Asegura que las sincronizaciones masivas entre Access y PostgreSQL completen con 0 errores.

---

## 3. REGLAS DE GOBERNANZA Y DIRECTIVAS (`AGENTS.md`)

Todos los agentes de PsAgents operan bajo directivas obligatorias e inviolables:
1. **Preservación de Orígenes:** Prohibido borrar o sobrescribir archivos origen sin necesidad explícita.
2. **Respeto a la Arquitectura:** Prohibido romper la separación de capas (Frontend React / API PostgREST / Access VBA / C# Tray Daemon).
3. **Documentación Viva Mandatoria:** Todo cambio relevante debe quedar reflejado en `Documentacion/00_EstadoProyecto.md` e índices `README.md`.
4. **Organización de Scripts:** Todos los scripts auxiliares (.ps1, .bat) deben alojarse exclusivamente en el directorio `Scripts/` clasificados por categoría (`BaseDatos`, `Desarrollo`, `Utilidades`, `Migracion`).
5. **Seguridad:** Prohibido exponer claves, contraseñas o secretos en repositorios.

---

## 4. FLUJO DE TRABAJO Y COMPONENTES CLAVE

```
+-------------------------------------------------------------------------+
|                         REPREDISL V4 FRONTEND                           |
|                  (React 18 + Vite + IndexedDB v2 PWA)                   |
+-------------------------------------------------------------------------+
                                    |
                           Peticiones HTTPS / REST
                                    v
+-------------------------------------------------------------------------+
|                            POSTGREST + CADDY                            |
|                       (API REST - PostgreSQL 12+)                       |
+-------------------------------------------------------------------------+
                                    ^
                         Sincronización VBA / DAO
                                    |
+-------------------------------------------------------------------------+
|                         ERP ACCESS (gestion.mdb)                        |
|           Módulo modActBdApi.bas (ExportarTablas BATCH=500)            |
+-------------------------------------------------------------------------+
                                    ^
                         Monitoreo en Tiempo Real
                                    |
+-------------------------------------------------------------------------+
|                  DEMONIO C# .NET 10 (ReprediTrayDaemon)                 |
|             (Barra de Tareas + Control de Errores >3 Modales)           |
+-------------------------------------------------------------------------+
```

### Principales Módulos del Sistema:
- **`modActBdApi.bas` (Access VBA):** Inyecta consultas dinámicas (`QryClientesApi`, `QryVendedoresApi`, `QryTarifasApi`, `QryPreciosApi`, `QryUventasApi`) y las exporta en lotes a PostgreSQL mediante conexiones DAO/ODBC.
- **`ReprediTrayDaemon.exe` (C# .NET 10):** Proceso en segundo plano alojado en la bandeja de sistema. Lee en tiempo real `sync_progress.log` y `sync_errors.log`. Si detecta más de 3 errores consecutivos, despliega un modal con 3 opciones: *Detener*, *Silenciar Ventanas*, o *Continuar*.
- **Servidor MCP Access Explorer (`luna-soft.access-explorer`):** Permite a los agentes de IA explorar e inspeccionar el esquema de `gestion.mdb` directa mediante protocolo MCP.

---

## 5. GLOSARIO RÁPIDO PARA NOTEBOOKLM

- **PsAgents:** Sistema de trabajo coordinado multagente con IA para desarrollo de software.
- **Offline-First:** Filosofía donde la aplicación móvil funciona sin conexión a internet y sincroniza datos en segundo plano.
- **PostgREST:** Motor que convierte automáticamente una base de datos PostgreSQL en una API RESTful.
- **IndexedDB:** Base de datos NoSQL integrada en los navegadores web para almacenamiento offline en el cliente.
- **ReprediTrayDaemon:** Aplicación nativa en C# .NET 10 para monitorización de sincronización desde la barra de tareas de Windows.
- **modActBdApi:** Módulo VBA en Microsoft Access encargado del volcado masivo de datos hacia la nube/PostgreSQL.
