@echo off
title ReprediSL V4 - Sincronizador de Pedidos (PostgreSQL - Access)
color 0B
echo =========================================================
echo REPREDISL V4 - SINCRONIZADOR DE PEDIDOS EN SEGUNDO PLANO
echo =========================================================
echo Monitoreando nuevos pedidos en PostgreSQL psgest-online...
echo Notificando al Demonio de Bandeja (ReprediTrayDaemon)
echo e insertando en Microsoft Access (gestion.mdb)
echo =========================================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\BaseDatos\SincronizarPedidosEntrantes.ps1" -Loop -IntervaloSegundos 3

pause
