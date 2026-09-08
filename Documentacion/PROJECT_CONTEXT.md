# PROJECT_CONTEXT - ReprediSL_V4

## Información del proyecto

- **Nombre:** ReprediSL_V4
- **Tipo de aplicación:** Web Application / PWA Móvil Comercial (Ventas)
- **Lenguaje:** JavaScript (ES6+ / React JSX) / VBA (Microsoft Access)
- **Framework:** React 18.3.1 + Vite 6.0.5
- **Versión:** 4.1.0
- **Ruta local:** `D:\programacio\repredi\ReprediSL_V4`

## Repositorio

- **Repositorio:** marcpensi/ReprediSL_V4
- **Rama principal:** main

## Servidor e Infraestructura

- **Servidor:** PostgREST 16.0 + Caddy Reverse Proxy
- **Dominio:** api.repredisl.com (Producción) / 127.0.0.1:3000 (Local)
- **Hosting:** Hostinger (Frontend static) / Windows Server local o VPS (API & Access Sync)

## Base de datos

- **Base de datos Access (Origen y Destino):** Microsoft Access Formato `.mdb` (`src/Access/GESTION_ACTUAL.MDB` y `src/Access/BdDestino.mdb`)
- **Base de datos API:** PostgreSQL 12+ (`repredisl_api`)
- **Base de datos local PWA:** IndexedDB (`REPREDISL` v2)

## Operaciones

- **Deployment:** Build estático mediante Vite (`npm run build`) y servicio Windows PostgREST+Caddy
- **Sincronización:** Módulo VBA `modActBdApi.bas` en Access exporta `QryClientesApi`, `QryVendedoresApi`, `QryTarifasApi`, `QryPreciosApi` y `QryUventasApi` a PostgreSQL y recarga el esquema con `NOTIFY pgrst, 'reload schema'`.
- **Health check:** `GET /clientes?limit=1` contra PostgREST
- **Documentación principal:** `Documentacion/README.md`

---

## Hechos confirmados

- Proyecto consolidado en V4 a partir de `ReprediSL_V3` y prototipos React/Vite.
- Inclusión de las bases de datos Microsoft Access (`GESTION_ACTUAL.MDB` y `BdNewRepre.accdb`) y script de inserción `modActBdApi.bas` en `src/Access`.
- Frontend compilando de forma limpia con Vite (1.800 módulos transformados).
- Persistencia local en IndexedDB v2 con tiendas `clientes` y `productos`.
- Backend desacoplado expuesto con PostgREST 16.0 sobre la base `repredisl_api`.

## Suposiciones

- La conexión ODBC/DAO configurada en `modActBdApi.bas` apunta a `localhost:5432` con la base de datos `repredisl_api`.
