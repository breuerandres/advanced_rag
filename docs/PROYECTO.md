# Advanced RAG Document Platform — Documentación del proyecto

> Documento técnico integral. Quien lo lea debería entender el producto, la arquitectura,
> las decisiones de tecnología, el sistema de autenticación y el flujo RAG completo, de
> punta a punta. La **fuente de verdad** es el código del repositorio; este documento lo
> describe y explica. Los identificadores de código, modelos y comandos se mantienen en
> inglés; la prosa explicativa está en español.

---

## 1. Resumen del proyecto

**Advanced RAG Document Platform** es un producto **single-tenant** de gestión documental
corporativa con **chat RAG** (Retrieval-Augmented Generation). Cada cliente recibe un stack
aislado (su propia base de datos, su almacenamiento de objetos, sus secretos y sus logs)
desplegado con Docker Compose.

El producto cubre dos grandes flujos:

1. **Gestión documental.** Administradores y editores crean documentos (a mano o importando
   PDF/DOCX), los editan como HTML normalizado, los envían a revisión, los publican y los
   archivan, con versionado inmutable y auditoría.
2. **Consumo vía chat RAG.** Los usuarios finales preguntan en lenguaje natural y reciben
   respuestas **fundamentadas únicamente en contenido publicado que su rol y su alcance de
   acceso permiten**, con citas a los documentos, respuesta en *streaming*, caché semántica
   y presupuesto de gasto de IA por usuario.

### Objetivos de diseño

- Seguridad del ciclo documental: del borrador a la publicación, con roles, auditoría y
  control de publicación explícitos.
- Recuperación acotada al **alcance efectivo de acceso** del usuario; el chat público nunca
  recupera contenido no publicado.
- Vendible como producto por cliente con Docker Compose: datos, secretos, logs y
  configuración aislados.
- Señales operativas y de calidad: auditoría de consultas, metadatos de tokens/costo,
  comportamiento de caché, citas y feedback.
- Latencia de respuesta del chat tratada como atributo de calidad de primer orden.
- Control del gasto de IA mediante presupuestos mensuales por usuario, configurables.

### Roles

- **Admin** — autoridad de gestión global, sin restricción de alcance.
- **DocumentEditor** — gestiona borradores/revisión dentro de su alcance de unidad
  organizativa; no publica por sí solo.
- **DocumentPublisher** — además puede publicar dentro de su alcance.
- **Viewer** — sólo chat, documentos publicados permitidos y autoservicio limitado de su
  propia cuenta/saldo.

> El rol único `DocumentManager` del MVP original fue reemplazado por `DocumentEditor` /
> `DocumentPublisher` durante el refactor de acceso jerárquico. En el código sólo sobrevive
> como historia de migraciones.

---

## 2. Arquitectura general

### 2.1 Topología

```
                                  Browser (HTTPS)
                                        │
                          ┌─────────────▼─────────────┐
                          │   Caddy — reverse proxy    │   manage. / chat. / docs.
                          │   TLS, ruteo same-origin   │   security headers
                          └─────────────┬─────────────┘
            ┌───────────────────────────┼───────────────────────────┐
       manage-web                  chat-web / docs-web         (SPAs React estáticas)
            │                           │
   /api/*  →  .NET        /api/chat*, /api/feedback*  →  rag-api (FastAPI, SSE)
                          /api/auth|session|csrf|viewer/*  →  .NET
            │                           │
   ┌────────▼─────────┐  token interno   ┌────────▼─────────┐
   │   dotnet-api      │  + validación    │     rag-api       │
   │   (.NET 8)        │  de sesión por   │    (FastAPI)      │
   │   esquema: app    │◀───request──────▶│    esquema: rag   │
   └────────┬──────────┘                  └────────┬──────────┘
            │                                       │
            └──────────────┬─────────────────────────┘
                           │
        ┌──────────────────▼───────────────────┐   ┌───────────────┐
        │  PostgreSQL 16 + pgvector             │   │  MinIO (S3)   │
        │  esquemas:  app  |  rag               │   │  imágenes     │
        └───────────────────────────────────────┘   └───────────────┘
                           │
                           ▼   OpenAI API — chat + embeddings
```

### 2.2 Dos backends, dos esquemas, sin escritura cruzada

- **`services/dotnet-api` (.NET 8)** posee el esquema **`app`**: identidad, sesión,
  usuarios/roles/grupos, unidades organizativas, ciclo de vida documental, import asistido
  PDF/DOCX, imágenes de documento, handoff de sesión hacia el visor, auditoría de gestión y
  reporting de sólo lectura.
