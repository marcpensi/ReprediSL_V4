# Historial TÃ©cnico - ReprediSL_V4

## [4.0.0-beta.1] - 2026-09-08

### Cambio
ConsolidaciÃ³n oficial de ReprediSL_V4 con soporte Offline-First PWA (manifest.json, Service Worker para cachÃ© de assets estÃ¡ticos), React 18, Vite 6, PostgREST 16 y Caddy.

### Release URL
https://github.com/marcpensi/ReprediSL_V4/releases/tag/v4.0.0-beta.1

### Componentes afectados
- public/manifest.json, public/sw.js, index.html (soporte PWA)
- src/Frontend (React 18 + Vite)
- src/API (PostgREST + Caddy)
- src/Access (Bases de datos Access + MÃ³dulo VBA exportaciÃ³n)

### Resultado
OK - Project Health: 100%

---

## 2026-09-08

### Cambio
Inclusión de la carpeta `src/Access/` conteniendo las bases de datos Access y el módulo VBA de exportación a PostgreSQL.

### Motivo
Proporcionar la fuente de datos original, la versión modificada con la nueva estructura y la herramienta de exportación/sincronización automática de datos hacia la base de datos PostgreSQL de PostgREST.

### Componentes afectados
- `src/Access/GESTION_ACTUAL.MDB`: Base de datos Access original.
- `src/Access/BdNewRepre.accdb`: Base de datos Access con modificaciones de estructura.
- `src/Access/modActBdApi.bas`: Módulo VBA para exportar las consultas `QryClientesApi`, `QryVendedoresApi`, `QryTarifasApi`, `QryPreciosApi` y `QryUventasApi` directamente a PostgreSQL (`repredisl_api`) y recargar el esquema de PostgREST.

### Resultado
OK

### Relacionado con incidencia
N/A

### Confirmado / Supuesto
Confirmado

---

## 2026-09-07

### Cambio
Inicialización de proyecto mediante PsProjectBuilder (`CreaProyecto`).

### Motivo
Creación/adopción de estructura estándar de desarrollo.

### Componentes afectados
- Estructura base de carpetas (`Documentacion`, `Scripts`, `src`).

### Resultado
OK

### Relacionado con incidencia
N/A

### Confirmado / Supuesto
Confirmado
