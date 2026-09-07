# REGLAS DE COLABORACIÓN Y FLUJO DE PROMPTS (ChatGPT <-> Antigravity <-> Usuario)

Este documento define el contrato de trabajo colaborativo entre el **Usuario**, **ChatGPT** y **Antigravity** para ReprediSL_V4.

---

## ROLES Y RESPONSABILIDADES

### 1. El Usuario
- **Función:** Es el director del proyecto y tomador final de decisiones.
- **Responsabilidad:** Plantea las necesidades, ideas, objetivos y requerimientos de negocio.
- **Regla:** Ninguna sugerencia técnica o cambio arquitectónico se aplica definitivamente sin la aprobación explícita del Usuario.

### 2. ChatGPT (Convertidor y Refinador de Prompts)
- **Función:** Asistente conversacional de diseño y optimizador de instrucciones.
- **Responsabilidades:**
  1. Recibir las ideas y peticiones brutas del Usuario.
  2. Convertir, enriquecer y estructurar dichas peticiones en **prompts técnicos precisos** para Antigravity.
  3. Formular sugerencias o alternativas de mejora para que el Usuario las revise y confirme antes de decidir su ejecución.
  4. Registrar las instrucciones mejoradas en `Prompts/Intercambio/PROMPTS_CHATGPT.md`.

### 3. Antigravity (Agente Ejecutor e Integrador)
- **Función:** Asistente técnico de programación, desarrollo y ejecución local.
- **Responsabilidades:**
  1. Leer y procesar las instrucciones refinadas por ChatGPT.
  2. Ejecutar las tareas de código, pruebas, diagnósticos o arquitectura solicitadas.
  3. Presentar propuestas de diseño al Usuario cuando sea necesario.
  4. Documentar los resultados y dejar el informe de ejecución en `Prompts/Intercambio/RESULTADOS_ANTIGRAVITY.md`.

---

## FLUJO DE TRABAJO (WORKFLOW)

```text
  [Usuario] ──(Expresa su idea)──> [ChatGPT]
                                      │
                                      ├─ (Mejora/Optimiza el prompt)
                                      └─ (Propone sugerencias)
                                      │
  [Usuario] <──(Confirma/Aprueba)─────┘
     │
     ▼
[Antigravity] ──(Ejecuta código/pruebas)──> [RESULTADOS_ANTIGRAVITY.md]
```

---

## REGLA FUNDAMENTAL DE CONFIRMACIÓN

> **"Toda sugerencia o propuesta realizada por ChatGPT o Antigravity debe ser presentada al Usuario. La decisión final de ejecución siempre recae en la confirmación previa del Usuario."**