- **`services/rag-api` (FastAPI, Python 3.12)** posee el esquema **`rag`**: chat,
  recuperación, embeddings, caché semántica, auditoría de consultas RAG, pricing de modelos
  y jobs de indexación.
- **Única interacción cruzada:** FastAPI crea vistas de reporting de sólo lectura en `rag` y
  otorga `SELECT` al rol *reader* de .NET; las migraciones EF de .NET otorgan a `rag_owner`
  `SELECT` sobre un par de tablas de `app` (las necesarias para el filtro de acceso en
  recuperación). FastAPI **nunca** escribe `app`; .NET **nunca** escribe `rag`.

### 2.3 Tres frontends de propósito único

`apps/manage-web`, `apps/chat-web` y `apps/docs-web` son SPAs React independientes que
llaman **sólo** a su backend asignado por **rutas same-origin `/api/*`** a través de Caddy,
nunca cross-origin. Componentes compartidos en `packages/shared-ui`
(`@helpcenter/shared-ui`).

- `manage` y `docs` → .NET.
- `chat` → FastAPI para chat/feedback y .NET para auth/sesión/links de visor.

### 2.4 Ruteo en Caddy (verificado en `infra/compose/Caddyfile`)

- `manage.{dominio}` — `/api/*` → `dotnet-api:8080`; resto → `manage-web`.
- `chat.{dominio}` — `/api/chat`, `/api/chat/*`, `/api/feedback`, `/api/feedback/*` →
  `rag-api:8000` con `flush_interval -1` (streaming SSE sin buffering);
  `/api/auth/*`, `/api/session`, `/api/session/*`, `/api/csrf`, `/api/viewer/*` →
  `dotnet-api`; resto → `chat-web`.
- `docs.{dominio}` — `/api/chat`, `/api/feedback*` → `rag-api`; `/api/*` → `dotnet-api`;
  resto → `docs-web`.
- TLS interno (CA propia de Caddy en local) y cabeceras de seguridad: HSTS,
  `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`,
  `X-Frame-Options: DENY`, `Permissions-Policy`.

### 2.5 Comunicación servicio-a-servicio

`.NET → FastAPI` (indexación, invalidación de caché, bytes de imagen para multimodal) viaja
por la red Docker con cabecera `X-Internal-Service-Token` (secreto de Compose), **no expuesta
por Caddy**. En sentido inverso, FastAPI valida la sesión del navegador llamando al endpoint
interno de .NET en cada request (sección 5).

---

## 3. Stack tecnológico y decisiones

| Capa | Elección | Por qué |
| --- | --- | --- |
| API de gestión | **.NET 8** (SDK 8.0.421), ASP.NET Core (controllers), EF Core | Tipado fuerte y EF/migraciones robustas para identidad, ciclo de vida y auditoría transaccional. |
| Servicio RAG | **FastAPI** (Python 3.12), SQLAlchemy 2 async + asyncpg, Alembic | El ecosistema RAG/IA es Python-first; `asyncio` encaja con I/O de proveedores y DB; SQL crudo donde la recuperación lo exige. |
| Base de datos | **PostgreSQL 16 + pgvector** (HNSW), esquemas `app` y `rag` | Una sola DB por cliente; pgvector evita una base vectorial aparte y permite **recuperación híbrida en una sola consulta** (vector + texto). |
| Almacén de objetos | **MinIO** (S3-compatible) | Imágenes de documento fuera de la DB, detrás de URLs autorizadas por la app. |
| Frontends | **React 18 + TypeScript**, Vite, Tailwind CSS, `shadcn/ui`, lucide-react, TanStack Query, react-hook-form + zod, i18next, TipTap | Base UI consistente; estado de servidor con React Query; formularios/validación tipados; editor rico TipTap. |
| Proveedor IA | **OpenAI** vía SDK oficial `openai`, **sólo desde FastAPI** | Centraliza todas las llamadas de IA en un servicio; .NET nunca llama a OpenAI. IDs/dimensiones de modelo son configuración. |
| Reverse proxy | **Caddy 2** (CA interna en local) | TLS automático y ruteo same-origin simple por host. |
| Empaquetado | pnpm workspace (frontends), `dotnet` (.NET), `uv` (Python), Docker Compose | Tres toolchains, un repo; despliegue reproducible por cliente. |

### Modelos de IA (default, configurables por despliegue)

- **Chat:** `gpt-4.1-nano`.
- **Embeddings:** `text-embedding-3-small` a **1024 dimensiones**.

