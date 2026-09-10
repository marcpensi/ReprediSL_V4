# Auditoría Integral del Proyecto · ReprediSL_V4

**Fecha de Auditoría:** 11-09-2026  
**Versión Auditada:** 4.8.1  
**Entorno de Producción:** `https://pedidos.repredisl.com` (Frontend PWA en Hostinger)  
**API Gateway:** `https://api.repredisl.com` (PostgREST 16.0 + Caddy Proxy SSL)  
**ERP Origen / Destino:** Microsoft Access (`gestion.mdb` / PsGest `C:\pensi\psgestw\e0012026\gestion.mdb`)  
**Centro de Control:** `ReprediTrayDaemon.exe` (Nativo .NET 10 WinForms System Tray)

---

## 1. Resumen Ejecutivo del Proyecto

`ReprediSL_V4` es la solución consolidada de digitalización comercial y gestión de pedidos para la fuerza de ventas en ruta de **REPREDISL**. El ecosistema resuelve de forma integral el ciclo comercial preventa y postventa conectando dispositivos móviles remotos (PWA Offline-First) con el sistema central ERP corporativo en Microsoft Access a través de una arquitectura desacoplada de alto rendimiento respaldada por PostgreSQL y PostgREST.

```
┌─────────────────────────────────────────────────────────────┐
│                 PWA Remota (Fuerza de Ventas)               │
│  React 18 + Vite · IndexedDB v2 · Service Worker Offline    │
│            https://pedidos.repredisl.com                    │
└──────────────────────────────┬──────────────────────────────┘
                               │ HTTPS (POST /pedidos)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│             Caddy Reverse Proxy + PostgREST 16              │
│                 https://api.repredisl.com                   │
└──────────────────────────────┬──────────────────────────────┘
                               │ SQL
                               ▼
┌─────────────────────────────────────────────────────────────┐
│               PostgreSQL 16 (repredisl_api)                 │
│      Tablas: clientes, catalogo, tarifas, historial...      │
│      Buffer entrante: public.pedidos_nuevos                 │
└──────────────────────────────┬──────────────────────────────┘
                               │ Sincronización Bidireccional
                               ▼
┌─────────────────────────────────────────────────────────────┐
│               Microsoft Access (gestion.mdb)                │
│   Exportación: modActBdApi.bas (Consultas qry*api)          │
│   Importación: SincronizarPedidosEntrantes.ps1              │
│   Tablas ERP: PedidosCab, PedidosLin (Serie 'VD')           │
└─────────────────────────────────────────────────────────────┘
                               ▲
                               │ Monitorización y Gestión
┌─────────────────────────────────────────────────────────────┐
│             Centro de Control (ReprediTrayDaemon)           │
│   Procesos: Postgres · PostgREST · Caddy · Sync · ERP       │
│   KPIs Responsivos · Auto-Aceptar / Manual · Telemetría     │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Qué HACE el Proyecto (Capacidades Activas y Operativas)

### 2.1. Frontend PWA Comercial (React 18 + Vite)
- **Operatividad Offline-First Real:** Permite a los agentes comerciales operar en zonas sin cobertura de red mediante Service Worker (`sw.js`) y base de datos local `IndexedDB` (versión 2, object store `productos`, `clientes`, `tarifas`, `historial`, `pedidos_locales`).
- **Búsqueda Reactiva con Debounce:** Búsqueda instantánea en catálogo de artículos y clientes con filtrado reactivo optimizado.
- **Filtrado de Historial por Cliente Seleccionado:** El historial de ventas (`historial` procedente de `qryuventasapi`) se filtra dinámicamente según el cliente cargado en la sesión de pedido, permitiendo consultar precios y artículos adquiridos previamente.
- **Gestión Local de Pedidos y Exportación a PDF:** Los pedidos se crean, calculan y persisten localmente en el terminal remoto. Generación instantánea de albarán/pedido en formato PDF mediante `jsPDF` con desglose de bases, tipos impositivos y totales.
- **Sincronización Automática con la Nube:** Al recuperar conectividad, los pedidos pendientes se transmiten a la API mediante HTTP POST (`/pedidos`).
- **Modal de Artículos Optimizado:** Ventana modal de añadir artículos (`AddModal`) con control de estado y estilos de alto contraste en el botón de confirmación.

### 2.2. Backend API y Servicios (PostgREST + Caddy)
- **PostgREST 16.0:** Exposición automática y segura de la capa relacional PostgreSQL como API RESTful tipada.
- **Caddy Reverse Proxy:** Gestión automatizada de certificados TLS/SSL HTTPS con compresión gzip/zstd y mapeo seguro de puertos (3000 PostgREST $\rightarrow$ 443 HTTPS público).
- **Control de Acceso y Roles:** Roles de base de datos (`web_anon`, `authenticator`) con permisos de solo lectura para maestros y permisos de inserción controlada para pedidos nuevos.

### 2.3. Sincronización Canónica Access $\rightarrow$ PostgreSQL (`modActBdApi.bas`)
- **Detección Dinámica Estricta (`qry*api`):** Exporta exclusivamente las consultas existentes cuyo nombre comience por `qry` y finalice por `api`.
- **Mapeo Canónico de Tablas Destino:**
  - `qryuventasapi` $\rightarrow$ `historial`
  - `qrypreciosapi` $\rightarrow$ `catalogo` (tarifa 1 o configurable)
  - `qrytarifasapi` $\rightarrow$ `tarifas`
  - Resto de consultas $\rightarrow$ nombre base sin prefijo/sufijo (`clientes`, `vendedores`, etc.).
- **Esquemas Limpios con `DROP CASCADE`:** Regenera automáticamente en PostgreSQL las tablas con `DROP TABLE ... CASCADE` para garantizar coherencia estructural exacta con los tipos DAO de Access.
- **Exportación por Lotes (`BATCH_SIZE = 500`):** Inserción masiva de miles de registros en segundos sin saturar memoria ni bloqueos de red.

### 2.4. Pipeline de Retorno de Pedidos (PostgreSQL $\rightarrow$ Access ERP)
- **Ingesta en Tiempo Real:** El script `SincronizarPedidosEntrantes.ps1` detecta pedidos depositados en `public.pedidos_nuevos`.
- **Inserción en ERP PsGest:** Registra de forma transaccional la cabecera en `PedidosCab` y el detalle en `PedidosLin` dentro de `gestion.mdb`.
- **Identificación de Serie Comercial:** Aplica la serie de vendedor asignada (`VD`), calculando el siguiente número de pedido correlativo disponible y respetando la lógica comercial del ERP.

### 2.5. Centro de Control de Escritorio (`ReprediTrayDaemon`)
- **Monitorización de 5 Servicios Clave:**
  1. 🐘 **PostgreSQL** (`:5432 · repredisl_api`)
  2. ⚡ **PostgREST API** (`:3000 · REST API`)
  3. 🌐 **Caddy Proxy** (`:443 · SSL HTTPS`)
  4. 🔄 **Sync Pedidos** (`Postgres ➔ Access`)
  5. 📄 **Access ERP** (`gestion.mdb`)
- **Diseño KPI Anti-Solapamiento:**
  - Iconos presentados en badges dedicados independientes de `(36x36px)`, eliminando cualquier recorte o interferencia física sobre el texto.
  - Textos y subtítulos limpios, formateados con alineación fija a `X = 52px`.
  - Botones de control individual de 72px («⏹️ Parar», «▶️ Iniciar») con visibilidad completa de textos y estados.
- **Responsividad Total del Formulario:**
  - Distribución en `TableLayoutPanel` de 5 columnas proporcionales (20% cada una) que se adaptan a cualquier ancho de pantalla (desde 1000px hasta resoluciones Ultra-Wide y 4K).
  - Escalado automático de las 8 tarjetas principales (`pnlHeader`, `pnlServicesCard`, `pnlModeSwitch`, `pnlPipelineDiagram`, `pnlMetricCards`, `flowSubMetrics`, `pnlLogConsoleCard`, `pnlStatusBar`).
  - Diagrama de pipeline dinámico: nodos centrados y líneas de conexión recalculadas en tiempo real.
- **Modos de Operación:**
  - `⚡ Auto-Aceptar`: Inserción inmediata de pedidos en Access.
  - `✋ Confirmación Manual`: Cola de retención con confirmación explícita por el usuario.
- **Telemetría y Registro en Tiempo Real:** Métricas de Uptime, pedidos procesados hoy, pedidos en espera, reloj sincronizado y consola RichText con scroll automático y filtros de nivel de log.
- **Codificación Estricta:** Cumplimiento de **UTF-8 con BOM** para preservación de emojis y caracteres en español.

---

## 3. Qué NO HACE el Proyecto (Límites y Exclusiones Intencionadas)

Para preservar la integridad del sistema y garantizar la estabilidad del ERP empresarial, el proyecto delimita estrictamente su alcance:

1. **NO altera tablas ni esquemas nativos de Access ERP:**
   - La rutina de sincronización `modActBdApi.bas` fue despojada de funciones intrusivas como `ActualizarEstructuraAccess`. No se añaden campos artificiales ni se modifican las tablas de producción del ERP.
2. **NO permite la edición de pedidos ya consolidados desde el terminal remoto:**
   - Una vez que un pedido se sincroniza e ingresa en `PedidosCab`/`PedidosLin` de Access ERP, la PWA remota no puede modificarlo ni eliminarlo. La gestión de facturación, preparación y anulación corresponde exclusivamente al personal autorizado en el ERP central.
3. **NO expone credenciales maestras ni claves de administración:**
   - La PWA no tiene acceso directo a la cadena de conexión de PostgreSQL ni a contraseñas administrativas. Toda la comunicación pasa por PostgREST restringido y Caddy con certificados SSL.
4. **NO inventa campos ni consultas que no existan en Access:**
   - El sistema se ciñe estrictamente a las consultas `qry*api` parametrizadas en la base de datos Access. Si un dato no está en la consulta, no se genera sintéticamente.
5. **NO requiere dependencia de internet continua:**
   - El terminal comercial no se bloquea ante cortes de fibra o zonas rurales sin cobertura; todo el catálogo, clientes y creación de pedidos permanece disponible en la base de datos local del navegador.

---

## 4. Objetivos Conseguidos (Cronograma de Hitos)

| Versión | Hito Principal Conseguido | Estado |
| :--- | :--- | :---: |
| **v4.0.0** | Consolidación de arquitectura V4, React 18, Vite y PostgREST 16 | Completado |
| **v4.1.0** | Soporte PWA Offline-First con IndexedDB v2 y Service Worker | Completado |
| **v4.2.0** | Suite de pruebas unitarias automatizadas (5/5 tests pasados) | Completado |
| **v4.3.0** | Demonio nativo C# WinForms .NET 10 con bandeja de sistema | Completado |
| **v4.4.0** | Sincronización PostgreSQL dinámica con `DROP CASCADE` en VBA | Completado |
| **v4.5.0** | Centro de Control unificado con gestión de procesos y servicios | Completado |
| **v4.6.0** | Pipeline bidireccional PWA $\rightarrow$ PostgREST $\rightarrow$ Access ERP | Completado |
| **v4.7.0** | Integración con ERP de producción PsGest (`C:\pensi\...`) y serie `VD` | Completado |
| **v4.8.0** | Sincronización canónica estricta `qry*api` (uventas $\rightarrow$ historial, precios $\rightarrow$ catálogo), corrección modal artículos y despliegue en Hostinger | Completado |
| **v4.8.1** | Rediseño de tarjetas KPI de servicios (anti-overlap) y responsividad integral de la interfaz de escritorio | Completado |

---

## 5. Auditoría de Calidad y Verificación de Código

### 5.1. Pruebas Unitarias Automatizadas
- **Suite:** Node.js native test runner (`node --test ../../tests/*.test.js`)
- **Resultado:** **5 pasadas / 0 fallidas** (100% éxito)
  - `normalizeClient`: Cobertura de normalización de datos PostgREST $\rightarrow$ PWA.
  - `normalizeProduct`: Normalización de tipos y valores por defecto seguros.
  - `calculateOrderTotal`: Precisión en cálculo de bases imponibles, 21% IVA y totales.
  - `calculateOrderTotal`: Gestión segura de pedidos vacíos.

### 5.2. Compilación del Demonio C# .NET 10
- **Proyecto:** `ReprediTrayDaemon.csproj` (.NET 10.0 Windows Forms)
- **Resultado de Compilación:** **0 Errores, 0 Advertencias**
- **Codificación:** UTF-8 con BOM (Byte Order Mark `EF BB BF`) verificado mediante script de firma binaria.

### 5.3. Despliegue en Producción
- **Frontend:** Compilado con `vite build` y desplegado en servidor de producción Hostinger (`pedidos.repredisl.com`).
- **Integridad:** Rutas SPA configuradas, HTTPS forzado con certificados Let's Encrypt / Caddy.
