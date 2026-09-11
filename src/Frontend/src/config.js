// Configuración centralizada de REPREDISL
export const CONFIG = {
  // --- API / PostgREST ---
  HISTORIAL_TABLE: 'historial',
  CATALOGO_TABLE: 'catalogo',
  PEDIDOS_TABLE: 'pedidos_nuevos',
  TARIFAS_TABLE: 'tarifas',
  VENDEDORES_TABLE: 'vendedores',
  CLIENTES_LIMIT: 30,
  PRODUCTOS_LIMIT: 2000,
  HISTORIAL_LIMIT: 200,

  // --- Mapeo de columnas del servidor ---
  HISTORIAL_COLUMN_MAP: {
    clienteCode:  ['id_cliente', 'cliente_code', 'codigo_cliente'],
    productoCode: ['id_producto', 'producto_code', 'codigo'],
    nombre:       ['descripcion', 'nombre_producto', 'nombre'],
    cantidad:     ['cantidad', 'uds', 'unidades'],
    precio:       ['precio_unitario', 'precio', 'pv'],
    fecha:        ['fecha', 'fecha_uventa', 'fecha_venta', 'fecha_albaran']
  },

  // --- IndexedDB ---
  DB_NAME: 'REPREDISL',
  DB_VERSION: 4,
  MAX_CACHE_CLIENTES: 300
};