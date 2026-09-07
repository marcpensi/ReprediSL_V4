# REPREDISL V3 - AUDITORÍA DE ESTADO INICIAL

**Fecha de Auditoría:** 04-09-2026  
**Proyecto:** REPREDISL_V3  
**Coordinador Principal:** Agente Coordinador REPREDISL_V3  

---

## 1. Resumen Ejecutivo

La presente auditoría analiza las tres fuentes de origen asignadas para la consolidación de la versión 3 de REPREDISL. El objetivo principal es construir una plataforma web comercial robusta, escalable, mantenible y preparada para PWA y uso móvil por parte de la fuerza de ventas.

---

## 2. Fuentes Auditadas

### Fuente 1: Código Avanzado (Frontend Principal)
- **Ubicación:** `D:\programacio\repredi\PEDIDOS_REPREDISL_CLIENTES_CORREGIDO\REPREDISL_Definitiva_v1`
- **Versión:** 1.0.0 (Prototipo React/Vite Consolidado)
- **Tecnologías:**
  - **Core UI:** React 18.3.1 + React DOM
  - **Build Tool:** Vite 6.0.5
  - **Iconografía:** Lucide React 0.468.0
  - **Generación PDF:** jsPDF 2.5.2
  - **Persistencia Local:** IndexedDB nativo (Base de datos `REPREDISL`, store `clientes`, límite máximo 300 clientes en caché).
  - **Integración API:** Fetch API con soporte para PostgREST (búsqueda con debounce de 300ms, paginación a 30 clientes).

### Fuente 2: Documentación y Referencia Histórica
- **Ubicación:** `D:\programacio\repredi\REPREDISL_FINAL_FUNCIONAL_v2_TARIFA`
- **Versión:** 1.0.0 (Versión documental y prototipo Vanilla JS)
- **Tecnologías:**
  - HTML5, CSS3 vanilla (variables CSS, responsive design), Vanilla JavaScript ES6+.
  - Servidor estático `http-server`.
  - Documentación técnica extensa en formato Markdown (9 documentos, ~3,000 líneas).

### Fuente 3: Backend / API Windows
- **Ubicación:** `D:\programacio\repredi\API_REPREDISL_WINDOWS`
- **Componentes:**
  - **Servidor API:** PostgREST v16.0 (binario ejecutable de Windows `postgrest.exe`).
  - **Reverse Proxy / SSL:** Caddy server (`Caddyfile`).
  - **Scripts de Automatización:** `ARRANCAR_POSTGREST.bat`, `ABRIR_FIREWALL_HTTPS.bat`, `PROBAR_API_LOCAL.bat`.
  - **Configuración:** `postgrest.conf` apuntando a base de datos PostgreSQL local.

---

## 3. Funcionalidades Existentes

### Frontend (React/Vite)
1. **Identidad Visual:** Esquema de colores corporativo (rojo, blanco, negro).
2. **Navegación Móvil:** Barra inferior fija (Clientes, Pedidos, Catálogo, Config).
3. **Gestión de Clientes:**
   - Visualización de clientes con carga inmediata desde IndexedDB.
   - Búsqueda con debounce (300ms) contra PostgREST al escribir 2 o más caracteres.
   - Paginación y límite de 30 resultados por consulta API para alta velocidad.
   - Ficha detallada de cliente: Datos fiscales, direcciones desglosadas (calle, CP, población, provincia), teléfonos clicables, enlace directo a WhatsApp, correo, web, datos bancarios (IBAN) y vendedor asignado.
   - Creación y edición de clientes (no se permite el borrado físico).
4. **Gestión de Pedidos:**
   - Selección de vendedor (1 al 13) con capitalización de nombres y series asignadas (`V1`..`V9`, `VA`..`VD`).
   - Numeración correlativa automática por serie.
   - Carga de historial de compras previa por cliente y producto.
   - Gestión de unidades por caja en catálogo y líneas de pedido.
   - Función "Mismas" para replicar cantidades de la última compra.
   - Generación real de PDF en cliente mediante `jsPDF`.
5. **Persistencia & Cacheing:**
   - IndexedDB maneja caché resiliente en offline.
   - Opción en configuración para vaciar caché local o sincronizar clientes guardados.

---

## 4. Problemas Detectados

1. **Inconsistencia de Arquitectura en Documentación Legada:**
   - En la fuente 2 (`REPREDISL_FINAL_FUNCIONAL`), existía un intento de migrar React a Vanilla JS puro para no usar `npm` ni `vite`. Sin embargo, esto limita severamente la mantenibilidad, escalabilidad PWA y reactividad requerida para V3.
2. **Dispersión de Fuentes y Scripts:**
   - El código avanzado, la documentación detallada y los ejecutables de la API estaban repartidos en tres rutas independientes.
3. **Manejo de Variables de Entorno y Secretos:**
   - En las versiones previas existen archivos `.conf` y `.env` con cadenas de conexión PostgreSQL locales o por defecto sin estandarizar.
4. **Falta de Pruebas Automatizadas:**
   - No existen tests unitarios ni de integración formalizados para el frontend ni para la API.

---

## 5. Análisis de Riesgos

| Riesgo | Impacto | Mitigación en REPREDISL_V3 |
|---|---|---|
| **Pérdida de rendimiento en móviles si se usara Vanilla JS desestructurado** | Alto | Mantener arquitectura React + Vite con arquitectura de componentes limpios y PWA. |
| **Cuello de botella o inconsistencia en caché offline IndexedDB** | Medio | Mantener la estrategia de límite de 300 registros en IndexedDB y sincronización por lotes. |
| **Fallos de conexión PostgREST / CORS en red local o producción** | Alto | Integrar Caddy como reverse proxy y estandarizar `.env.example` con la URL unificada de la API. |
| **Exposición inadvertida de credenciales de base de datos** | Crítico | Mantener las credenciales fuera del control de versiones (`.env` en `.gitignore`). |