Estos defaults priorizan costo bajo hasta tener el MVP corriendo de punta a punta. Se fijan
por variables de entorno / `tenant_config` (`OPENAI_CHAT_MODEL`, `OPENAI_EMBEDDING_MODEL`,
`OPENAI_EMBEDDING_DIMENSIONS`) y **nunca** se hardcodean en lógica de negocio.

### Versiones clave (verificadas en manifiestos)

- .NET SDK `8.0.421` (`global.json`, `rollForward: latestFeature`).
- Python `>=3.12,<3.13`; FastAPI `0.136.1`, SQLAlchemy `2.0.49`, asyncpg `0.31.0`,
  pgvector `0.4.2`, `openai` `2.36.0`, Alembic `1.18.4`, PyJWT `2.12.1`, structlog
  `25.5.0`, uvicorn `0.46.0`. Dev: pytest `9.0.2`, ruff `0.14.8`, mypy `1.19.0`,
  testcontainers `4.14.2`.

### Reglas de convención

- No se introducen librerías nuevas (HTTP, ORM, logging, validación, testing, SDK de IA) sin
  registrar la decisión. El stack del MVP está fijado.
- Controllers/routers finos; la lógica (ciclo de vida, auth, import, caché, presupuesto,
  auditoría) vive en servicios testeables. DTOs .NET bajo `Models/<Feature>/`; FastAPI usa
  Pydantic para todo lo que cruza un límite.
- Envelope de error compartido `{ "error": { "code", "message", "details", "requestId" } }`
  con catálogo de códigos `UPPER_SNAKE_CASE` estable.
- HTML de documento saneado en servidor (`Ganss.Xss`) y al renderizar (`DOMPurify`); sólo se
  preservan CSS inline `color` y `text-align` más `<mark>`.

---

## 4. Modelo de datos

Una base PostgreSQL por cliente con dos esquemas. pgvector provee el tipo `vector` y los
índices HNSW.

### 4.1 Esquema `app` (.NET / EF Core) — entidades principales

- **Identidad y organización:** `User` (email, `PasswordHash`, `IsActive`,
  `OrganizationalUnitId`, `AccessScopeVersion`), `Role`, `UserRole`, `OrganizationalUnit`
  (árbol con `ParentId`, `IsActive`), `OrganizationalUnitClosure` (`AncestorId`,
  `DescendantId`, `Depth` — tabla de cierre para consultas de ancestro/descendiente),
  `Group` (`OwnerOrganizationalUnitId`, `PublishingPolicy`), `UserGroup`,
  `UserGroupPublishGrant` (permiso explícito de uso de un grupo para publicar).
- **Documentos:** `Document` (`CurrentState`, `CurrentDraftVersionId`,
  `CurrentPublishedVersionId`), `DocumentVersion` (`VersionNumber`, `State`, `Title`,
  `DocumentTypeId`, `ContentHtml`, marcas de revisión/publicación, `IndexingJobId`,
  `IndexingStatus`), `DocumentType` (catálogo ABM admin), `DocumentImage` (`ObjectKey` en
  MinIO, `Sha256Hash`, `AltText`), `ReviewComment`, `ImportMetadata` (origen del import).
- **Acceso a documento:** `DocumentPermission` (regla con `OrganizationalUnitId?` y
  atributos opcionales) y `DocumentPermissionGroup` (grupos de la regla). Una regla compone
  unidad organizativa **Y** grupos (sección 6).
- **Sesión y handoff:** `SessionHandoffCode` (target `chat`/`docs`) y
  `ViewerSessionHandoffCode` (por `DocumentId`, con `Purpose` y `AllowedStateScope`). Ambos
  de un solo uso, almacenados como hash, con expiración.
- **Presupuesto y auditoría:** `UserAiBudgetLimit` (`MonthlyBudgetUsd?`, `IsDisabled`),
  `AuditEvent` (auditoría funcional de gestión), `TenantConfig` (configuración del tenant,
  sección 12).
- `DocumentTag` permanece como tabla **dormida** (los tags no son feature expuesta en el
  MVP).

La unidad raíz **"Empresa"** tiene id fijo `01000000-0000-0000-0000-000000000001`; una regla
de acceso scopeada a esa unidad es la regla "toda la empresa".

### 4.2 Esquema `rag` (FastAPI / Alembic)

- **`document_chunks`** — chunk indexado: `embedding vector(1024)`, `content`,
  `content_tsv` (BM25), `heading_path`, `corpus` (`published` / `preview`), `is_active`,
  `embedding_model`, `document_version_id`. Índices HNSW parciales por corpus y GIN sobre
  `content_tsv`.
- **`document_chunk_images`** — referencias estables `chunk → image_id` para multimodal.
- **`semantic_cache_entries`** y **`semantic_cache_sources`** — caché semántica y sus
  documentos fuente (para invalidación).
