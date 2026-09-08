---
title: Access VBA & PostgreSQL Sync
tags:
  - access
  - vba
  - postgresql
  - modActBdApi
version: 4.1.0
---

# 🗄️ Access VBA & PostgreSQL Sync

Relacionado con: [[00 - Índice General (MOC)]] | [[03 - Demonio C# .NET 10 & Sync]]

## 1. Módulo VBA `modActBdApi.bas`
- **Ubicación:** `src/Access/modActBdApi.bas`
- **Consultas exportadas:**
  - `QryClientesApi` (3.180 registros)
  - `QryVendedoresApi` (13 registros)
  - `QryTarifasApi` (14 registros)
  - `QryPreciosApi` (2.840 registros)
  - `QryUventasApi` (938 registros)
- **Estrategia DDL:** `DROP TABLE public."tabla" CASCADE` para regenerar estructuras automáticamente sin conflictos de columnas en Postgres.
- **Script Executor:** `Scripts/BaseDatos/EjecutarExportacionAccess.ps1`
