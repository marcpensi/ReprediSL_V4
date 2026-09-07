# REGISTRO DE DECISIONES TÉCNICAS (ADR) - REPREDISL V3

---

## ADR-001: Mantenimiento del Stack React 18 + Vite frente a Vanilla JS

- **Estado:** ACEPTADO  
- **Fecha:** 04-09-2026  
- **Contexto:** En el repositorio de referencia `REPREDISL_FINAL_FUNCIONAL` existía una propuesta para refactorizar la aplicación a Vanilla JS. Sin embargo, la fuente `REPREDISL_Definitiva_v1` cuenta con una arquitectura basada en React 18 + Vite plenamente funcional, reactiva y con módulos limpios.
- **Decisión:** Mantener React 18 y Vite como base del Frontend en `ReprediSL_V3/Frontend`.
- **Consecuencias:** Permite construir componentes modularizados, facilitar la conversión a PWA, acelerar el desarrollo con HMR y mantener la generación de PDF con `jsPDF` de forma aislada y mantenible.

---

## ADR-002: Persistencia con IndexedDB y estrategia de Caché Limitada (300 Registros)

- **Estado:** ACEPTADO  
- **Fecha:** 04-09-2026  
- **Contexto:** Descargar miles de clientes en dispositivos móviles agota la memoria del navegador y la tasa de transferencia de datos.
- **Decisión:** Adoptar la estrategia de IndexedDB con límite de 300 clientes en la caché local y descarga bajo demanda (lote de 30 por consulta PostgREST).
- **Consecuencias:** Velocidad inmediata en la experiencia de usuario, soporte offline resiliente y bajo consumo de recursos en smartphones.

---

## ADR-003: Estructura Monorepo Desacoplada (Frontend + API + Documentación)

- **Estado:** ACEPTADO  
- **Fecha:** 04-09-2026  
- **Contexto:** El proyecto requería integrar código, API Windows y documentación en una sola raíz coherente.
- **Decisión:** Estructurar `ReprediSL_V3` en subcarpetas especializadas (`Frontend`, `API`, `Documentacion/00..07`, `Pruebas`, `Scripts`, `Backups`, `Recursos`).
- **Consecuencias:** Organización clara para el equipo de desarrollo y los agentes locales de IA.
