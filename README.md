# Dulce Atardecer API

Backend del sistema de punto de venta de **Dulce Atardecer**. Lo usan los empleados desde sus celulares para operar el negocio: caja, cocina y administración.

Módulos actuales:

- **Usuarios, autenticación y sesiones**: inicio de sesión con JWT y refresh token rotativo, gestión de usuarios por el administrador, roles fijos (`Admin`, `Cashier`, `Kitchen`), revocación inmediata de sesiones y auditoría de eventos de seguridad.
- **Categorías y subcategorías**: clasificación de productos en dos niveles. El administrador las crea, edita, activa y desactiva; el punto de venta consulta el árbol de lo disponible en `GET /api/v1/categories`.
- **Extras**: catálogo global de agregados con precio (incluye IVA; puede ser 0). El administrador los gestiona; el punto de venta consulta los activos en `GET /api/v1/extras`.

Próximos módulos: productos, ventas y cocina.

## Stack

- .NET 10 / ASP.NET Core Web API con controllers
- Entity Framework Core 10 + PostgreSQL 16 (Npgsql)
- ASP.NET Core Identity + JWT (HS256)
- Mapster, Asp.Versioning (`/api/v1/...`), OpenAPI

## Estructura

```
Constants/      Nombres de roles, claims, políticas y acciones de auditoría
Controllers/    AuthController, UsersController, CategoriesController, ExtrasController
Data/           ApplicationDbContext, interceptor de auditoría, DataSeeder
Mapping/        Configuración de Mapster
Middleware/     Manejo global de errores y filtros
Migrations/     Migraciones de EF Core
Models/         Entidades, DTOs y ApiResponse
Repository/     Repositorios (los controllers nunca usan el DbContext directo)
Services/       Caché de sesiones revocadas, limpieza, comando reset-admin
```

## Formato de respuesta

Todas las respuestas, incluidas las de error, tienen la misma forma:

```json
{
  "success": false,
  "statusCode": 400,
  "message": "Hay errores de validación",
  "data": null,
  "errors": ["El usuario es obligatorio", "La contraseña es obligatoria"]
}
```

- `statusCode` siempre es igual al código HTTP de la respuesta.
- `errors` siempre es un arreglo: vacío si todo salió bien y, si hubo error, con un mensaje por cada falla.

## Base de datos (PostgreSQL)

La API necesita PostgreSQL 16 con un usuario y una base propios. Elige la opción que te corresponda.

### Opción A: ya tienes PostgreSQL

Crea un usuario y una base solo para este proyecto, sin permisos sobre tus otras bases. Conéctate con un superusuario (si PostgreSQL corre en Docker: `docker exec -it <contenedor> psql -U <superusuario> -d postgres`) y ejecuta:

```sql
CREATE ROLE dulceatardecer LOGIN PASSWORD 'tu_password';
CREATE DATABASE dulceatardecer OWNER dulceatardecer;
REVOKE ALL ON DATABASE dulceatardecer FROM PUBLIC;
```

### Opción B: no tienes PostgreSQL

Levántalo con Docker. La red `dulceatardecer_db` permite que la API en Docker lo encuentre después:

```bash
docker network create dulceatardecer_db
docker run -d --name dulceatardecer-postgres --network dulceatardecer_db --restart unless-stopped \
  -e POSTGRES_USER=dulceatardecer -e POSTGRES_PASSWORD=tu_password -e POSTGRES_DB=dulceatardecer \
  -p 127.0.0.1:5432:5432 -v dulceatardecer_data:/var/lib/postgresql/data postgres:16
```

Queda en `localhost:5432` con el usuario y la base `dulceatardecer`.

## Desarrollo local

### Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- PostgreSQL 16 (ver [Base de datos](#base-de-datos-postgresql))
- La herramienta de EF Core: `dotnet tool install --global dotnet-ef`

### Pasos

1. **Clona el repo y crea tu `.env`:**

   ```bash
   git clone https://github.com/Jerrys212/DulceAtardecer.git
   cd DulceAtardecer
   cp .env.example .env
   ```

   En `.env` completa:
   - `ConnectionStrings__DefaultConnection`: el host, puerto, usuario y contraseña de tu PostgreSQL.
   - `ApiSettings__SecretKey`: una clave aleatoria de al menos 32 bytes, por ejemplo `openssl rand -base64 48`.
   - `InitialAdmin__Password`: al menos 10 caracteres.

