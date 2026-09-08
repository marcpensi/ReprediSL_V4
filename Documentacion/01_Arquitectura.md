# Arquitectura del Sistema - ReprediSL_V4

**Versión:** 4.1.0  
**Patrón de Diseño:** Offline-First Decoupled Web Application

---

## 1. Diagrama de la Arquitectura

```
+-----------------------------------------------------------------------+
|                         REPREDISL V4 FRONTEND                         |
|                 (React 18 + Vite + IndexedDB Nativo)                  |
+-----------------------------------------------------------------------+
                                   |
                         Peticiones HTTPS / REST
                                   v
+-----------------------------------------------------------------------+
|                           REVERSE PROXY                               |
|                           (Caddy Server)                              |
+-----------------------------------------------------------------------+
                                   |
                              Puerto 3000
                                   v
+-----------------------------------------------------------------------+
|                             API ENGINE                                |
|                            (PostgREST 16)                             |
+-----------------------------------------------------------------------+
                                   |
                             Conexión SQL
                                   v
+-----------------------------------------------------------------------+
|                           BASE DE DATOS                               |
|                         (PostgreSQL 12+)                              |
+-----------------------------------------------------------------------+
```

---

## 2. Componentes de la Arquitectura

### 2.1 Capa Cliente (Frontend)
- **Framework:** React 18.3.1 + Vite 6.0.5.
- **Diseño Móvil:** Layout adaptativo reactivo con navegación inferior (Clientes, Pedidos, Catálogo, Configuración).
- **Generación PDF:** `jsPDF` en navegador para generación local inmediata de comprobantes de pedido.

### 2.2 Persistencia y Caché Local
- **IndexedDB Nativo (`db.js`):** Almacenamiento local de hasta 300 clientes. Sincronización en segundo plano mediante la API cuando existe conexión de red.

### 2.3 Capa API Gateway
- **PostgREST 16.0:** Convierte las tablas PostgreSQL en endpoints RESTful sin código backend intermedio.
- **Caddy Server:** Proxy inverso para SSL/TLS, compresión Gzip y headers CORS.

---

## 3. Documentación Detallada

- Especificación de Arquitectura Completa: [ARQUITECTURA_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/01_Arquitectura/ARQUITECTURA_V3.md)
