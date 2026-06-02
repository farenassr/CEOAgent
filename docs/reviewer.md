No hagas ningún cambio en el código, solo genera un archivo MD dentro de la carpeta docs/CODE_REVIEW.
Actúa como un Ingeniero de Software Principal y Arquitecto de Soluciones Senior. Tu objetivo es realizar una revisión exhaustiva, crítica y de nivel enterprise de los cambios realizados en la rama actual.

No revises todo el proyecto como una auditoría general. Enfócate en el diff de la current branch contra su rama base, por ejemplo usando `git diff` o el equivalente que te permita ver únicamente los archivos y líneas modificadas. Puedes leer archivos no modificados solo cuando sean necesarios para entender el contexto, contratos, patrones existentes o impacto real de los cambios.

Asume que estos cambios formarán parte de un sistema crítico en producción (SaaS / Cloud Native), por lo que debes ser implacable con las mejores prácticas, pero limita tus hallazgos a problemas introducidos, empeorados o expuestos por la rama actual.

Por favor, Crea un archivo md del informe técnico detallado formateado exclusivamente en Markdown (.md), usando emojis en los títulos y hallazgos para mejorar la claridad visual, estructurado estrictamente bajo las siguientes secciones:

al final del informe crea una tabla con los siguientes datos: | # | Severidad | Hallazgo | Sección |

### 🧭 Contexto de Harness Engineering

Este repositorio usa harness engineering.

Antes de revisar:

1. Lee `AGENTS.md`.
2. Ejecuta o inspecciona `git status --short`.
3. Determina la rama base y revisa el diff de la rama actual contra esa base usando `git diff` o equivalente.
4. No revises todo el proyecto como auditoría general.
5. Enfócate en archivos y líneas modificadas.
6. Puedes leer archivos no modificados solo cuando sean necesarios para entender contexto, contratos, patrones existentes o impacto real.
7. Lee documentos de `AIHarness/` solo si son relevantes para los cambios.
8. Usa `scripts/` en lugar de comandos ad-hoc cuando necesites validar algo.

### 🤖 Uso de Subagentes

Usa los subagentes disponibles en `.codex/agents/` cuando sean relevantes para el diff.

Subagentes disponibles:

- `architecture-reviewer`: arquitectura, modular monolith, vertical slices, límites entre proyectos, acoplamiento y diseño.
- `backend-engineer`: implementación .NET, FastEndpoints, Mediator, Worker jobs, servicios, handlers y lógica de aplicación.
- `db-specialist`: EF Core, PostgreSQL, JSONB, índices, query filters, tenant isolation, queries y persistencia.
- `integrations-engineer`: ports/adapters, WhatsApp Cloud, Google Calendar, APIs externas, SDKs y tool execution.
- `ai-engineer`: Microsoft Agent Framework, agent loop, prompts, structured output, tool calling, LLM safety y evals.
- `testing-engineer`: pruebas unitarias, integración, Aspire Testing, Testcontainers, fixtures, evals y regresiones.
- `code-simplifier`: redundancia, simplificación de código, queries duplicadas, LINQ/EF Core readability y refactors behavior-preserving.
- `codebase-scout`: lectura inicial del contexto, archivos relevantes, patrones existentes y resumen del estado actual, si existe.

Si los cambios son complejos, primero usa `codebase-scout` para resumir el contexto del diff.

Luego ejecuta en paralelo solo los subagentes relevantes. No uses todos por defecto.

Cada subagente seleccionado debe devolver:

1. Riesgos principales.
2. Archivos o áreas relevantes.
3. Hallazgos accionables.
4. Pruebas, evals o docs que deberían agregarse o actualizarse.
5. Cosas que no deberían cambiarse.

## 1. 🧭 Conclusiones Generales

- Un resumen ejecutivo sobre la calidad general de los cambios de la rama actual, legibilidad, mantenibilidad y coherencia con los módulos afectados.

- Calificación cualitativa del estado de los cambios revisados (Excelente / Aceptable / Requiere Refactorización Urgente).

## 2. 🔐 Seguridad

- Identificación de vulnerabilidades, fallos lógicos, validación deficiente de entradas o riesgos de inyección.

- Manejo inadecuado de datos sensibles, secretos, configuraciones, tokens, variables de entorno o excepciones que puedan revelar información interna del sistema.

