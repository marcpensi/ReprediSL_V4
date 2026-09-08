---
title: Demonio C# .NET 10 & Sync
tags:
  - demonio
  - csharp
  - net10
  - system-tray
version: 4.1.0
---

# ⚡ Demonio C# .NET 10 & Sync (`ReprediTrayDaemon`)

Relacionado con: [[00 - Índice General (MOC)]] | [[01 - Arquitectura ReprediSL_V4]] | [[04 - Access VBA & PostgreSQL]]

## 1. Características Técnicas
- **Ubicación:** `src/Daemon/ReprediTrayDaemon/`
- **Binario:** `src/Daemon/bin/ReprediTrayDaemon.exe`
- **Framework:** .NET 10 WinForms (C# nativo).
- **Mutex:** `Local\ReprediSL_V4_Daemon_Mutex` (Asegura 1 sola instancia sin pedir elevación de administrador).
- **Gestión de Errores:** Al superar 3 errores en `sync_progress.log` o `sync_errors.log`, muestra un modal interactivo con opciones: *Detener*, *Silenciar Ventanas*, *Continuar*.
