// Configuración centralizada de REPREDISL
// Si cambia el esquema del servidor, solo se modifica este archivo.

export const CONFIG = {
  // --- API / PostgREST ---
  HISTORIAL_TABLE: 'zlineas_plantillas',
  HISTORIAL_LIMIT: 100,
  CLIENTES_LIMIT: 30,
  PRODUCTOS_LIMIT: 100,

  // --- Mapeo de columnas del servidor ---
  HISTORIAL_COLUMN_MAP: {
    clienteCode:  ['id_cliente', 'cliente_code', 'codigo_cliente'],
    productoCode: ['id_producto', 'producto_code', 'codigo'],
    nombre:       ['nombre', 'descripcion', 'nombre_producto'],
    cantidad:     ['cantidad', 'uds', 'unidades'],
    precio:       ['precio', 'precio_unitario', 'pv'],
    fecha:        ['fecha', 'fecha_venta', 'fecha_albaran']
  },

  // --- IndexedDB ---
  DB_NAME: 'REPREDISL',
  DB_VERSION: 3,
  MAX_CACHE_CLIENTES: 300,

  // --- Seguridad ---
  PIN_LENGTH: 4,

  // --- Marca ---
  COLORS: {
    primary: '#28a745',
    dark:    '#000000',
    light:   '#ffffff'
  }
};