# CLAUDE.md

Este archivo guía a Claude Code cuando trabaja en este repositorio. Léelo por completo antes de proponer cambios. Está pensado para que, con una instrucción breve ("creá el módulo de Órdenes"), Claude pueda generar un módulo completo replicando los patrones ya establecidos en el proyecto, sin tener que redescubrirlos cada vez.

## 1. Proyecto

> ✏️ Completa con 3-5 líneas reales del proyecto.

- **Nombre**: `<NombreProyecto>` (`<NombreProyecto>.csproj`)
- **Tipo**: ASP.NET Core 10 Web API — `<breve descripción del dominio, ej: backend de e-commerce con Productos, Categorías, Usuarios>`
- **Solución**: `<un único .csproj en la raíz / solución multi-proyecto (Api, Application, Infrastructure)>`
- **Tests**: `<no hay proyecto de tests todavía / xUnit en /tests>`

## 2. Stack tecnológico

- **Runtime**: .NET 10 (LTS, soportado hasta 2028)
- **Lenguaje**: C# 14, `Nullable` habilitado
- **Framework web**: ASP.NET Core 10 con **Controllers** (`ControllerBase`), no Minimal APIs
- **ORM**: Entity Framework Core 10 + PostgreSQL vía Npgsql (`ApplicationDbContext`)
- **Mapeo DTO ↔ Entidad**: **Mapster** (no AutoMapper), configuración centralizada
- **Auth**: ASP.NET Core Identity + JWT (HMAC-SHA256)
- **Versionado de API**: `Asp.Versioning.Mvc`, versionado por segmento de URL
- **Docs**: Swagger / OpenAPI 3.1 nativo (`AddOpenApi()`), con docs separados por versión si aplica
- **DB local**: PostgreSQL vía Docker Compose (`postgres:16`)

> ✏️ Si el proyecto usa otra combinación (FluentValidation en vez de Data Annotations, Dapper en vez de EF Core, etc.) ajustalo acá — esta sección es la fuente de verdad del stack.

## 3. Comandos esenciales

Requiere el SDK/runtime de .NET 10 (el proyecto apunta a `net10.0`; si solo tenés un SDK más nuevo instalado, necesitás igual los runtime packs 10.x de `Microsoft.AspNetCore.App`/`Microsoft.NETCore.App` o `dotnet run`/`dotnet ef` van a fallar).

```bash
docker compose up -d          # levanta PostgreSQL en localhost:5432
dotnet restore
dotnet ef database update     # aplica migraciones + corre el seeder (requiere `dotnet tool install --global dotnet-ef`)
dotnet run                    # o `dotnet watch run`; Swagger en /swagger
dotnet build
dotnet test                   # si existe proyecto de tests
```

Crear una migración después de cambiar el modelo:

```bash
dotnet ef migrations add <Nombre>
```

**Siempre ejecutá `dotnet build` (y `dotnet test` si existe) antes de dar una tarea por terminada.**

### 3.1 Docker Compose (si no existe)

Si el repo todavía no tiene `docker-compose.yml`, crearlo con un servicio de **PostgreSQL** para desarrollo local, siguiendo esta convención:

- El **nombre del servicio y del contenedor** (`container_name`) deben ser el **nombre de la carpeta raíz del proyecto** (no un genérico como `postgres` o `db`), para que sea identificable si hay varios proyectos corriendo Docker en la misma máquina.
- Imagen `postgres:16`, puerto `5432:5432`, variables `POSTGRES_USER`, `POSTGRES_PASSWORD` y `POSTGRES_DB`.
- La contraseña de PostgreSQL **nunca hardcodeada en texto plano en el `docker-compose.yml` committeado**: usar un archivo `.env` (agregado a `.gitignore`) con `POSTGRES_PASSWORD=...` y referenciarlo con `${POSTGRES_PASSWORD}`.
- Agregar un volumen nombrado para persistir los datos entre reinicios (`<nombre-carpeta>_data:/var/lib/postgresql/data`).

Ejemplo (reemplazando `<nombre-carpeta>` por el nombre real de la carpeta del proyecto):

```yaml
services:
  <nombre-carpeta>:
    image: postgres:16
    container_name: <nombre-carpeta>
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: <nombre-carpeta>
    ports:
      - "5432:5432"
    volumes:
      - <nombre-carpeta>_data:/var/lib/postgresql/data

volumes:
  <nombre-carpeta>_data:
```

## 4. Arquitectura

**Capas**: `Controllers → Repository interfaces (Repository/IRepository/*) → EF Core (ApplicationDbContext)`.
Los repositorios se registran como `Scoped` en `Program.cs`. **Los controllers nunca tocan `DbContext` directamente** — siempre pasan por su interfaz de repositorio inyectada.