- **`query_audit_events`** y **`query_audit_citations`** — auditoría de cada consulta y sus
  citas.
- **`model_pricing`** — pricing versionado por `model_id`/`model_kind` con vigencia.
- Migraciones (15 revisiones Alembic) cubren el esquema inicial, las vistas de reporting, el
  cambio de dimensiones a 1024 (v2), las columnas BM25, los campos de sesión/citas en
  auditoría, las referencias multimodales y los índices HNSW parciales.

### 4.3 Frontera cruzada de sólo lectura

FastAPI crea vistas de reporting en `rag` y otorga `SELECT` al *reader* de .NET (feedback y
analítica de uso desde la app de gestión). A su vez, .NET otorga a `rag_owner` `SELECT` sobre
las tablas de `app` que el filtro de recuperación necesita (`documents`,
`document_permissions`, `document_permission_groups`, `organizational_unit_closure`,
`document_dimension_values`). Ninguno escribe el esquema del otro.

---

## 5. Autenticación y sesión

### 5.1 Login

`AuthService.AuthenticateAsync` busca al usuario activo por email (normalizado en
minúsculas) y verifica la contraseña con `IPasswordHashService.Verify` contra
`User.PasswordHash`. No hay SSO/OIDC en el MVP.

### 5.2 Sesión unificada con JWT RS256

Tras el login, .NET emite un **JWT firmado con RS256** y lo entrega en una cookie
**`__Host-session`** (`HttpOnly`, host-only). Esa cookie es la **única** credencial de sesión
del navegador para los tres frontends.

- **Rotación de claves** (`JwtSigningKeyStore`): se carga una lista de claves RSA desde el
  secreto `jwt_signing_keys.json`, cada una con `kid` y `status`. La marcada `current` firma;
  **todas** las públicas se aceptan para validar. Se rota agregando una nueva `current` y
  degradando la anterior, que sigue validando los tokens vigentes hasta que expiran.
- Las claves públicas se publican como **JWKS** en `/.well-known/jwks.json`
  (`kty=RSA, use=sig, alg=RS256, kid, n, e`).

### 5.3 CSRF (double-submit firmado)

Las requests mutantes se protegen con un token CSRF **double-submit firmado**
(`CsrfTokenService` + `CsrfProtectionMiddleware`, secreto `csrf_signing_key`). El frontend
obtiene el token desde `/api/csrf` y lo reenvía en cada mutación.

### 5.4 Validación cross-service (cada request, sin caché)

FastAPI **no** confía localmente en el JWT para las claims de acceso: en cada request,
`DotnetSessionValidator` hace `GET` a `/internal/session/validate` enviando
`X-Internal-Service-Token` y la cookie `__Host-session`. .NET responde las claims efectivas:
`userId`, `role`, `isGlobalAdmin`, `organizationalUnitId`, `groups`, `accessScopeVersion`,
`accessScopeHash`, `corpus`. **No se cachean** las claims relevantes para acceso, de modo que
cambios de rol/grupo/unidad/estado se reflejan de inmediato (sección 6.3).

### 5.5 Navegación entre apps y visor (handoff de un solo uso)

- **Manage → Chat/Docs:** el sidebar de gestión genera un `SessionHandoffCode` de un solo
  uso (target `chat` o `docs`) y abre la app destino en una pestaña nueva; ésta lo consume,
  limpia la URL y continúa con su propia cookie `__Host-session`.
- **Chat/Docs (links a documentos):** el visor usa **localizadores por `documentId`**, no
  credenciales en la URL. `docs.cliente.com` revalida la sesión autenticada y los permisos
  del documento en el servidor antes de devolver contenido (los links son *locators*, no
  autorización). Cuando hace falta cruzar de chat a docs se usa un `ViewerSessionHandoffCode`
  de un solo uso, acotado por `Purpose` y `AllowedStateScope`. El JWT de sesión nunca viaja
  en una URL.

---

## 6. Modelo de control de acceso

### 6.1 Unidades organizativas jerárquicas + grupos transversales

El acceso combina dos dimensiones:

- **Unidad organizativa** del usuario, dentro de un árbol con tabla de cierre
  (`organizational_unit_closure`) para resolver ancestro/descendiente eficientemente.
- **Grupos transversales**, que pueden ampliar o acotar audiencias de lectura. Cada grupo
  tiene un alcance de propiedad (global o de una unidad) y una `PublishingPolicy`; los
  publishers no-admin sólo pueden usar grupos propios de su alcance o explícitamente
  concedidos (`UserGroupPublishGrant`).

