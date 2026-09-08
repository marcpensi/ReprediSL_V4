---
title: PsAgents Framework & Roles
tags:
  - psagents
  - gobernanza
  - agentes-ia
  - agents-md
version: 4.1.0
---

# 🧠 PsAgents Framework & Roles

Relacionado con: [[00 - Índice General (MOC)]] | [[01 - Arquitectura ReprediSL_V4]]

## 1. Concepto
**PsAgents** es el framework de gobernanza y coordinación multagente con IA para `ReprediSL_V4`.

## 2. Los 5 Roles Principales

### 🧠 Coordinador Principal
- Gestión de hitos, commits explícitos y tagging Git (`v4.1.0`). Protege las carpetas origen.

### 🏛️ Agente de Arquitectura
- Vela por el patrón Offline-First, IndexedDB v2 y la migración a `.NET 10`.

### 🔍 Agente Auditor de Código
- Revisa diffs, valida `DROP CASCADE` en SQL y asegura cero exposición de credenciales.

### 📝 Agente de Documentación
- Mantiene la documentación viva numerada (`00_EstadoProyecto.md`, `README.md` y notas de Obsidian).

### 🧪 Agente de Testing
- Pruebas estáticas (`npm run build`), unitarias (`npm test`) y validación de sincronización.