No se usa CQRS/MediatR salvo que el proyecto ya lo tenga adoptado — si se pide agregar esa capa, señalarlo explícitamente en la respuesta antes de introducirla, porque cambia el patrón para todo el proyecto.

## 5. Mapeo de DTOs (Mapster)

- Todas las conversiones tipo↔tipo se centralizan en `Mapping/MapsterConfig.cs`, dentro de un único método `RegisterMappings()` llamado una vez al arrancar desde `Program.cs`.
- Los controllers llaman `.Adapt<T>()` directamente sobre la entidad/DTO — no crear clases `Profile` por feature.
- Al agregar un módulo nuevo, sumar sus mapeos (`Entity ↔ CreateDto`, `Entity ↔ UpdateDto`, `Entity ↔ ReadDto`) en ese mismo archivo, no en archivos sueltos.

## 6. Versionado de API

- `Asp.Versioning` con versionado por segmento de URL: `api/v{version:apiVersion}/[controller]`.
- Controllers con lógica que cambia entre versiones viven en `Controllers/V1/` y `Controllers/V2/` (ej. un endpoint que en V2 agrega ordenamiento/paginación que en V1 no existía). Marcar la versión vieja con `[Obsolete]` cuando corresponda, no borrarla.
- Controllers cuya lógica es idéntica en todas las versiones se marcan `[ApiVersionNeutral]` y viven en `Controllers/` a secas — no dupliques un controller entre V1/V2 si no hay diferencia real de comportamiento.
- Swagger se configura con un doc separado por versión (`v1`, `v2`, ...).

## 7. Autenticación y autorización

- Modelo de usuario: `ApplicationUser : IdentityUser` (no crear un segundo modelo `User` en paralelo — si el dominio necesita datos extra de usuario, extender `ApplicationUser`, no duplicar la tabla).
- Login/registro vía `UserManager`/`RoleManager` de Identity; el repositorio de usuarios (`UserRepository`) es el único lugar que emite JWT.
- JWT: HMAC-SHA256, claims mínimos `id`, `username`, rol(es); expiración configurable (`ApiSettings:SecretKey` u equivalente).
- Autorización a **nivel de controller** con `[Authorize(Roles = "Admin")]` (o el rol que corresponda), con `[AllowAnonymous]` explícito en las acciones puntuales que deben ser públicas (login, registro, lecturas públicas). No dejar autorización "por accidente" sin declarar.
- **Secretos**: `ApiSettings:SecretKey`, la cadena de conexión y cualquier credencial van por `dotnet user-secrets` en local y variables de entorno/Key Vault en el resto de entornos — **nunca committeados en texto plano en `appsettings.json`**, ni siquiera para desarrollo rápido.

## 8. Seeding

- `Data/DataSeeder.cs` se conecta vía el hook `UseSeeding` de EF Core, cableado en `Program.cs` — corre con `dotnet ef database update` (y en paths tipo `EnsureCreated()`), **no en cada arranque de la app**.
- Responsabilidad típica del seeder: roles base (`Admin`, `User`), un usuario admin y uno estándar de prueba, y un puñado de datos de ejemplo por entidad principal.
- Al agregar un módulo nuevo, **preguntar antes de agregarle datos de seed** — no todos los módulos necesitan datos de ejemplo, y el seeder no debe crecer sin control.

## 9. Manejo de archivos (uploads)

Patrón para entidades que aceptan una imagen u otro archivo:

- El DTO de creación/edición se bindea con `[FromForm]` (multipart) y expone un `IFormFile?` opcional.
- El controller guarda el archivo en `wwwroot/<Entidad>Images/<id><guid><ext>` y persiste dos campos en la entidad: la URL pública (`ImgUrl`) y la ruta local en disco (`ImgUrlLocal`).
- Si no se sube archivo, `ImgUrl` cae a una URL de placeholder (ej. `placehold.co`) — nunca dejar el campo nulo o vacío en la respuesta.

## 10. Caching y CORS

- Perfiles de cache con nombre en `Constants/CacheProfiles.cs` (ej. `Default10`, `Default20`, en segundos), aplicados con `[ResponseCache(CacheProfileName = "...")]` en endpoints de lectura que lo justifiquen (listados poco volátiles).
- Política de CORS nombrada en `Constants/PolicyNames.cs` (ej. `AllowSpecificOrigin`). **Para proyectos nuevos, configurar orígenes explícitos desde el inicio** (no `WithOrigins("*")` + cualquier método/header) — el wildcard solo se acepta temporalmente en desarrollo local y debe quedar señalado como deuda técnica si aparece.