## 3. ⚡ Performance (Rendimiento)

- Detección de cuellos de botella, asignaciones de memoria innecesarias en el Heap u operaciones bloqueantes.

- Optimización de algoritmos, bucles, colecciones, consultas a base de datos, llamadas externas o llamadas asíncronas.

## 4. 📈 Problemas de Escalabilidad

- Análisis de cómo se comportará este proyecto bajo escenarios de alta concurrencia o cargas masivas de datos.

- Evaluación de impactos en entornos distribuidos, multi-tenant, cloud native o de alta disponibilidad.

## 5. 📊 Observabilidad y Telemetría

- Evaluación del manejo de excepciones (evitar bloques catch vacíos o silenciosos).

- Análisis de si implementa logging estructurado adecuado o si requiere la adición de trazas, métricas, correlation IDs, health checks y monitoreo para ser rastreable en producción.

## 6. 🧵 Concurrencia y Thread Safety (Seguridad de Hilos)

- Búsqueda de condiciones de carrera (race conditions), bloqueos innecesarios (deadlocks) o mal uso del estado en memoria.

- Evaluación del comportamiento de instancias compartidas (Singleton), estáticas, servicios registrados en DI, background services, colas o consumidores si aplica.

## 7. 🏗️ Cumplimiento Arquitectónico y Acoplamiento

- Análisis de si el proyecto respeta límites de responsabilidad entre capas, módulos, features o bounded contexts.

- Identificación de acoplamiento rígido, dependencias cruzadas, violaciones de arquitectura o mezcla indebida de responsabilidades.

- [OPCIONAL: Evalúa si cumple estrictamente con las reglas de: Especifica aquí tu arquitectura si aplica, ej. Vertical Slice Architecture / Clean Architecture / DDD / Modular Monolith].

## 8. 🧪 Testabilidad

- Análisis de qué tan fácil o difícil sería escribir pruebas unitarias, de integración, contract tests o end-to-end tests para este proyecto.

- Identificación de dependencias ocultas o difíciles de simular (mockear) y sugerencias para desacoplarlas.

## 9. 🧠 Uso de Sintaxis Moderna y Optimización de Memoria (.NET 10)

- Sugerencias para aplicar las características más recientes del lenguaje utilizado, incluyendo azúcar sintáctico moderno y patrones recomendados.

- Propuestas para reducir la presión del Garbage Collector mediante técnicas de asignación cero (zero-allocation) si el rendimiento es crítico.

## 10. ☁️ Optimización de Costos en Azure (Cloud FinOps & Resource Efficiency)

- Identificación de ineficiencias que disparen el uso de CPU y memoria, impactando directamente en las métricas de escalado de Azure (App Services, Container Apps o Azure Functions) y aumentando la facturación.

- Detección de fugas de memoria, retención innecesaria de objetos en el Heap o mal manejo de conexiones, como reutilización deficiente de HttpClient, conexiones a bases de datos, clientes SDK, colas o storage.

## 11. 📐 Buenas Prácticas de Ingeniería y Estándares de Diseño

- Evaluación del cumplimiento de principios SOLID, DRY (Don't Repeat Yourself), KISS (Keep It Simple, Stupid) y separación de responsabilidades.

- Análisis de mantenibilidad, consistencia en convenciones de nombrado, estructura limpia, claridad de la lógica de negocio y coherencia de los cambios con el repositorio existente.

---

### 📌 Reglas de Formato para los Hallazgos:

Reporta solo hallazgos accionables relacionados con los cambios de la rama actual. Si detectas problemas preexistentes que no fueron modificados ni agravados por esta rama, menciónalos únicamente como contexto breve o ignóralos.

Para cada problema o hallazgo crítico detectado en las secciones técnicas, debes estructurarlo estrictamente de la siguiente manera:

- **🚨 Descripción del problema:** Qué está mal, por qué es un riesgo y qué principio viola.

- **🔥 Impacto potencial:** Qué pasaría en un entorno de producción real bajo estrés si no se corrige.

- **🛠️ Propuesta de Refactorización:** El bloque de código corregido, aplicando las mejores prácticas y optimizaciones avanzadas del lenguaje.

- **✅ Recomendación adicional:** Explica brevemente si este cambio requiere pruebas, cambios arquitectónicos, documentación o monitoreo adicional.
