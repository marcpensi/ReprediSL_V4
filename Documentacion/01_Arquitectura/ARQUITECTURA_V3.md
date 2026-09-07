# ARQUITECTURA GENERAL - REPREDISL V3

**Proyecto:** REPREDISL_V3  
**Versión del Sistema:** 3.0.0  
**Fecha:** 04-09-2026  

---

## 1. Visión General de la Arquitectura

REPREDISL V3 está diseñado como una aplicación comercial moderna, desacoplada y orientada a la movilidad. Sigue una arquitectura **Offline-First** optimizada para vendedores y comerciales en campo.

```
+-----------------------------------------------------------------------+
|                         REPREDISL V3 FRONTEND                         |
|                     (React 18 + Vite + IndexedDB)                     |
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

## 2. Capas del Sistema

### 2.1 Capa de Presentación (Frontend)
- **Tecnología:** React 18.3.1 + Vite 6.0.5 + Lucide React.
- **Estructura Móvil:** Layout reactivo con barra de navegación inferior (Clientes, Pedidos, Catálogo, Configuración).
- **Generación de Documentos:** `jsPDF` en cliente para la creación instantánea de pedidos en formato PDF.

### 2.2 Capa de Persistencia Local (Caché & Offline)
- **Tecnología:** IndexedDB API nativa.
- **Nombre BD:** `REPREDISL`
- **Store:** `clientes` (índice `cachedAt`).
- **Política de Caché:** 
  - Almacena hasta 300 clientes en la caché local para acceso ultrarrápido sin cobertura.
  - Sincronización en segundo plano cuando hay conexión disponible.

### 2.3 Capa API & Gateway (Backend)
- **PostgREST 16:** Expone automáticamente las tablas y vistas de PostgreSQL a través de una API RESTful estándar.
- **Caddy Server:** Administrador de certificados SSL/TLS, reverse proxy y terminación HTTPS para producción.

---

## 3. Modelo de Datos Principal (PostgreSQL)

### Tabla `clientes`
Contiene la información de los clientes comerciales:
- `id_cliente` (BIGINT / SERIAL PRIMARY KEY)
- `codigo` (VARCHAR)
- `nombre` (Razon Social)
- `nombre_comercial` (Nombre comercial)
- `nif` (NIF/CIF)
- `telefono`, `movil`, `email`, `web`
- `street`, `codigo_postal`, `city`, `state` (Dirección fiscal)
- `direccionenvio`, `cpostalenvio`, `poblacionenvio`, `provinciaenvio` (Dirección reparto)
- `nombre_banco`, `cuenta_bancaria`
- `id_vendedor` (Asignación a vendedores V1..VD)

---

## 4. Estrategia PWA y Movilidad

- **Diseño Adaptativo:** Mobile-first optimizado para smartphones y tablets.
- **Resiliencia a Desconexión:** Si la red falla, la aplicación continúa funcionando leyendo y buscando sobre IndexedDB.
