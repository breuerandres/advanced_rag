# Initial Project Prompt (Historical)

This is the original Spanish prompt that kicked off the project. It has been superseded by the `context/` files. Keep it here only as historical reference.

**Source of truth for current product/architecture decisions:** `context/README.md` and the files it links to.

---

**INTRO**: Vamos a empezar un proyecto nuevo. Primero vamos a conversar sobre el diseño del proyecto y buscar las mejores soluciones. De a poco vamos a ir editando los .md de @context y cuando tengamos todo definido vamos a empezar con la implementación.

**PROYECTO**: Vamos a desarrollar un producto que se pueda vender. Se trata de una plataforma de gestión y consulta de instructivos corporativos. La idea es que se centralicen los instructivos y documentos y se puedan consultar a través de un chatbot. El sistema controla todo el ciclo de vida del instructivo.

**ARQUITECTURA**: El proyecto cuenta de dos grandes partes:

- *El módulo de gestión de instructivos*: Accede el personal de la empresa que gestiona estos documentos. Es un CRUD de los documentos. Cuenta con un front end y un back end separados. En el front debemos poder generar documentos en HTML con un editor y asignarles diferentes atributos como tipo de instructivo, accesibilidad de usuarios, etiquetas, etc. Debemos tener filtros avanzados para buscar documentos cuando estos escalen. Debe haber una pantalla de visualización de uso: Debe consumir de las auditorías de cada pregunta (usuario, pregunta, respuesta, cache hit, like).
- *Módulo RAG*: Recibe la consulta, realiza el retrieval de documentos y elabora la respuesta. Debe tener un sistema de caching de preguntas (ampliaremos el funcionamiento). Debe haber un sistema de filtrados de documentos por atributos y roles para restringir a los usuarios a los documentos que les corresponden. Esto se define mejor con cada cliente pero el proyecto debe contar con ciertos filtros a modo de ejemplo.
- *Visualizador*: Accede el consumidor de la plataforma. Cuando el chat le devuelve una respuesta al usuario debe decir de qué instructivo obtuvo la información dando un link a esta página donde se encuentra el instructivo completo. Desde aquí los instructivos tienen un tratamiento como de "red social", es decir, se pueden likear, comentar, agregar como favoritos y compartir.
- *Cliente Chat*: A modo de muestra una ventana de chat desde donde se prueba la funcionalidad del sistema. Las respuestas se pueden likear.

**TECNOLOGÍAS**:

- Frontend: React.js
- Backend: .NET 8 Core
- RAG: FastAPI
- DB: Postgres para documentos, base vectorial y auditorías.
- LLM y Embedding model: OpenAI

**HERRAMIENTAS QUE DEBEN ESTAR PRESENTES**:

- Desde el módulo de gestión de instructivos se deben auditar todos los cambios: alta, baja, modificación.
- Desde el RAG se deben auditar todas las consultas.
- Los sistemas deben generar logs en archivos independientes cada día: http request, IP origen, fecha y hora, response status y el error en caso de que falle.
- Todos los sistemas deben tener un buen sistema de error handling para que en el caso de que haya un error podamos saber a ciencia cierta desde dónde viene.
- La auditoría del RAG debe calcular siempre el costo de las consultas en base a tokens de entrada, cache y salida.

**INFRAESTRUCTURA**: Todos los servicios deben ir de la mano. Cada uno debe correr en docker y se debe orquestar el despliegue de forma automatizada con algún YAML. Necesito sugerencias de cómo manejar las variables de entorno de manera segura.

**SEGURIDAD**: Tener presente en todo momento las mejores prácticas de seguridad para reducir las vulnerabilidades del sistema.

---

## Open Items From The Original Prompt That Were Resolved In The Context Files

- "Funcionalidad de red social" (likes, comments, favorites, share) on the viewer: **deferred**. Only thumbs up/down on chat answers is in MVP scope. Social interactions on the viewer are out of scope.
- "Filtros avanzados para buscar documentos cuando escalen": resolved as a combination of attribute filters + group-based access + lifecycle state filters in the management UI; full-text search on instruction content is not in MVP scope.
- "Sugerencias de cómo manejar las variables de entorno de manera segura": resolved as Docker Compose secrets for sensitive values + env vars for non-sensitive runtime configuration. See `context/architecture.md` and `context/code-patterns.md`.
