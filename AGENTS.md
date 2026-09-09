# AGENTS.md

## Proyecto

ReprediSL_V4

Ruta:

D:\programacio\repredi\ReprediSL_V4

## Reglas

- No borrar ni sobrescribir archivos sin necesidad.
- Respetar arquitectura existente.
- Documentar cambios importantes.
- Mantener `Documentacion\README.md`.
- Mantener `Scripts\README.md`.
- Guardar scripts en `Scripts`.
- Buscar reutilización antes de crear herramientas nuevas.
- No exponer secretos.
- En consolidaciones, no modificar orígenes.

## Codificación de Archivos (REGLA CRÍTICA)

- **TODOS los archivos `.cs` deben guardarse en UTF-8 con BOM** (UTF-8 with BOM, Codepage 65001).
- Esto es obligatorio para que los emojis (`🗄️`, `⚡`, `✋`, `🟢`...) y los caracteres españoles (`ñ`, `á`, `é`, `ó`, `ú`, `ü`, `¿`, `¡`...) se muestren correctamente en el código fuente y en la interfaz de usuario en tiempo de ejecución.
- Si al abrir un `.cs` se ven caracteres corruptos como `ðŸ—„ï¸` o `Ã±`, significa que el archivo NO está en UTF-8 con BOM. En ese caso, re-guardar el archivo con la codificación correcta antes de cualquier otra edición.
- En Visual Studio / VS Code: `Archivo → Guardar con codificación → UTF-8 con firma (BOM)`.
- Esta regla aplica especialmente a `MainForm.cs` y cualquier archivo con texto en pantalla visible para el usuario.

## Estado

Ver `Documentacion\00_EstadoProyecto.md`.

