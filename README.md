# AltarWeb

Sistema de gestion y evaluacion para el Concurso de Altares de Muertos de la Facultad de Ingenieria (FIM). Este aplicativo permite a los jueces registrar evaluaciones, calcular puntajes basados en criterios tradicionales y de personalizacion, y generar constancias de participacion automatizadas en formato PDF.

## Requisitos

- .NET 8.0 SDK o superior.
- SQL Server (LocalDB incluido en Visual Studio).
- Conexion a internet para el envio de correos electronicos y carga de librerias externas (CDN).

## Configuracion e Instalacion

1. Clonar el repositorio:
   git clone https://github.com/AbrahamHamedFloresCabanillas/AltarWeb.git

2. Configurar la cadena de conexion:
   El archivo appsettings.json contiene la configuracion por defecto para SQL Server LocalDB. Si se requiere utilizar una instancia distinta, modificar la propiedad ConnectionStrings.AltarWebContext.

3. Aplicar migraciones de base de datos:
   Ejecutar el siguiente comando en la consola de administracion de paquetes o terminal en la raiz del proyecto:
   dotnet ef database update

4. Ejecutar el proyecto:
   dotnet run --project AltarWeb

## Funcionalidades Principales

- Registro de evaluaciones con desglose de integrantes y datos del altar.
- Calculo automatico de nota final basado en rubrica de tradicion, estetica y personalizacion.
- Generacion de constancias en PDF utilizando la libreria QuestPDF.
- Envio automatico de resultados via correo electronico (Gmail SMTP).
- Historial de evaluaciones por juez.

## Tecnologias Utilizadas

- ASP.NET Core MVC 8.0
- Entity Framework Core
- QuestPDF (Generacion de documentos)
- Bootstrap 5 y FontAwesome (Interfaz de usuario)