## 11. Convenciones de código C#

- `Nullable` habilitado; evitar `!` (null-forgiving) salvo casos justificados y comentados.
- `file-scoped namespaces` y `primary constructors` para servicios/repositorios con inyección de dependencias.
- `records` para DTOs inmutables; `class` para entidades con identidad y ciclo de vida.
- Async/await en todo el pipeline de I/O, sufijo `Async` obligatorio, nunca `.Result`/`.Wait()`.
- `CancellationToken` propagado desde la acción del controller hasta las llamadas a EF Core.
- Nombrado: `PascalCase` para tipos/miembros públicos, `_camelCase` para campos privados, `camelCase` para locales/parámetros.
- No usar `var` cuando el tipo no es evidente por el lado derecho.

## 12. Testing

> ✏️ Ajustar según el estado real del proyecto.

- Si no hay proyecto de tests todavía y se agrega un módulo con lógica no trivial, proponer (no imponer) crear `/tests/<Proyecto>.UnitTests` con xUnit + FluentAssertions antes de escalar el proyecto.
- Si ya existe: unit tests para repositorios/lógica de negocio con mocks, integration tests contra PostgreSQL real (Testcontainers) para flujos completos de un endpoint.

## 13. Checklist: crear un módulo nuevo

Cuando se pida "creá el módulo de `<Entidad>`", el flujo esperado es:

1. **Modelo**: `Models/<Entidad>.cs` (entidad EF Core) + configuración Fluent API si hace falta (índices, tipos de columna, ej. `decimal(18,2)` para dinero).
2. **DTOs**: `Create<Entidad>Dto`, `Update<Entidad>Dto`, `<Entidad>Dto` (lectura) en `Models/Dtos/`.
3. **Mapeos**: agregar las conversiones correspondientes en `Mapping/MapsterConfig.cs`.
4. **Repositorio**: interfaz `I<Entidad>Repository` en `Repository/IRepository/` + implementación en `Repository/`, registrada `Scoped` en `Program.cs`.
5. **Migración**: `dotnet ef migrations add Add<Entidad>` y revisar el `.cs` generado antes de aplicarlo.
6. **Controller**: decidir si es `[ApiVersionNeutral]` o si necesita `V1`/`V2` según si ya se prevén cambios de contrato; aplicar `[Authorize(Roles = "...")]` a nivel controller con `[AllowAnonymous]` en las acciones públicas que correspondan.
7. **Cache/CORS**: aplicar `[ResponseCache]` en los `GET` que lo justifiquen, usando los perfiles existentes.
8. **Uploads**: si la entidad maneja imágenes/archivos, seguir el patrón de la sección 9.
9. **Seed**: preguntar antes de sumar datos de ejemplo al `DataSeeder`.
10. **Build/test**: correr `dotnet build` (y `dotnet test` si aplica) y confirmar que Swagger documenta bien el nuevo controller (`[ProducesResponseType]` en cada acción).

## 14. Qué NO debe hacer Claude

- No committear secretos (connection strings, JWT keys) en `appsettings.json` — siempre `user-secrets`/variables de entorno.
- No aplicar migraciones automáticamente contra una base de datos que no sea la local de desarrollo.
- No crear un segundo modelo de usuario/entidad en paralelo al existente "para no tocar lo viejo" — si hay que migrar algo, migrarlo, no duplicarlo.
- No introducir un patrón arquitectónico nuevo (CQRS, MediatR, Clean Architecture en capas separadas) sin señalarlo explícitamente y confirmar que es lo que se quiere.
- No dejar CORS en wildcard ni autorización sin declarar "por velocidad" en código que no es explícitamente un prototipo descartable.
- No reescribir archivos completos cuando alcanza con un diff quirúrgico.

## 15. Git

Ramas
main: rama de producción
dev: rama de desarrollo
feat/_: funcionalidades nuevas
fix/_: corrección de errores
hotfix/\*: correcciones críticas en producción

Commits
Los mensajes de commit se escriben en inglés
Formato de conventional commits (feat:, fix:, chore:, refactor:, docs:, test:)
Describe el "por qué" del cambio, no solo el "qué"

---

> 💡 **Cómo adaptar esta plantilla a un proyecto nuevo**: reemplazá los placeholders de la sección 1, borrá/ajustá las secciones 5-10 según las librerías reales que uses (si no usás Mapster, versionado, o uploads, sacá esas secciones), y completá la sección 13 con el nombre real de tus entidades de ejemplo. Una vez pegado en el repo nuevo, con algo como "creá el módulo de Órdenes" alcanza para que se repliquen todos los patrones documentados acá.