### 6.2 Reglas de acceso de un documento (AND dentro, OR entre)

Un documento tiene una o más **reglas** de acceso. La semántica (idéntica en la política
.NET `DocumentAccessPolicy` y en el SQL de recuperación de FastAPI):

- Un **Admin global** (`isGlobalAdmin`) puentea el filtro de reglas (no el de estado/ciclo
  de vida).
- Un documento es visible si **alguna** regla matchea (**OR entre reglas**).
- Dentro de una regla, la condición de **unidad organizativa Y** la condición de **grupo**
  deben cumplirse (**AND dentro de la regla**); cada condición se satisface trivialmente si
  esa dimensión está ausente en la regla.
- La condición de unidad matchea la regla "toda la empresa" (unidad raíz), o cuando la unidad
  de la regla es **ancestro o descendiente** de la unidad del usuario (join sobre el cierre).
- Una regla **sin** unidad ni grupo es inválida y se ignora defensivamente (espejo del
  rechazo en la capa de aplicación).

### 6.3 `access_scope_hash` v2 y frescura

`AccessScopeHash.ComputeV2` calcula un SHA-256 (hex, minúsculas) sobre un JSON canónico y
ordenado de: `accessScopeVersion`, `groups` (ids ordenados), `isGlobalAdmin`,
`organizationalUnitId`, `role` y `v: 2`. Este hash:

- **Particiona la caché semántica**: una respuesta cacheada sólo se reusa para un
  `access_scope_hash` idéntico (sección 9), evitando fugas entre alcances.
- Cambia cuando cambia cualquier dimensión de acceso. Los cambios de rol, grupos, estado
  activo o unidad **incrementan `users.access_scope_version`**, lo que invalida hashes
  previos sin tener que purgar nada.

---

## 7. Ciclo de vida documental

### 7.1 Estados y versionado

Estados: **`Draft` → `In Review` → `Published` → `Archived`** (con `restore` desde
archivado). Cada **publicación exitosa crea una versión publicada inmutable**
(`DocumentVersion` con `VersionNumber`). `Document` apunta a `CurrentDraftVersionId` y
`CurrentPublishedVersionId`.

### 7.2 Publicación bloqueada por indexación

La publicación está **bloqueada hasta que FastAPI indexa el documento con éxito**: el
contenido no se vuelve público (ni recuperable por el chat) hasta tener chunks/embeddings
listos. La autorización de publicación es de `Admin` o de `DocumentPublisher` dentro de su
alcance.

### 7.3 Import asistido PDF/DOCX

El import devuelve HTML de borrador seguro cuando es posible, dejando el formato final y los
atributos bajo control del usuario:

- **DOCX** (Mammoth) puede preservar estructura semántica común y **extraer imágenes
  embebidas**: se normalizan con SkiaSharp (decodificar → cap de dimensiones → re-encode a
  WebP para formatos no web-safe; se saltan EMF/WMF/TIFF e imágenes diminutas), se guardan en
  MinIO y se referencian con URLs estables `/api/document-images/{id}/content`.
- **PDF** es conservador: no promete reconstrucción visual fiel.

### 7.4 Imágenes y saneamiento

Las imágenes subidas desde el editor se guardan en almacenamiento de objetos privado y se
referencian por URLs autorizadas por la app (nunca URLs de almacenamiento crudas ni base64;
fuentes externas/base64 se rechazan con `DOCUMENT_IMAGE_SOURCE_INVALID`). El HTML se sanea en
servidor (`Ganss.Xss`) y al renderizar (`DOMPurify`).

---

## 8. Flujo RAG completo (chat)

Implementado en `ChatService` (`services/rag-api/.../rag/chat_service.py`). El router expone
el chat como **SSE** y antecede el stream con un `precheck`.

### 8.1 `precheck` (antes de cualquier llamada paga)

1. Pregunta vacía → `VALIDATION_FAILED` (400).
2. Refresco de `tenant_config` si está vencido (TTL 30s, sección 12).
3. Largo de pregunta > `chat_max_question_chars` (default 4000) → `CHAT_QUESTION_TOO_LONG`
   (400).
4. Pricing de modelos configurado (chat + embedding) o `RAG_PROVIDER_MISCONFIGURED`.
5. Presupuesto del usuario no agotado (sección 10) o `AI_BUDGET_EXCEEDED` (429).

Estos errores salen como envelope JSON normal, **no** como evento SSE a mitad de stream.

### 8.2 Selección de corpus

