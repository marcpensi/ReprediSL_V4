# Esquema de Base de Datos Access → PostgreSQL
> **ReprediSL_V4** — Documentación de diseño de la BD
> Generado: 2026-09-11 | Versión: v4.8.1

> Los archivos binarios `.mdb` y `.accdb` están excluidos de git por tamaño (>50 MB).
> Este documento es la referencia oficial del diseño de la base de datos.

---

## Archivos de Base de Datos (locales, no versionados)

| Archivo | Tamaño | Descripción |
|---------|--------|-------------|
| `src/Access/gestion.mdb` | 67,56 MB | BD principal operativa (copia de trabajo) |
| `src/Access/BdDestino.mdb` | 67,56 MB | BD destino para migraciones/comparativas |
| `src/Access/BdNewRepre.accdb` | — | BD de nuevo representante (formato ACCDB) |
| `src/Access/E0012026/gestion.mdb` | 16,6 MB | BD de empresa ejercicio 2026 |
| `src/Access/GESTION_ACTUAL.MDB` | 16,5 MB | Snapshot actual para comparativas |
| `src/Access/GESTION_ORIGEN.MDB` | 16,5 MB | BD origen para migraciones |

---

## Consultas API (qry*api) → Tablas PostgreSQL

El módulo `modActBdApi.bas` exporta automáticamente todas las consultas Access
que siguen el patrón **`qry{nombre}api`** al esquema `public` de PostgreSQL,
y crea vistas en el esquema `api` accesibles vía PostgREST.

### Mapeo de Consultas → Tablas

| Consulta Access | Tabla PostgreSQL (public) | Vista API (api) | Descripción |
|-----------------|---------------------------|-----------------|-------------|
| `qryuventasapi` | `historial` | `api.historial` | Historial de ventas (filtrado por cliente) |
| `qrypreciosapi` | `catalogo` | `api.catalogo` | Catálogo de artículos (solo tarifa 1) |
| `qrytarifasapi` | `tarifas` | `api.tarifas` | Tarifas comerciales |
| `qrypedidosapi` | `pedidos` | `api.pedidos` | Pedidos (solo envío hacia cliente remoto) |
| `qry{nucleo}api` | `{nucleo}` | `api.{nucleo}` | Resto de consultas (nombre directo) |

**Notas importantes:**
- **`historial`**: Los registros se filtran por el cliente indicado al cargar la PWA.
- **`catalogo`**: Solo se exporta la **tarifa 1** (o la configurable). Filtro sobre campo `tarifa`, `id_tarifa` o `codtarifa`.
- **`pedidos`**: Solo fluyen en sentido Access → PWA remota. No se sincronizan de vuelta.

---

## Configuración de Conexión PostgreSQL

```
Host     : localhost
Puerto   : 5432
Base     : repredisl_api
Usuario  : postgres
Schemas  : public (datos), api (vistas PostgREST)
```

Las credenciales están en `modActBdApi.bas` como constantes privadas.
Migrar a tabla de configuración o archivo .env local en producción.

---

## Esquema de Tipos: Access DAO → PostgreSQL

| Tipo DAO (Access) | Tipo PostgreSQL |
|-------------------|-----------------|
| `dbBoolean` (1) | `boolean` |
| `dbByte` (2) | `smallint` |
| `dbInteger` (3) | `integer` |
| `dbLong` (4) | `integer` |
| `dbSingle` (6) | `real` |
| `dbDouble` (7) | `double precision` |
| `dbCurrency` (5) | `numeric(19,4)` |
| `dbDecimal` (20) | `numeric` |
| `dbDate` (8) | `timestamp` |
| `dbText` (10) | `varchar(N)` o `text` si N > 10 MB |
| `dbMemo` (12) | `text` |
| `dbGUID` (15) | `uuid` |
| Otros | `text` |

---

## Flujo de Sincronización

```
Microsoft Access (gestion.mdb)
        |
        | ExportarTablas() — VBA modActBdApi.bas
        | Filtra consultas: qry*api
        |
        v
PostgreSQL (public schema)
  ┌──────────────────────────────────────┐
  │ DROP TABLE ... CASCADE               │
  │ CREATE TABLE public.{tabla} (...)    │
  │ INSERT ... VALUES ... (lotes 500)    │
  └──────────────────────────────────────┘
        |
        | CREATE OR REPLACE VIEW api.{tabla} AS SELECT * FROM public.{tabla}
        | GRANT SELECT ON ALL TABLES IN SCHEMA api TO web_anon
        | NOTIFY pgrst, 'reload schema'
        v
PostgREST API  ->  PWA React (IndexedDB)
```

### Características del proceso
- **Lotes de 500 filas** (`BATCH_SIZE = 500`) para evitar timeouts
- **DROP TABLE CASCADE** antes de cada importación (reemplazo completo)
- **Transacción ADODB** por tabla (commit/rollback atómico)
- **Log en tiempo real** → `sync_progress.log` y `sync_errors.log` en el directorio de la BD

---

## Tablas PostgreSQL resultantes (esquema `public`)

### `public.historial` (← `qryuventasapi`)
Historial de ventas del representante. Campos típicos:
- `codcli` / `nomcli` — Código y nombre de cliente
- `codart` / `desart` — Código y descripción de artículo
- `fecha` — Fecha de venta
- `cantidad`, `precio`, `importe` — Datos de línea
- `numpedido`, `numalbaran` — Referencias documentales

### `public.catalogo` (← `qrypreciosapi`)
Catálogo de artículos con precios (tarifa 1). Campos típicos:
- `codart` — Código de artículo (clave)
- `desart` — Descripción
- `precio` / `pvp` — Precio de venta
- `tarifa` / `id_tarifa` — Identificador de tarifa (filtro = 1)
- `familia`, `subfamilia` — Clasificación

### `public.tarifas` (← `qrytarifasapi`)
Tarifas comerciales aplicables. Campos típicos:
- `codtarifa` / `id_tarifa` — Identificador de tarifa
- `destarifa` — Descripción
- `descuento` — Porcentaje de descuento

### `public.pedidos` (← `qrypedidosapi`)
Pedidos del representante. Solo lectura desde la PWA; se envían desde Access.
- `numpedido` — Número de pedido
- `codcli` / `nomcli` — Cliente
- `fecha` — Fecha del pedido
- `importe` — Total pedido
- Líneas: `codart`, `desart`, `cantidad`, `precio`

---

## Permisos PostgREST

```sql
GRANT USAGE ON SCHEMA api TO web_anon;
GRANT SELECT ON ALL TABLES IN SCHEMA api TO web_anon;
ALTER DEFAULT PRIVILEGES IN SCHEMA api GRANT SELECT ON TABLES TO web_anon;
NOTIFY pgrst, 'reload schema';
```

El usuario `web_anon` es el rol anónimo de PostgREST (solo lectura sobre esquema `api`).

---

## Notas de Mantenimiento

1. **Añadir nueva consulta exportable**: Crear `qry{nombre}api` en Access. Se exportará automáticamente.
2. **Cambiar filtro de tarifa**: Modificar línea 144 de `modActBdApi.bas` (`= 1` → valor deseado).
3. **Restaurar BD**: Copiar el `.mdb` desde backup. Los archivos no están en git intencionalmente.
4. **Comparar esquemas**: Usar `GESTION_ACTUAL.MDB` vs `GESTION_ORIGEN.MDB` para diffs de estructura.

