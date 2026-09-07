# ROADMAP Y PLAN DE EVOLUCIÓN - REPREDISL V3

**Proyecto:** REPREDISL_V3  
**Fecha de actualización:** 04-09-2026  
**Repositorio Privado:** [marcpensi/ReprediSL_V3](https://github.com/marcpensi/ReprediSL_V3)  

---

## 1. Estado Actual (V3.0 - Consolidación Inicial)

- [x] **Auditoría e Integración:** Unificación del código avanzado React 18 + Vite de `Definitiva_v1` en `ReprediSL_V3/Frontend`.
- [x] **Compilación Limpia:** `npm run build` verificado exitosamente (1800 módulos, 7.12s, 0 errores).
- [x] **Backend API:** Binarios y scripts de `API_REPREDISL_WINDOWS` integrados en `ReprediSL_V3/API`.
- [x] **Seguridad:** Configuración segura `postgrest.conf.example` y exclusión estricta de `.env` y secretos en `.gitignore`.
- [x] **Documentación Viva:** Estructura numerada de carpetas (`00_EstadoProyecto` a `07_HistorialCambios`) y ADRs preliminares.
- [x] **Git & GitHub:** Control de versiones local configurado en rama `main` listo para su vinculación remota.

---

## 2. Objetivos Principales V3

1. Proporcionar a la fuerza de ventas una herramienta ágil, reactiva y offline-first en dispositivos móviles.
2. Garantizar la generación instantánea de pedidos en formato PDF listos para enviar por email o WhatsApp.
3. Asegurar la sincronización resiliente con la API PostgREST y la base de datos PostgreSQL.
4. Mantener una arquitectura limpia, profesional y totalmente mantenible mediante componentes React y TypeScript/ES6+.

---

## 3. Próximas Fases del Proyecto

### Fase 3.1 - Capacidades PWA y Mobile Offline (Siguiente Hito)
- Instalación de Manifest Web (`manifest.json`) e iconos de aplicación para móviles.
- Implementación de Service Worker para caché estática offline del bundle web.
- Mejora del indicador de estado de red (Online / Offline) en el header de la aplicación.

### Fase 3.2 - Integración Completa de Tarifas y Productos
- Conexión del esquema de la tabla `tarifas` de PostgREST con la lógica de cálculo de precios en el Frontend.
- Sincronización avanzada de existencias y productos en segundo plano.

### Fase 3.3 - Autenticación y Gestión de Usuarios
- Implementación de autenticación JWT mediante PostgREST.
- Sistema de roles (Comercial, Administrador, Supervisión).

---

## 4. Lista de Pendientes (Backlog Inmediato)

- [ ] Vincular el remoto origin de GitHub (`git remote add origin https://github.com/marcpensi/ReprediSL_V3.git`).
- [ ] Subir la rama `main` al repositorio privado (`git push -u origin main`).
- [ ] Configurar variables de entorno específicas para el servidor de staging/producción (`.env`).
- [ ] Iniciar pruebas end-to-end de creación de pedidos en dispositivo móvil real.