- Mini-chat scopeado a un documento → siempre `published` (preserva el invariante de "chat
  público sólo Published").
- `Viewer` → `published`. Otros roles → el `corpus` de sus claims (permite preview a gestión
  autorizada).

### 8.3 Memoria conversacional (multi-turno)

Si la consulta pertenece a una sesión, se cargan hasta `conversation_history_turns` (default
5) turnos previos del mismo usuario y se **condensa** la pregunta de seguimiento a una
pregunta auto-contenida (`condense_question`) antes de embeber/recuperar/cachear. La pregunta
reescrita se audita (`rewritten_question`).

### 8.4 Embedding + lookup de caché

Se embebe la pregunta (de recuperación) y, salvo en mini-chat scopeado, se intenta un **hit
de caché semántica** (sección 9). Un hit emite la respuesta cacheada y sus citas verbatim y
audita el turno con costo 0.

### 8.5 Recuperación híbrida (`hybrid_retrieve`)

Una sola sentencia SQL con un CTE `allowed_documents` **MATERIALIZED** que aplica **una vez
por documento** el filtro de permiso (sección 6.2), el de ciclo de vida, el de scope de
documento y el de dimensión:

- **Permiso:** admin bypass; OR entre reglas; AND (unidad por cierre o raíz) + grupos.
- **Ciclo de vida:** corpus `published` ⇒ el chunk debe pertenecer a
  `current_published_version_id` y `current_state = 'Published'` (auto-sanación ante
  desactivaciones perdidas); corpus `preview` ⇒ documento no `Archived`.
- **Modelo de embedding:** los chunks se filtran por `embedding_model`, de modo que cambiar
  de modelo particiona la recuperación.

Sobre los documentos permitidos corren dos recuperadores y se fusionan:

- **Vector kNN** con HNSW (`embedding <=> :q_embedding`, distancia coseno). `ef_search`
  elevado (80) y *iterative scan* cuando pgvector ≥ 0.8, para que el post-filtro de acceso no
  pierda recall.
- **Léxico** BM25 (`websearch_to_tsquery` + `ts_rank_cd` sobre `content_tsv`, con `unaccent`
  inmutable) más similitud por trigramas como respaldo.
- **Fusión RRF** (Reciprocal Rank Fusion): `score = Σ 1 / (rrf_k + rank)` con `rrf_k = 60`.

El corpus se inyecta como literal SQL validado por whitelist (`published`/`preview`) para que
el planner pueda usar los **índices HNSW parciales por corpus**. Los candidatos fusionados
(hasta `rag_hybrid_top_k = 30`) pasan a un **reranker cross-encoder** (TEI BGE
`BAAI/bge-reranker-v2-m3`, opcional) que reduce al **top-K final** (`rag_final_top_k = 8`).

### 8.6 Generación con streaming

- Sin chunks → mensaje de "sin información" localizado (es/en).
- Con chunks → se intenta **multimodal query-time** (sección 8.7); haya o no imágenes, la
  generación produce **deltas de tokens** que se reenvían como eventos SSE `answer-token` a
  través de un parser JSON incremental compartido. La respuesta incluye las citas (los
  `chunk_id` que el modelo efectivamente citó).
- Defaults de generación: `temperature = 0.1`, `max_tokens = 900`.

### 8.7 Multimodal query-time (imágenes)

Cuando hay imágenes asociadas a los chunks recuperados, se seleccionan **hasta 3** (cap de
**5 MB** totales, `detail = low`), se obtienen sus **bytes autorizados** a través del endpoint
interno de .NET (token de servicio) y se adjuntan a la generación. Nunca se exponen URLs de
almacenamiento ni se envían todas las imágenes del documento. Las respuestas multimodales
**no** se escriben en caché.

### 8.8 Auditoría y costo

Tras generar, se inserta una fila completa en `query_audit_events` (pregunta y reescritura,
respuesta, `cache_hit`, tokens input/cached/output, `pricing_snapshot_id`,
`estimated_cost_usd`, `latency_ms`, `access_scope_hash`, `corpus`, `session_id`,
`previous_event_id`, filtros, métricas de reranker, `scope_document_id` y los cinco campos
`multimodal_*`) más las `query_audit_citations`. El **costo estimado** =
`tokens_embedding × precio_input_embedding + tokens_input × precio_input_chat +
tokens_output × precio_output_chat` (con pricing versionado vigente).

Se **escribe en caché** sólo si hay citas, no es mini-chat scopeado y no fue multimodal.
Finalmente se emiten los eventos SSE `citations` y `usage`; el router los envuelve con
`request-id` / `done` / `error`.

---

## 9. Caché semántica

`semantic_cache_entries` particiona por **`(corpus, access_scope_hash, filters_hash,
embedding_model)`**. El lookup es un **vecino más cercano HNSW** sobre `question_embedding`;
se acepta el hit si la **similitud coseno** (`1 - distancia`) ≥ **umbral 0.90**. Las entradas
expiran a `cached_at + TTL` (**24 h**). Se guardan las **citas verbatim** (jsonb) y los
documentos fuente (`semantic_cache_sources`).

- **Invalidación por documento:** `.NET` notifica a FastAPI (`/internal/cache-invalidations`)
  tras archivar, republicar, restaurar o editar tras publicar; `invalidate_sources` borra las
  entradas de caché cuyas fuentes incluyen esos `document_id`.
- **Aislamiento por alcance:** una entrada sólo se reusa para un `access_scope_hash` idéntico;
  las entradas v1 nunca matchean alcances v2 porque el hash difiere.
- **Sin caché para multimodal** ni para mini-chat scopeado a documento.
- Usuarios sin presupuesto **no** reciben respuestas cacheadas: el lookup requiere un embedding
  (pago), que el `precheck` ya bloqueó.

El lookup degrada con gracia: si falla, se sigue por el camino RAG completo (la corrección
nunca depende de un hit de caché).

---

## 10. Presupuestos y costos de IA

- `user_ai_budget_limits` define el presupuesto mensual por usuario (`MonthlyBudgetUsd`
  nulo ⇒ usa el default `default_monthly_ai_budget_usd` = **USD 5**); `IsDisabled` exime al
  usuario del límite.
- Los meses se calculan en la **zona horaria del cliente** (`customer_timezone`); el gasto del
  período es la suma de `estimated_cost_usd` en `query_audit_events` dentro de `[inicio, fin)`.
- Si el gasto ≥ presupuesto ⇒ `AI_BUDGET_EXCEEDED` (429). El agotamiento bloquea nuevo uso de
  IA pago, **no** la visualización de documentos ni el acceso de gestión.
- `model_pricing` mantiene precios versionados por modelo y tipo (`chat` / `embedding`) con
  vigencia; cada consulta toma un *snapshot* de pricing.

---

## 11. Auditoría, reporting y observabilidad

- **Auditoría funcional** de gestión en `app.AuditEvent` (acciones administrativas) y
  **auditoría de consultas** en `rag.query_audit_events` (+ citas), no sólo en logs.
- **Feedback** de respuestas (pulgar arriba/abajo + comentario opcional) ligado a la fila de
  auditoría de la consulta; revisión de feedback en la app de gestión (filtros por feedback
  negativo, documento citado, usuario y rango de fechas), incluyendo nombre/email del revisor.
- **Reporting** de sólo lectura sobre auditoría RAG vía vistas en `rag` con `SELECT` otorgado
  al *reader* de .NET.
- **Logs técnicos** estructurados (JSON) por servicio, con retención configurable; Caddy
  registra accesos en JSON.
- **Observabilidad opcional** vía `infra/compose/compose.observability.yaml` (OpenTelemetry),
  activable por despliegue.

---

## 12. Configuración operativa (`tenant_config`)

`app.tenant_config` es un singleton que centraliza la configuración del tenant (branding,
modelos, knobs de RAG, presupuestos, S3, zona horaria, límites de import y de chat).

- **Sembrado una sola vez desde entorno** (`TenantConfigEnvSeeder`, guardado por
  `SeededFromEnv`) para no cambiar el modelo silenciosamente en un upgrade.
- **Subconjunto editable por Admin** vía `GET`/`PUT /api/configuration`: `customer_timezone`,
  `import_max_file_size_mb`, `chat_max_question_chars` y el presupuesto mensual default. .NET
  lo usa para el límite de tamaño de import y el presupuesto inicial de usuarios nuevos.
- **FastAPI** lee `tenant_config` (con grant `SELECT` a `rag_owner`) y **refresca su `Settings`
  en memoria** con un TTL de 30s en `ChatService.precheck` (`TenantConfigRefresher`), aplicando
  así el largo máximo de pregunta en caliente.
- **Proveedor y embedding quedan fijos por entorno** (read-only) para evitar un cambio de
  modelo accidental.

---

## 13. Despliegue y operaciones

### 13.1 Docker Compose (single-tenant)

Servicios (`infra/compose/compose.yaml`): `postgres` (pgvector/pgvector:pg16),
`postgres-init` (crea DB y roles `app_owner`/`rag_owner`/`app_reporting_reader`), `minio` +
`minio-init` (bucket de imágenes), `dotnet-api`, `rag-api`, `manage-web`/`chat-web`/`docs-web`
(nginx estáticos) y `caddy` (puertos 80/443). Healthchecks en todos; los frontends y APIs
deben estar *healthy* antes de levantar Caddy.

- **Secretos por archivo** (Docker secrets): contraseñas de Postgres (admin/app/rag/reporting),
  `openai_api_key`, `jwt_signing_keys.json`, `csrf_signing_key`, `internal_service_token` y
  credenciales de MinIO/S3. Nunca se commitean secretos reales.
- **Migraciones automáticas al iniciar**: EF Core para `app`, Alembic para `rag`.
- `.NET` publica JWKS en `/.well-known/jwks.json`; `rag-api` recibe `DOTNET_JWKS_URL`,
  `DOTNET_SESSION_VALIDATE_URL` y `SESSION_COOKIE_NAME=__Host-session`.

### 13.2 Local

```powershell
.\infra\compose\Start-Local.ps1 -TrustCaddyCertificate   # levanta el stack y confía la CA de Caddy
.\infra\compose\Seed-LocalDemoData.ps1                    # siembra jerarquía/usuarios/documentos demo
```

Apps en `https://manage.localhost`, `https://chat.localhost`, `https://docs.localhost`.
Administrador sembrado inicial: `admin@admin.com` / `admin` (cambiar de inmediato fuera de
pruebas descartables).

### 13.3 Demo (Raspberry Pi + Cloudflare Tunnel)

El stack corre en un host ARM64 (Ubuntu Server) y se expone por Cloudflare Tunnel con
hostnames públicos `manage.`/`chat.`/`docs.<dominio>`. Helpers de host (camino fuente
temporal hasta tener GHCR/CD): `updateService.sh` (update seguro con backup, pull
fast-forward, validación y recreación) e `installServiceAutostart.sh` (unit systemd
`advanced-rag.service` para recuperación tras reboot). Detalle en `docs/operations/`.

---

## 14. Seguridad e invariantes

Invariantes que el sistema sostiene (verificados en código):

1. El chat público recupera **sólo** contenido `Published`.
2. `docs.cliente.com` revalida el acceso al documento en servidor; **los links son
   localizadores, no autorización**.
3. El JWT de sesión **nunca** viaja en una URL.
4. Una respuesta cacheada se reusa **sólo** con `access_scope_hash` coincidente.
5. La auditoría persiste en PostgreSQL, no sólo en logs.
6. Los dos backends nunca cruzan escritura de esquema; el único cruce es lectura
   (vistas/grants).
7. Las llamadas de IA viven sólo en FastAPI; .NET nunca llama a OpenAI.
8. Toda llamada interna `.NET ↔ FastAPI` exige `X-Internal-Service-Token` y no pasa por Caddy.

Defensa en profundidad adicional: HTML saneado en servidor y cliente; CSRF double-submit;
cookies `__Host-`; cabeceras de seguridad en Caddy; rotación de claves de firma.

---

## 15. Testing y calidad

- **Frontends:** Vitest (unitarios/componentes) y Playwright (E2E contra el stack Compose por
  Caddy). `pnpm lint` / `typecheck` / `test` / `build` a nivel workspace.
- **.NET:** xUnit con **Testcontainers** (Docker requerido) para integración de EF/endpoints.
- **FastAPI:** pytest (incl. Testcontainers Postgres), `ruff` y `mypy`.
- **Matriz funcional por rol:** `pruebas.md` es la fuente de aceptación de comportamiento por
  rol.

---

## 16. Estado del proyecto y pendientes

**Estado:** MVP sustancialmente completo sobre la rama `mvp-implementation`. Implementado y
verificado por agente: ciclo de vida documental, import PDF/DOCX (con extracción de imágenes
DOCX), refactor de acceso jerárquico (unidades + grupos + roles `DocumentEditor`/
`DocumentPublisher`), recuperación híbrida con filtro branch-aware y reranker, streaming SSE
real, multimodal query-time, caché semántica escalada, mini-chat scopeado a documento,
catálogo de tipos de documento, configuración operativa editable y reporting de feedback.

**Pendiente (mayormente verificación user-owned):**

- Aceptación en navegador/Compose de los slices "agent-verified" (incluido el *reset*
  destructivo local + matriz de aceptación del refactor de acceso jerárquico).
- Diseño del contenido demo: taxonomía final de unidades/grupos/tipos, set de documentos y
  conjunto de *golden questions* para validar el chat.
- Camino de release: imágenes en GHCR y CD que reemplacen el update por fuente del host demo.

---

*Última actualización: 2026-06-16. La fuente de verdad es el código; ante cualquier
divergencia, vale el código y este documento debe corregirse.*