2. **Aplica las migraciones.** Esto crea las tablas, los roles y el administrador inicial:

   ```bash
   dotnet ef database update
   ```

3. **Corre la API:**

   ```bash
   dotnet run --launch-profile https
   ```

   La documentación interactiva queda en `https://localhost:7201/swagger`. Para probar endpoints protegidos, haz login, copia el `accessToken` y pégalo en **Authorize**.

4. **Entra con el administrador inicial** (`InitialAdmin__Username` / `InitialAdmin__Password`). El sistema te pedirá cambiar la contraseña antes de permitir cualquier otra acción.

Para crear una migración después de cambiar el modelo:

```bash
dotnet ef migrations add NombreDeLaMigracion
```

## Autenticación (para el frontend)

| Paso | Endpoint | Notas |
| --- | --- | --- |
| Login | `POST /api/v1/auth/login` | Devuelve `accessToken` (10 min) y deja el refresh token en una cookie `HttpOnly`. |
| Peticiones | Header `Authorization: Bearer <accessToken>` | |
| Renovar | `POST /api/v1/auth/refresh` | Requiere el header `X-Requested-With` y `credentials: "include"`. Rota la cookie. |
| Mi usuario | `GET /api/v1/auth/me` | Incluye `mustChangePassword`. |
| Cambiar contraseña | `POST /api/v1/auth/change-password` | Obligatorio si `mustChangePassword` es `true`. Después llama a refresh. |
| Logout | `POST /api/v1/auth/logout` | Revoca la sesión y borra la cookie. |

- Si una petición devuelve `401` con `errors: ["El token de acceso expiró"]`, llama a refresh y reintenta.
- La cookie es `Secure` y `SameSite=Strict`, así que en producción el frontend y la API deben servirse por HTTPS y desde el mismo sitio.

Los endpoints de usuarios (`/api/v1/users`) son solo para el rol `Admin`.

## Despliegue en un VPS con Docker

El `docker-compose.yml` no incluye PostgreSQL: la API se conecta a un contenedor de PostgreSQL que ya exista, uniéndose a su red de Docker.

| Servicio | Qué hace |
| --- | --- |
| `api` | La API. Escucha en `127.0.0.1:8080` para quedar detrás de un reverse proxy con HTTPS. |
| `migrations` | Aplica migraciones y crea el admin inicial. Solo corre cuando lo pides (perfil `tools`). |

### Pasos

