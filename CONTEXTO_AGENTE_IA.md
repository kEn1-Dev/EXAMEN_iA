# Contexto del Proyecto `MonitorSerial` para Agente de IA (Servidor Local / Casa)

Este documento contiene todo el contexto técnico, las reglas del examen y el estado actual del proyecto **`MonitorSerial`** para que el Agente de IA en el servidor de casa (`192.168.1.100`) pueda continuar el despliegue en **Docker / IIS** y publicarlo mediante **Cloudflare Tunnel (`*.kenys.site`)** sin perder ningún requisito de evaluación.

---

## 1. Objetivo del Proyecto y Reglas del Examen (20 Puntos)

El proyecto es un examen práctico de **20 puntos** que consiste en un sistema web llamado **`MonitorSerial`** para vigilar la temperatura de una sala de equipos por puerto serie (`COM`), guardarla en **SQL Server** con **Entity Framework (Database First)**, mostrarla con **Materialize CSS local** y restringir el acceso con **ASP.NET Identity**.

### Reglas estrictas que NO se deben romper al modificar el repositorio:
1. **Entregable en el repositorio Git:**
   * La solución debe estar **SIN las carpetas `bin`, `obj` ni `packages`** en el repositorio (usar `.gitignore` para excluirlas siempre).
   * En la raíz del repositorio deben estar siempre presentes **`script.sql`** y **`README.md`**.
   * **Prohibido** incluir contraseñas reales en `Web.config`, código o documentación (usar solo las credenciales ficticias de laboratorio `DemoLab2026`).
2. **Interfaz Materialize Local (Parte 1 - 3 pts):**
   * Prohibido usar CDN de Materialize y prohibido usar Bootstrap.
   * Los archivos `Content/materialize.min.css` y `Scripts/materialize.min.js` están integrados localmente en `App_Start/BundleConfig.cs`, y `Views/Shared/_Layout.cshtml` ejecuta `M.AutoInit()`.
3. **Base de Datos y EF Database First (Parte 2 - 4 pts):**
   * `script.sql` recrea desde cero la BD `MonitorSerial` con las tablas `Dispositivo` (`DispositivoId`, `Nombre`, `PuertoCOM`, `Activo`) y `Lectura` (`LecturaId`, `DispositivoId`, `Temperatura`, `FechaHora`) con clave foránea `FK_Lectura_Dispositivo` e inserta dispositivos de prueba (`COM3`, `COM1`).
   * El modelo Entity Framework Database First se llama `MonitorModel` (`Models/MonitorModel.edmx`, Contexto: `MonitorSerialEntities`).
4. **Servicio Serial y Lectura AJAX (Parte 3 - 4 pts):**
   * `Services/ServicioSerial.cs` implementa `IDisposable`, lista puertos COM, abre el puerto a `9600 baudios, 8 bits, Parity.None, StopBits.One`, captura excepciones (`UnauthorizedAccessException`, `IOException`, `TimeoutException`), envía el comando `T?` en `LeerTemperatura` y convierte la respuesta con `CultureInfo.InvariantCulture`.
   * `Controllers/SensorController.cs` tiene la acción `[HttpPost] Leer` que usa una instancia `static readonly ServicioSerial` protegida con `lock`, guarda la lectura con Entity Framework dentro de un bloque `using` y responde en JSON para actualizar la tarjeta en `Views/Sensor/Index.cshtml` sin recargar la página.
5. **Historial y Validación con Metadatos (Parte 4 - 3 pts):**
   * `Controllers/LecturasController.cs` muestra el historial ordenado de la lectura más reciente a la más antigua (`.OrderByDescending(l => l.FechaHora)`).
   * `Models/LecturaMetadata.cs` aplica `[MetadataType(typeof(LecturaMetadata))]` sobre la clase parcial `Lectura` con `[Required]` y `[Range(typeof(decimal), "-40", "125")]` con mensajes en español.
6. **Autenticación y Roles (Parte 5 - 4 pts):**
   * `SensorController` y `LecturasController` tienen `[Authorize]`.
   * Solo el rol `Administrador` (`[Authorize(Roles = "Administrador")]`) puede editar (`Edit`) y eliminar (`Delete`) lecturas; los demás usuarios autenticados solo consultan y registran.
   * Usuarios de prueba preconfigurados:
     * **Administrador:** `admin@monitorserial.local` / `DemoLab2026`
     * **Operador (Estándar):** `operador@monitorserial.local` / `DemoLab2026`

---

## 2. Arquitectura de Doble Modo Ya Implementada en el Código

Para que el proyecto funcione tanto en un servidor con SQL Server + puerto COM físico como en un entorno sin hardware serial conectado o sin instancia local de SQL Server:

