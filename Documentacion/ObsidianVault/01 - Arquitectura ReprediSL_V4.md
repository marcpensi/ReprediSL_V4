---
title: Arquitectura ReprediSL_V4
tags:
  - arquitectura
  - pwa
  - react
  - postgrest
  - offline-first
version: 4.1.0
---

# 🏛️ Arquitectura del Sistema - ReprediSL_V4

Relacionado con: [[00 - Índice General (MOC)]] | [[02 - PsAgents Framework & Roles]] | [[03 - Demonio C# .NET 10 & Sync]]

## 1. Visión General
`ReprediSL_V4` es una aplicación desacoplada **Offline-First** orientada a movilidad comercial.

- **Frontend:** React 18.3.1 + Vite 6.0.5 + IndexedDB v2.
- **Backend API:** PostgREST 16.0 + Proxy inverso Caddy.
- **Base de Datos Central:** Microsoft Access `gestion.mdb` (ERP local) + PostgreSQL 12+ (`repredisl_api`).
- **Proceso de Fondo:** [[03 - Demonio C# .NET 10 & Sync]].

## 2. Flujo de Datos
1. La PWA lee de `IndexedDB` para funcionamiento instantáneo sin cobertura móvil.
2. Al haber conexión, realiza debounce y peticiones REST contra PostgREST (`api.repredisl.com` / `127.0.0.1:3000`).
3. El módulo [[04 - Access VBA & PostgreSQL]] vuelca en lotes desde `gestion.mdb` hacia PostgreSQL.
