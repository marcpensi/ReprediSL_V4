# ESPECIFICACIÓN DE LA API REST - REPREDISL V3

**Servidor API:** PostgREST v16.0  
**URL Base Local:** `http://127.0.0.1:3000`  
**URL Producción:** `https://api.repredisl.com`  

---

## 1. Clientes (`/clientes`)

### GET `/clientes`
Obtiene la lista de clientes o realiza búsquedas con filtros.

#### Parámetros HTTP:
- `order`: Ordenamiento (`id_cliente.asc`)
- `limit`: Cantidad máxima de registros (Predeterminado: 30)
- `or`: Filtro multilinea PostgREST para búsqueda
- `id_cliente`: Filtro de actualización masiva `in.(1,2,3)`

#### Ejemplos de Petición:

**Carga inicial (30 registros):**
```bash
GET /clientes?order=id_cliente.asc&limit=30
```

**Búsqueda multitérmino (debounce 300ms):**
```bash
GET /clientes?or=(nombre.ilike.*BAR*,nombre_comercial.ilike.*BAR*,nif.ilike.*BAR*,id_cliente.eq.100)&limit=30
```

**Sincronización por lote de códigos guardados:**
```bash
GET /clientes?id_cliente=in.(101,102,103,104)
```

---

## 2. Pedidos (`/pedidos`)

### GET `/pedidos`
Obtiene pedidos filtrados por cliente o serie.

```bash
GET /pedidos?id_cliente=eq.101
```

### POST `/pedidos`
Registra un nuevo pedido comercial generado desde el frontend.

#### Body JSON:
```json
{
  "id_cliente": 101,
  "numero_pedido": "V1-00045",
  "vendedor_pedido": 1,
  "serie": "V1",
  "lineas": [
    {
      "id_producto": 5,
      "descripcion": "Producto Ejemplo",
      "cantidad": 12,
      "unidad": "UNI",
      "precio_unitario": 15.50,
      "subtotal": 186.00
    }
  ],
  "subtotal": 186.00,
  "impuestos": 39.06,
  "total": 225.06,
  "estado": "procesado"
}
```

---

## 3. Códigos de Respuestas HTTP

- **200 OK:** Petición procesada correctamente.
- **201 Created:** Registro insertado con éxito.
- **400 Bad Request:** Filtro de PostgREST mal formado.
- **404 Not Found:** Recurso o vista no encontrada.
- **500 Internal Server Error:** Error de conexión con PostgreSQL.