* **`Services/RepositorioRespaldoLocal.cs`**:
  * Comprueba automáticamente si hay una instancia de SQL Server activa (`HaySqlServerDisponible()`).
  * Si **hay SQL Server**, la aplicación usa `MonitorSerialEntities` (EF Database First) y `ApplicationDbContext` (ASP.NET Identity en SQL Server).
  * Si **no hay SQL Server accesible**, la aplicación hace *fallback* transparente a un archivo JSON persistente en `App_Data/MonitorSerial_Datos.json` pre-poblado con los mismos dispositivos de `script.sql` y los dos usuarios de prueba (`admin@monitorserial.local` y `operador@monitorserial.local`), emitiendo cookies OWIN reales con sus respectivos claims de rol.
* **`Services/ServicioSerial.cs`**:
  * Si el puerto físico `COM3`/`COM1` existe en el sistema operativo, abre el puerto real con `System.IO.Ports.SerialPort` a `9600, 8N1` y envía `T?`.
  * Si el servidor no tiene un sensor físico conectado por USB/Serial, emula la trama de respuesta de `T?` en `COM3`/`COM1` (ej. `"23.45"`) y la procesa por el mismo `decimal.TryParse(..., CultureInfo.InvariantCulture)`, mientras que si se consulta un puerto inexistente (ej. `COM99`) dispara el flujo real de excepción controlada.

---

## 3. Entorno del Servidor de Casa (`192.168.1.100`) y Cloudflare Tunnel

El servidor de casa tiene la IP local **`192.168.1.100`**, cuenta con **Docker / Portainer** y un **Cloudflare Tunnel** bajo el dominio **`kenys.site`**.

### Puertos que YA están ocupados en `192.168.1.100` (NO USAR):
* `80` (`appflowy.kenys.site`)
* `4321` (`ClothingStoresGrupoK.kenys.site`)
* `5678` (`n8n.kenys.site`)
* `8080` (`vault.kenys.site`)
* `8082` (`drive.kenys.site`)
* `8085` (`chartdb.kenys.site`)
* `9000` (`portainer.kenys.site`)

### Puerto recomendado para `MonitorSerial` en `192.168.1.100`:
* **Puerto sugerido:** **`8090`** (`http://192.168.1.100:8090`)
* **Subdominio sugerido en Cloudflare Tunnel:** **`monitorserial.kenys.site`** $\rightarrow$ `http://192.168.1.100:8090`

---

## 4. Instrucciones para el Agente de IA en el Servidor (`192.168.1.100`)

Cuando clones el repositorio en el servidor `192.168.1.100`, realiza lo siguiente según el sistema operativo donde corra Docker/el servidor:

### Escenario A: Si el servidor `192.168.1.100` corre Windows
1. Puedes publicar la carpeta `Publicacion_IIS` directamente en **IIS** (creando un Application Pool `.NET CLR v4.0` en modo **Integrado** y asignando el sitio al puerto `8090`), **o bien** ejecutar IIS Express / contenedor Windows en el puerto `8090`.
2. Si deseas levantar también **SQL Server** en Docker o local:
   * Ejecuta `script.sql` en la instancia de SQL Server.
   * Ajusta el `Data Source` en `Web.config` si SQL Server escucha en un puerto o instancia específica.

### Escenario B: Si el servidor `192.168.1.100` corre Linux con Docker (Portainer)
1. Dado que el código fuente entregable (`MonitorSerial/`) debe permanecer en **ASP.NET MVC 5 (.NET Framework 4.7.2)** para la revisión del profesor, **NO modifiques ni borres** los archivos de `MonitorSerial/` que exige la rúbrica (`MonitorModel.edmx`, `BundleConfig.cs`, `ServicioSerial.cs`, etc.).
2. Para exponer el servicio web en Linux Docker en `http://192.168.1.100:8090`:
   * Puedes levantar un contenedor con `mono` / `xsp4` (ej. imagen `mono:latest` ejecutando `xsp4 --port 8090 --address 0.0.0.0` sobre `Publicacion_IIS`), **o** un contenedor ligero en una subcarpeta `docker/` que sirva exactamente las mismas rutas, vistas Materialize locales y base de datos SQL Server (`mcr.microsoft.com/mssql/server:2022-latest`) en el puerto `8090`, manteniendo intacta la carpeta `MonitorSerial/` para la evaluación académica.
3. Verifica que exista el archivo `.gitignore` en la raíz del repositorio excluyendo:
   ```gitignore
   [Bb]in/
   [Oo]bj/
   [Pp]ackages/
   *.suo
   *.user
   .vs/
   ```
4. Una vez que el servicio responda en `http://192.168.1.100:8090`, indícale al usuario que agregue la ruta pública en **Cloudflare Zero Trust > Networks > Tunnels > Public Hostname**:
   * **Subdomain:** `monitorserial`
   * **Domain:** `kenys.site`
   * **Type:** `HTTP`
   * **URL:** `192.168.1.100:8090`
