# Estrategia y Registro de Pruebas - ReprediSL_V4

**Estado de Pruebas:** Verificación de Compilación Completada | Tests Automatizados en Planificación

---

## 1. Pruebas de Compilación y Sintaxis

- **Herramienta:** Vite 6.0.5 (`vite build`)
- **Resultado:** **EXITOSO (0 errores)**
- **Detalles del Build:**
  - 1.800 módulos transformados e integrados.
  - Tiempo total de compilación: 6.85 segundos.
  - Bundle generado en `src/Frontend/dist`.

---

## 2. Pruebas de Persistencia y API (Manuales)

- **Búsqueda con Debounce:** Verificado el envío de peticiones HTTP `GET /clientes?or=...` tras 300ms de inactividad de teclado.
- **IndexedDB Fallback:** Verificada la carga de hasta 300 clientes en la base de datos `REPREDISL` del navegador en modo offline.
- **Generación de PDF:** Verificada la creación y descarga de documentos de pedido en cliente usando `jsPDF`.

---

## 3. Plan de Pruebas Automatizadas (Próxima Fase)

- **Directorio de Tests:** `tests/`
- **Áreas a Cubrir:**
  1. Unit Tests para normalización de respuesta de clientes en [`clientApi.js`](file:///d:/programacio/repredi/ReprediSL_V4/src/Frontend/src/clientApi.js).
  2. Integration Tests para sincronización de IndexedDB en [`db.js`](file:///d:/programacio/repredi/ReprediSL_V4/src/Frontend/src/db.js).
