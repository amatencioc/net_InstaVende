# Guía de instalación de InstaVende en una PC nueva (Windows)

Pasos desde cero hasta tener la aplicación funcionando con datos de prueba. Tiempo estimado: 20–30 minutos
(la mayor parte es descargar e instalar programas).

---

## 1. Instalar los programas necesarios

Instálalos en este orden, con las opciones por defecto. Al terminar, **reinicia la PC** (o al menos cierra y
vuelve a abrir todas las ventanas de terminal) para que Windows reconozca los programas nuevos.

| # | Programa | Para qué | Descarga |
|---|---|---|---|
| 1 | **Git** | Descargar el código | https://git-scm.com/download/win |
| 2 | **.NET SDK 10** | Compilar y ejecutar la API | https://dotnet.microsoft.com/download/dotnet/10.0 (elige "SDK x64") |
| 3 | **Node.js 22 LTS** | Panel web y servicio de WhatsApp | https://nodejs.org (botón "LTS") |
| 4 | **SQL Server** | Base de datos | Ver opciones abajo |
| 5 | **Google Chrome** (o Edge, que ya viene con Windows) | Conexión de WhatsApp por QR | https://www.google.com/chrome |
| 6 | *(Opcional)* **Visual Studio Code** | Editar archivos de configuración | https://code.visualstudio.com |
| 7 | *(Opcional)* **SQL Server Management Studio** | Ver la base de datos | https://aka.ms/ssms |

### Opciones de SQL Server (elige una)

- **A. LocalDB (la más simple, recomendada para desarrollo).** Viene incluida si instalas Visual Studio con la
  carga de trabajo "ASP.NET y desarrollo web". También se instala sola con el instalador de SQL Server Express:
  https://www.microsoft.com/sql-server/sql-server-downloads → *Express* → *Descargar medios* → *LocalDB*.
- **B. SQL Server Express o Developer** (gratuitos): https://www.microsoft.com/sql-server/sql-server-downloads.
  Durante la instalación anota el nombre de la instancia (por ejemplo `SQLEXPRESS`) y, si eliges autenticación
  mixta, la contraseña del usuario `sa`.

### Comprobar que todo quedó instalado

Abre **PowerShell** (menú Inicio → escribe "PowerShell") y ejecuta:

```powershell
git --version
dotnet --list-sdks      # debe aparecer una línea que empiece por 10.
node --version          # debe ser v20.19 o superior (recomendado v22)
sqllocaldb info         # solo si elegiste LocalDB: debe mostrar MSSQLLocalDB
```

---

## 2. Descargar el código

En PowerShell, ve a la carpeta donde quieras guardar el proyecto y clónalo:

```powershell
cd D:\Proyectos            # o la carpeta que prefieras
git clone https://github.com/amatencioc/net_InstaVende.git net_InstaVende
cd net_InstaVende
```

---

## 3. Elegir la base de datos

Abre `src\InstaVende.Web\appsettings.Development.json` con cualquier editor. Verás dos conexiones; **solo una
debe quedar sin `//` delante**:

```jsonc
// Opción 1 · Esta PC (SQL Server LocalDB, viene con Visual Studio)
"DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=InstaVendeDb_Dev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

// Opción 2 · Conexión anterior (SQL Server de casa)
// "DefaultConnection": "Server=.;Database=InstaVendeDb_Dev;User Id=sa;Password=sql;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

- **Si instalaste LocalDB (opción A):** no cambies nada.
- **Si instalaste SQL Server Express/Developer (opción B):** comenta la opción 1, descomenta la 2 y ajusta:
  - `Server=.` → la instancia predeterminada de la PC. Para Express suele ser `Server=.\\SQLEXPRESS`
    (en el JSON la barra se escribe doble: `\\`).
  - `User Id=sa;Password=...` → tu usuario y contraseña. Si usas tu usuario de Windows, reemplázalo por
    `Trusted_Connection=True`.

> La base de datos **no hace falta crearla**: el script del paso 5 la crea si no existe.

---

## 4. (Opcional) Activar la inteligencia artificial

Sin clave de OpenAI la app funciona, pero el vendedor solo responde comandos (`#catalogo`, `agregar 1`,
`#pedido`…) y respuestas fijas. Para que converse libremente, ejecuta en la carpeta del proyecto:

```powershell
dotnet user-secrets --project src\InstaVende.Web set "OpenAI:ApiKey" "sk-tu-clave"
```

La clave se guarda en tu usuario de Windows, fuera del proyecto (no se sube a git).

---

## 5. Compilar, preparar la base de datos y levantar la app

Desde la carpeta del proyecto (puedes hacer **doble clic** en el archivo indicado o ejecutarlo en PowerShell):

### Primera vez — con datos de demostración

```powershell
.\scripts\iniciar-app.cmd -SeedDemo demo@instavende.pe
```

### Las siguientes veces

```powershell
.\scripts\iniciar-app.cmd
```

El script hace todo automáticamente:

