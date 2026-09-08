# Modelo de Datos - ReprediSL_V4

**Motor Backend:** PostgreSQL 12+ (Base de datos: `repredisl_api`)  
**Origen y Destino de Datos Access:** Microsoft Access Formato MDB (`src/Access/GESTION_ACTUAL.MDB` y `src/Access/BdDestino.mdb`)  
**Motor Local (Offline):** IndexedDB API (Nativo de navegador, BD: `REPREDISL`)

---

## 1. Integración y Sincronización desde Microsoft Access

El módulo VBA [`modActBdApi.bas`](file:///d:/programacio/repredi/ReprediSL_V4/src/Access/modActBdApi.bas) automatiza la exportación de las consultas principales de Microsoft Access hacia PostgreSQL (`repredisl_api`):

1. **`clientes`** (vía `QryClientesApi`): Información fiscal, direcciones desglosadas, contacto y cobros de clientes.
2. **`vendedores`** (vía `QryVendedoresApi`): Comercial asignado y series de facturación (`V1` a `VD`).
3. **`tarifas`** (vía `QryTarifasApi`): Definición de tarifas comerciales.
4. **`precios`** (vía `QryPreciosApi`): Matriz de precios por artículo y tarifa.
5. **`uventas`** (vía `QryUventasApi`): Unidades por caja y unidades de venta por producto.

Una vez insertados los datos, el script ejecuta la notificación dinámica a PostgREST:
```sql
NOTIFY pgrst, 'reload schema';
```

---

## 2. Esquema Relacional PostgreSQL (Backend `repredisl_api`)

### 2.1 Tabla `clientes`
- `id_cliente` (BIGINT / PRIMARY KEY): Identificador único del cliente.
- `codigo` (VARCHAR): Código comercial asignado.
- `nombre` (VARCHAR): Razón social fiscal.
- `nombre_comercial` (VARCHAR): Nombre comercial / marca.
- `nif` (VARCHAR): NIF / CIF fiscal.
- `telefono`, `movil`, `email`, `web` (VARCHAR): Canales de contacto.
- `street`, `codigo_postal`, `city`, `state` (VARCHAR): Dirección fiscal.
- `direccionenvio`, `cpostalenvio`, `poblacionenvio`, `provinciaenvio` (VARCHAR): Dirección de entrega.
- `nombre_banco`, `cuenta_bancaria` (VARCHAR): Información de cobro / IBAN.
- `id_vendedor` (INTEGER): ID del vendedor asignado (`1` a `13`).

### 2.2 Tabla `tarifas` y `precios`
- `id_tarifa` (INTEGER / PRIMARY KEY): Código de tarifa.
- `id_producto` / `codigo` (VARCHAR): Referencia al artículo.
- `precio_venta` (NUMERIC): Precio aplicable por tarifa.

### 2.3 Tablas `pedidos` y `lineas_pedido`
- `id_pedido` (BIGINT / PRIMARY KEY): Identificador del pedido.
- `numero_pedido` (VARCHAR): Formato correlativo por serie (ej. `V1-00045`).
- `id_cliente` (BIGINT / FK): Referencia a `clientes`.
- `vendedor_pedido` (INTEGER): ID del comercial.
- `serie` (VARCHAR): Serie del vendedor (`V1`..`VD`).
- `subtotal`, `impuestos`, `total` (NUMERIC): Importes económicos.

---

## 3. Almacenamiento Local IndexedDB (`REPREDISL` v2)

- **Database Name:** `REPREDISL`
- **Object Stores:**
  - `clientes`: Caché de hasta 300 clientes en movilidad (`keyPath: 'code'`, índice `cachedAt`).
  - `productos`: Caché local de catálogo y precios (`keyPath: 'code'`, índice `cachedAt`).
