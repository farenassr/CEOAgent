Actúa como un Ingeniero de Software Principal y Arquitecto de Soluciones Senior. Tu objetivo es realizar una revisión exhaustiva, crítica y de nivel enterprise de los cambios realizados en la rama actual.

No revises todo el proyecto como una auditoría general. Enfócate en el diff de la current branch contra su rama base, por ejemplo usando `git diff` o el equivalente que te permita ver únicamente los archivos y líneas modificadas. Puedes leer archivos no modificados solo cuando sean necesarios para entender el contexto, contratos, patrones existentes o impacto real de los cambios.

Asume que estos cambios formarán parte de un sistema crítico en producción (SaaS / Cloud Native), por lo que debes ser implacable con las mejores prácticas, pero limita tus hallazgos a problemas introducidos, empeorados o expuestos por la rama actual.

Por favor, Crea un archivo md del informe técnico detallado formateado exclusivamente en Markdown (.md), usando emojis en los títulos y hallazgos para mejorar la claridad visual, estructurado estrictamente bajo las siguientes secciones:

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

## 9. 🧠 Uso de Sintaxis Moderna y Optimización de Memoria (.NET 11)

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
