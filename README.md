# AltarWeb 🏵️💀

**AltarWeb** es una aplicación web desarrollada en **ASP.NET Core MVC (.NET 8)** diseñada para gestionar y evaluar concursos de Altares de Muertos. Permite a los jueces registrar evaluaciones detalladas, calcular puntuaciones automáticamente, generar constancias de participación en formato PDF y enviarlas por correo electrónico.

## ✨ Características Principales

- 👨‍⚖️ **Sistema de Jueces**: Login y gestión de sesiones para evaluadores.
- 👥 **Registro de Equipos**: Gestión dinámica de integrantes (Nombres, Matrículas y Correos).
- 📋 **Evaluación Detallada**: Checklist de elementos tradicionales y rúbrica de calificación personalizada.
- 🏆 **Cálculo Automático**: Algoritmo que promedia tradición, personalización y estética.
- 📄 **Generación de PDFs**: Creación automática de constancias de agradecimiento usando [QuestPDF](https://www.questpdf.com/).
- 📧 **Notificaciones por Email**: Envío automático de resultados y constancias a los integrantes del equipo.

## 🛠️ Tecnologías Utilizadas

- **Framework**: .NET 8 (ASP.NET Core MVC)
- **Base de Datos**: SQL Server (Entity Framework Core)
- **PDF**: QuestPDF
- **Frontend**: Bootstrap 5 + Vanilla JavaScript + FontAwesome
- **Email**: SmtpClient (Gmail SMTP)

## 🚀 Cómo Hacerlo Funcionar

### 1. Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado.
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (o LocalDB que viene con Visual Studio).
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Recomendado).

### 2. Configuración de la Base de Datos

El proyecto utiliza una cadena de conexión hacia `LocalDB` por defecto. Puedes verificarla o cambiarla en el archivo `AltarWeb/appsettings.json`:

```json
"ConnectionStrings": {
  "AltarWebContext": "Server=(localdb)\\mssqllocaldb;Database=AltarWebDB;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

### 3. Ejecutar Migraciones

Abre una terminal en la carpeta raíz del proyecto y ejecuta:

```bash
dotnet ef database update --project AltarWeb
```

*Nota: Esto creará las tablas necesarias y sembrará los datos iniciales (Jueces por defecto).*

### 4. Ejecutar la Aplicación

```bash
dotnet run --project AltarWeb
```

La aplicación estará disponible en `https://localhost:7123` (o el puerto configurado).

## 📝 Notas de Uso

- **Credenciales por defecto**: El sistema incluye datos de prueba para los jueces. Puedes consultar el archivo `SeedData.cs` o la base de datos para ver los usuarios registrados.
- **Configuración de Correo**: Actualmente utiliza una cuenta de Gmail configurada en el `EvaluacionController.cs`. Para producción, se recomienda usar variables de entorno o `User Secrets`.

---
Desarrollado con ❤️ para la preservación de las tradiciones mexicanas.
