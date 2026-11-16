/* =========================================================================
   EXAMEN: MonitorSerial
   SCRIPT T-SQL PARA CREAR Y RECREAR LA BASE DE DATOS DESDE CERO
   Parte 2: Base de datos y Entity Framework
   ========================================================================= */

USE master;
GO

-- 1. Si la base de datos ya existe, cerramos conexiones activas y la eliminamos
IF DB_ID(N'MonitorSerial') IS NOT NULL
BEGIN
    ALTER DATABASE [MonitorSerial] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [MonitorSerial];
END
GO

-- 2. Crear la base de datos desde cero
CREATE DATABASE [MonitorSerial];
GO

USE [MonitorSerial];
GO

-- 3. Crear la tabla Dispositivo
-- (identificador, nombre, puerto COM y estado activo)
CREATE TABLE [dbo].[Dispositivo] (
    [DispositivoId] INT IDENTITY(1,1) NOT NULL,
    [Nombre]        NVARCHAR(100)     NOT NULL,
    [PuertoCOM]     NVARCHAR(20)      NOT NULL,
    [Activo]        BIT               NOT NULL CONSTRAINT [DF_Dispositivo_Activo] DEFAULT (1),
    CONSTRAINT [PK_Dispositivo] PRIMARY KEY CLUSTERED ([DispositivoId] ASC)
);
GO

-- 4. Crear la tabla Lectura
-- (identificador, dispositivo, temperatura decimal y fecha y hora)
CREATE TABLE [dbo].[Lectura] (
    [LecturaId]     INT IDENTITY(1,1) NOT NULL,
    [DispositivoId] INT               NOT NULL,
    [Temperatura]   DECIMAL(5, 2)     NOT NULL,
    [FechaHora]     DATETIME          NOT NULL CONSTRAINT [DF_Lectura_FechaHora] DEFAULT (GETDATE()),
    CONSTRAINT [PK_Lectura] PRIMARY KEY CLUSTERED ([LecturaId] ASC),
    CONSTRAINT [FK_Lectura_Dispositivo] FOREIGN KEY ([DispositivoId])
        REFERENCES [dbo].[Dispositivo] ([DispositivoId])
        ON DELETE CASCADE
);
GO

-- 5. Insertar al menos un dispositivo de prueba con el puerto COM del equipo
INSERT INTO [dbo].[Dispositivo] ([Nombre], [PuertoCOM], [Activo])
VALUES
    (N'Sensor Sala de Equipos Principal', N'COM3', 1),
    (N'Sensor Rack Respaldo', N'COM1', 1);
GO

-- 6. Insertar lecturas iniciales de prueba para verificación del historial
INSERT INTO [dbo].[Lectura] ([DispositivoId], [Temperatura], [FechaHora])
VALUES
    (1, 22.50, DATEADD(MINUTE, -30, GETDATE())),
    (1, 23.10, DATEADD(MINUTE, -15, GETDATE())),
    (1, 21.85, GETDATE());
GO
