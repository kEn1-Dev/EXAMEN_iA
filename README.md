# MonitorSerial — Vigilancia de Temperatura por Puerto Serie

Aplicación web construida con **ASP.NET MVC 5 (.NET Framework 4.7.2)**, **Entity Framework 6 (Database First)**, **SQL Server**, **ASP.NET Identity** y **Materialize CSS (local)** para vigilar y registrar la temperatura de una sala de equipos a través del puerto serie (`COM`).

---

## 1. Requisitos del Sistema

* **Sistema Operativo:** Windows 10 / Windows 11 o Windows Server con **IIS** habilitado.
* **Framework:** .NET Framework 4.7.2 / 4.8 (`.NET CLR v4.0` en modo Integrado en IIS).
* **Motor de Base de Datos:** SQL Server (Express, Developer, Standard o LocalDB) y SQL Server Management Studio (SSMS).
* **Entorno de Desarrollo:** Visual Studio 2019 o 2022 con la carga de trabajo *Desarrollo web y ASP.NET*.
* **Puerto Serie:** Dispositivo físico conectado por puerto `COM` (a `9600 baudios, 8N1`) o par de puertos virtuales (ej. `com0com` con el script incluido `SimuladorSensorSerial.ps1`).

---

## 2. Configuración de la Base de Datos y Cadena de Conexión

### Paso 1: Crear la Base de Datos con `script.sql`
1. Abra **SQL Server Management Studio (SSMS)** y conéctese a su instancia local de SQL Server.
2. Abra el archivo [`script.sql`](./script.sql) incluido en la raíz de la solución y ejecútelo (`F5`).
3. El script recrea desde cero la base de datos **`MonitorSerial`**, crea las tablas **`Dispositivo`** y **`Lectura`** relacionadas mediante la clave foránea `FK_Lectura_Dispositivo`, e inserta los dispositivos de prueba (`COM3` y `COM1`) junto con lecturas iniciales.

### Paso 2: Configurar la Conexión en `Web.config`
En el archivo `MonitorSerial/Web.config`, verifique la sección `<connectionStrings>`:

```xml
<connectionStrings>
  <add name="DefaultConnection"
       connectionString="Data Source=localhost;Initial Catalog=MonitorSerial;Integrated Security=True;MultipleActiveResultSets=True"
       providerName="System.Data.SqlClient" />
  <add name="MonitorSerialEntities"
       connectionString="metadata=res://*/Models.MonitorModel.csdl|res://*/Models.MonitorModel.ssdl|res://*/Models.MonitorModel.msl;provider=System.Data.SqlClient;provider connection string=&quot;data source=localhost;initial catalog=MonitorSerial;integrated security=True;MultipleActiveResultSets=True;App=EntityFramework&quot;"
       providerName="System.Data.EntityClient" />
</connectionStrings>
```

* Si su instancia de SQL Server tiene nombre (por ejemplo `.\SQLEXPRESS`), reemplace `localhost` por `.\SQLEXPRESS` en ambas cadenas (`DefaultConnection` y `MonitorSerialEntities`).
* Por seguridad (**Parte 2.4**), se utiliza `Integrated Security=True` (Autenticación de Windows) y **no se incluyen contraseñas de uso real**.

---

## 3. Usuarios de Prueba (Cuentas Individuales y Roles)

Al iniciar la aplicación por primera vez (`Startup.Auth.cs`), el sistema crea automáticamente el rol **`Administrador`** y registra dos cuentas de prueba con **contraseñas ficticias exclusivas de laboratorio**:

| Perfil / Rol | Correo Electrónico (Usuario) | Contraseña de Laboratorio (Ficticia) | Permisos en el Sistema |
| :--- | :--- | :--- | :--- |
| **Administrador** | `admin@monitorserial.local` | `DemoLab2026` | Consultar monitor, leer puerto serie, ver historial, crear, **editar** y **eliminar** lecturas. |
| **Usuario Estándar (Operador)** | `operador@monitorserial.local` | `DemoLab2026` | Consultar monitor, leer puerto serie, ver historial y registrar nuevas lecturas (**sin permiso** para editar ni eliminar). |

> **Nota de seguridad (Parte 5.4):** Las credenciales anteriores son de uso estrictamente académico/ficticio para evaluación en laboratorio y no corresponden a ninguna cuenta real.

---

## 4. Párrafo sobre Permisos de la Identidad del Grupo de Aplicaciones en IIS (Parte 6.3)

Para que la aplicación publicada en **IIS** pueda usar el puerto `COM` y conectarse a **SQL Server**, la identidad del grupo de aplicaciones (por defecto `IIS AppPool\MonitorSerialPool`, bajo la cual se ejecuta el proceso de trabajo `w3wp.exe`) requiere permisos específicos tanto en el sistema operativo como en el motor de base de datos. En primer lugar, para abrir y comunicarse por el puerto serie (`COM`), la cuenta del grupo de aplicaciones debe tener permisos de lectura y escritura sobre los dispositivos locales de hardware en Windows (ejecutándose con una cuenta local autorizada o `LocalSystem`/`NetworkService` en entornos de laboratorio) y el puerto no debe estar bloqueado de forma exclusiva por otro proceso (como el Monitor Serie del IDE). En segundo lugar, al utilizar autenticación integrada (`Integrated Security=True`), es necesario registrar en SQL Server un inicio de sesión (*Login*) para la cuenta `IIS AppPool\MonitorSerialPool` (o para la cuenta de servicio asignada al *Application Pool*) y otorgarle en la base de datos `MonitorSerial` los permisos de lectura y escritura (`db_datareader` y `db_datawriter`, además de `db_ddladmin` la primera vez que ASP.NET Identity genera sus tablas de usuarios y roles).

---

## 5. Estructura del Proyecto

* `script.sql`: Script T-SQL reproducible desde cero para crear la BD `MonitorSerial`, las tablas `Dispositivo` y `Lectura`, su clave foránea y datos iniciales.
* `MonitorSerial/App_Start/BundleConfig.cs`: Registro local de `materialize.min.css` y `materialize.min.js` (sin CDN y sin Bootstrap).
* `MonitorSerial/Services/ServicioSerial.cs`: Clase que implementa `IDisposable`, lista puertos COM, configura `9600, 8, None, One`, envía `T?` y parsea con `CultureInfo.InvariantCulture`.
* `MonitorSerial/Controllers/SensorController.cs`: Protegido con `[Authorize]`, usa una instancia estática de `ServicioSerial` sincronizada con `lock`, guarda lecturas con Entity Framework dentro de un bloque `using` y responde en JSON.
* `MonitorSerial/Controllers/LecturasController.cs`: CRUD de `Lectura` ordenado de más reciente a más antigua (`OrderByDescending`), protegido con `[Authorize]` y restringido con `[Authorize(Roles = "Administrador")]` para Editar y Eliminar.
* `MonitorSerial/Models/LecturaMetadata.cs`: Clase de metadatos (`[MetadataType]`) con validaciones `[Required]` y `[Range(-40, 125)]` con mensajes en español.