1. **Prepara la base de datos** con la [opción A o B](#base-de-datos-postgresql).

2. **Averigua el nombre y la red de tu contenedor de PostgreSQL** (si usaste la opción B son `dulceatardecer-postgres` y `dulceatardecer_db`, y puedes saltarte este paso):

   ```bash
   docker ps --format '{{.Names}}\t{{.Image}}'
   docker inspect <contenedor> -f '{{range $k, $v := .NetworkSettings.Networks}}{{$k}} {{end}}'
   ```

3. **Clona el repo y crea el `.env`** a partir de `.env.example`. En el VPS solo se usan:
   - `DB_HOST`: el nombre del contenedor de PostgreSQL.
   - `DB_NETWORK`: su red de Docker.
   - `DB_NAME`, `POSTGRES_USER` y `POSTGRES_PASSWORD`: la base, el usuario y la contraseña que creaste.
   - `ApiSettings__SecretKey`
   - `InitialAdmin__Username` e `InitialAdmin__Password`
   - `CORS_ALLOWED_ORIGIN`: la URL del frontend.
   - `OPENAPI_ENABLED` (opcional): Swagger queda en `https://api.tu-dominio.com/swagger` para los devs; ponlo en `false` para apagarlo.

   Evita el carácter `$` en las contraseñas, porque Compose lo interpreta como variable.

4. **Construye las imágenes y aplica las migraciones.** Repite la migración cada vez que despliegues una versión con migraciones nuevas:

   ```bash
   docker compose --profile tools build
   docker compose run --rm migrations
   ```

5. **Levanta la API:**

   ```bash
   docker compose up -d api
   ```

6. **Pon un reverse proxy con HTTPS delante del puerto 8080.** Es obligatorio, porque la cookie de refresh solo viaja por HTTPS. Ejemplo con [Caddy](https://caddyserver.com), que obtiene el certificado automáticamente:

   ```
   api.tu-dominio.com {
       reverse_proxy 127.0.0.1:8080
   }
   ```

   La API ya confía en los headers `X-Forwarded-*` del proxy. Por eso la auditoría y el límite de intentos de login usan la IP real del cliente.

### Actualizar a una nueva versión

Un merge a `main` despliega solo (ver [CI/CD](#cicd)). Para actualizar a mano desde el servidor:

```bash
git pull
docker compose --profile tools build
docker compose run --rm migrations
docker compose up -d api
```

### Recuperar el acceso del administrador

No hay recuperación por correo. Desde el servidor:

```bash
docker compose exec api dotnet DulceAtardecer.dll reset-admin --username admin
```

El comando imprime una contraseña temporal, cierra todas las sesiones de ese administrador y lo obliga a cambiar la contraseña al entrar. En local el equivalente es `dotnet run -- reset-admin --username admin`.

### Respaldos

Para generar un respaldo en el formato comprimido de `pg_dump` (`<contenedor>` es tu contenedor de PostgreSQL):

```bash
docker exec <contenedor> pg_dump -U dulceatardecer -d dulceatardecer -Fc > backup.dump
```

Para restaurarlo (reemplaza los objetos que ya existan en la base):

```bash
docker exec -i <contenedor> pg_restore -U dulceatardecer -d dulceatardecer --clean --if-exists < backup.dump
```

## CI/CD

El workflow [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) corre en GitHub Actions.

| Evento | `ci` | `deploy` |
| --- | --- | --- |
| PR hacia `dev` o `main` | ✅ | — |
| Push a `dev` | ✅ | — |
| Push a `main` (merge de un PR) | ✅ | ✅ si `ci` pasa |
| Manual (`workflow_dispatch`) | ✅ | ✅ solo si se lanza sobre `main` |

**`ci` (Compilar y verificar):**

1. `dotnet restore` y `dotnet build -c Release`.
2. `dotnet ef migrations has-pending-model-changes`: falla si cambiaste el modelo y olvidaste crear la migración.
3. Construye las imágenes Docker `runtime` y `migrations`, para detectar un `Dockerfile` roto antes de desplegar.

Si haces un push nuevo a un PR, se cancela la ejecución anterior de ese PR. Los despliegues nunca se cancelan y corren de uno en uno.

**`deploy` (Desplegar en el VPS):** abre una conexión SSH al servidor sin enviar ningún comando. Lo que se ejecuta lo decide el propio servidor con un comando forzado en `authorized_keys`. Así, aunque alguien obtuviera la llave, solo podría lanzar el despliegue.

### Configurar el despliegue

1. **Genera una llave solo para el despliegue** (sin passphrase):

   ```bash
   ssh-keygen -t ed25519 -N "" -C "github-actions-deploy" -f deploy_key
   ```

2. **Autoriza la llave pública en el VPS con un comando forzado.** Agrega una línea al `~/.ssh/authorized_keys` del usuario de despliegue:

   ```
   command="/ruta/a/deploy.sh",restrict ssh-ed25519 AAAA... github-actions-deploy
   ```

   El script hace lo mismo que la [actualización manual](#actualizar-a-una-nueva-versión): `git pull`, construir las imágenes, aplicar las migraciones y levantar la API.

3. **Crea los secrets del repositorio** (Settings → Secrets and variables → Actions):

   | Secret | Valor |
   | --- | --- |
   | `VPS_HOST` | La IP o el dominio del VPS. |
   | `VPS_USER` | El usuario de despliegue del paso 2. |
   | `VPS_SSH_KEY` | La llave privada completa (`deploy_key`). |
   | `VPS_KNOWN_HOSTS` | La salida de `ssh-keyscan <ip-del-vps>`, para que Actions verifique la identidad del servidor. |

4. Borra la llave privada local después de guardarla en el secret.

## Ramas y commits

- `main`: producción
- `dev`: desarrollo
- `feat/*`, `fix/*`, `hotfix/*`: trabajo en curso, con PR hacia `dev`

Los commits siguen Conventional Commits en inglés (`feat:`, `fix:`, `chore:`…).