1. Verifica .NET 10, Node.js y el navegador para WhatsApp.
2. Instala las dependencias del panel y del servicio de WhatsApp.
3. Compila la solución.
4. Hace un **respaldo** de la base de datos (si ya existe) y aplica las migraciones pendientes.
5. Con `-SeedDemo`: crea el usuario de demostración (si no existe) y llena la app con datos de prueba.
6. Abre dos ventanas (API y panel web) y el navegador en la pantalla de inicio de sesión.

**No cierres las dos ventanas que se abren**: son la API y el panel. Mientras estén abiertas, la app funciona.

Opciones útiles del script:

| Opción | Efecto |
|---|---|
| `-SeedDemo correo` | Crea o regenera los datos de demostración de ese usuario |
| `-SinMigrar` | No toca la base de datos |
| `-SinRespaldo` | Migra sin hacer respaldo antes |
| `-SoloCompilar` | Solo instala dependencias y compila |
| `-SinNavegador` | No abre el navegador |

---

## 6. Entrar a la aplicación

| | Dirección |
|---|---|
| Panel web | http://localhost:5173 |
| API y su documentación | http://localhost:5221/api/docs |

Usuarios de demostración (contraseña `Demo12345` en ambos):

| Usuario | Rol |
|---|---|
| `demo@instavende.pe` | Propietario y administrador del negocio "Urban Step" |
| `vendedor@instavende.pe` | Miembro del equipo (sin permisos de administración) |

### (Opcional) Que el login aparezca ya relleno

Crea el archivo `frontend\.env.development.local` (no se sube a git) con este contenido y recarga la página:

```
VITE_DEV_LOGIN_EMAIL=demo@instavende.pe
VITE_DEV_LOGIN_PASSWORD=Demo12345
```

---

## 7. Conectar WhatsApp (opcional)

1. En el panel ve a **Canales → WhatsApp**.
2. Espera a que aparezca el código QR (la primera vez tarda hasta un minuto).
3. En el teléfono: WhatsApp → **Dispositivos vinculados** → **Vincular un dispositivo** → escanea el QR.
4. Cuando diga "Conectado", los mensajes que reciba ese número llegan a **Mensajes** y el vendedor IA responde.

> Usa un número de prueba: este modo funciona con WhatsApp Web. Para producción se recomienda la API oficial
> de WhatsApp Cloud (**Canales → Meta**).

---

## 8. Uso diario

| Quiero… | Hago… |
|---|---|
| Levantar la app | `scripts\iniciar-app.cmd` |
| Detener la app | `scripts\detener-app.cmd` (o cerrar las dos ventanas) |
| Traer cambios nuevos del repositorio | `git pull` y luego `scripts\iniciar-app.cmd` (aplica migraciones nuevas solo) |
| Solo actualizar la base de datos | `scripts\migrar-bd.cmd` |
| Volver a tener los datos demo como nuevos | `scripts\iniciar-app.cmd -SeedDemo demo@instavende.pe` |
| Cambiar de base de datos | Editar `appsettings.Development.json` (paso 3) y ejecutar `scripts\migrar-bd.cmd` |

Los respaldos automáticos quedan en la carpeta de respaldos de SQL Server o, con LocalDB, en
`database\respaldos\` (no se suben a git).

---

## 9. Solución de problemas

| Síntoma | Solución |
|---|---|
| "No se puede cargar el archivo … porque la ejecución de scripts está deshabilitada" | Usa los archivos `.cmd` (doble clic) o ejecuta `powershell -ExecutionPolicy Bypass -File .\scripts\iniciar-app.ps1` |
| "Se necesita el SDK de .NET 10" / "Falta Node.js" | Instala lo indicado (paso 1) y abre una ventana nueva de PowerShell |
| "No se pudo conectar" al migrar | SQL Server no está iniciado o la conexión del paso 3 no es correcta. Con LocalDB: `sqllocaldb start MSSQLLocalDB`. Con Express: abre "Servicios" de Windows e inicia "SQL Server (SQLEXPRESS)" |
| "Login failed for user 'sa'" | Usuario o contraseña incorrectos, o SQL Server sin autenticación mixta: usa `Trusted_Connection=True` |
| "No se pudo respaldar" | El servicio de SQL Server no puede escribir en su carpeta de respaldos. Corrige permisos o ejecuta con `-SinRespaldo` (bajo tu responsabilidad) |
| "El puerto 5221 / 5173 ya está en uso" | `scripts\detener-app.cmd` y vuelve a iniciar |
| La API se cierra al arrancar | Lee el mensaje en su ventana; suele ser la conexión a la base (paso 3) |
| El QR de WhatsApp no aparece | Comprueba que Chrome o Edge estén instalados; reinicia con `detener-app` + `iniciar-app` |
| El panel muestra "Desconectado" arriba a la derecha | La API no está en marcha o se reinició: espera unos segundos o revisa su ventana |

---

## 10. Para saber más

- `README.md` — resumen del proyecto y comandos.
- `docs/ARCHITECTURE.md` — arquitectura, módulos y convenciones de código.
- `deploy/docker-compose.yml` — despliegue completo con Docker (SQL Server + API + WhatsApp + panel).
