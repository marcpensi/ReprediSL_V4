# ADR-001: Arquitectura Base del Frontend (React + Vite)

**Estado:** ACEPTADO  
**Fecha:** 04-09-2026  
**Autor:** Coordinador Principal REPREDISL_V3  

---

## 1. Decisión
Mantener **React 18 + Vite** como el framework y entorno de desarrollo base para el frontend en `ReprediSL_V3/Frontend`.

---

## 2. Motivo
- **Mantenibilidad:** El uso de una arquitectura basada en componentes modulares facilita el mantenimiento y la resolución de incidencias en comparación con código imperativo en Vanilla JS.
- **Escalabilidad y Ecosistema:** Permite la incorporación rápida de librerías avanzadas (ej. `jsPDF` para la generación de albaranes y pedidos, `lucide-react` para iconografía corporativa).
- **Preparación PWA Móvil:** Facilita la conversión rápida de la SPA en una PWA instalable en dispositivos móviles para la fuerza de ventas.
- **Rendimiento:** HMR (Hot Module Replacement) instantáneo en desarrollo y empaquetado ultra-rápido en producción mediante Vite (`npm run build` en ~7s).

---

## 3. Alternativas Evaluadas
- **Vanilla JavaScript (ES6+):** Evaluada en el repositorio documental `REPREDISL_FINAL_FUNCIONAL`. Aunque eliminaba la necesidad de un bundler, incrementaba la complejidad de gestión del DOM manual, complicaba la gestión de estado asíncrono con IndexedDB y ralentizaba el desarrollo de nuevas características reactivas.

---

## 4. Resultado
**React/Vite será la base oficial del Frontend de REPREDISL V3.** Se consolida en `ReprediSL_V3/Frontend` manteniendo el rendimiento offline-first respaldado por IndexedDB y la comunicación asíncrona mediante PostgREST.
