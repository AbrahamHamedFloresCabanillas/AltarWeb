using Concurso.Core.Pdf;

namespace AltarWeb.Services
{
    // Contenido de los manuales de usuario del Concurso de Altares, uno por rol (peticion del propietario, 2026-10-22).
    // Describe las pantallas tal como funcionan hoy: si cambia un flujo, actualizar aqui el manual correspondiente.
    // La maquetacion del PDF es comun a las dos apps (Concurso.Core.Pdf.ManualUsuarioPdf).
    public static class ManualesAltares
    {
        private const string Concurso = "Concurso de Altares";

        private static readonly SeccionManual Acceso = new(
            "Entrar al sistema",
            "Abre la dirección del Concurso de Altares en tu navegador. La pantalla de inicio tiene dos pestañas: " +
            "\"Registro\" para participantes y \"Jueces / Admin\" para el jurado y la administración.",
            Avisos: new[]
            {
                "Después de 5 contraseñas incorrectas seguidas, la cuenta queda en pausa 5 minutos. Si alguien la bloquea a propósito, la administración puede desbloquearla.",
                "Usa siempre tu correo institucional @uabc.edu.mx.",
                "Para salir, usa el ícono de salida junto a tu nombre."
            });

        public static readonly ManualUsuario Participante = new(Concurso, "Participante",
            "Esta guía es para alumnos, maestros y administrativos que inscriben un altar: cómo crear tu cuenta, formar tu " +
            "equipo, completar la Ficha de Registro del Altar y descargar tu constancia.",
            new[]
            {
                Acceso,
                new SeccionManual("Crear tu cuenta",
                    "En la pestaña \"Registro\" elige \"Regístrate con tu matrícula o número de empleado\" o \"Continuar con Google\" con tu cuenta institucional.",
                    new[]
                    {
                        "Escribe tu nombre completo.",
                        "Escribe tu Matrícula / No. de empleado: 7 dígitos si eres alumno, 5 dígitos si eres maestro o administrativo. El tipo de cuenta se detecta solo.",
                        "Escribe tu correo institucional @uabc.edu.mx y, si quieres, tu teléfono, género, programa educativo y autodescripción cultural (opcionales).",
                        "Elige una contraseña de al menos 6 caracteres, confírmala y presiona registrar. Entrarás directo a tu portal."
                    },
                    new[] { "Los datos de género y autodescripción solo se usan en reportes agregados; nunca se publican por persona." }),
                new SeccionManual("Tu portal",
                    "Al entrar ves tu portal: tu equipo (si ya tienes), el estado de la Ficha de Registro, las invitaciones que " +
                    "recibiste y la lista de elementos requeridos del altar con su significado, como guía para armarlo."),
                new SeccionManual("Crear tu equipo",
                    "Quien crea el equipo es su organizador y es el único que edita la Ficha.",
                    new[]
                    {
                        "En tu portal presiona \"Crear mi equipo\".",
                        "Escribe el nombre del grupo y el nombre del altar, y elige el programa educativo.",
                        "Escribe la matrícula del maestro encargado (5 dígitos). Si aún no tiene cuenta, queda \"pendiente\" y se vincula sola cuando se registre.",
                        "Escribe la ubicación del altar, el nombre del difunto y su fecha de defunción.",
                        "Guarda. El equipo recibe un código público (por ejemplo ALT-2026-2-0001) que puedes compartir, por ejemplo para inscribirse en el Concurso de Catrinas."
                    },
                    new[]
                    {
                        "La fecha de defunción debe ser de al menos un año atrás y no puede ser futura.",
                        "Solo puedes pertenecer a un equipo por periodo."
                    }),
                new SeccionManual("Invitar integrantes",
                    "Los integrantes no se agregan solos: el organizador los invita y cada persona acepta desde su portal.",
                    new[]
                    {
                        "En la Ficha, escribe la Matrícula / No. de empleado de la persona y presiona \"Invitar\".",
                        "La persona ve la invitación en su portal y presiona \"Aceptar\" o \"Rechazar\". Solo queda en el equipo si acepta.",
                        "Mientras no acepte, verás la invitación como pendiente (solo con la matrícula) y puedes cancelarla."
                    },
                    new[]
                    {
                        "Por privacidad, el sistema responde siempre el mismo mensaje al invitar: no confirma si la matrícula existe ni si la persona ya tiene equipo.",
                        "La persona invitada debe tener cuenta registrada. Pídele que se registre primero."
                    }),
                new SeccionManual("Recibir una invitación",
                    "Si otro organizador te invita y aún no tienes equipo, verás la invitación en tu portal con el nombre del equipo, del altar y de quien te invita. " +
                           "Presiona \"Aceptar\" para unirte o \"Rechazar\" para descartarla. Al aceptar, tus demás invitaciones del periodo se descartan."),
                new SeccionManual("Designar al Narrador",
                    "Cada equipo necesita exactamente un Narrador: la persona que explica el altar al jurado.",
                    new[]
                    {
                        "En la Ficha, junto al integrante, presiona \"Hacer narrador\".",
                        "Si eliges a otra persona, el Narrador anterior vuelve a ser integrante."
                    },
                    new[] { "Sin Narrador designado el jurado no puede cerrar la evaluación como Final." }),
                new SeccionManual("Completar y editar la Ficha de Registro",
                    "El portal te indica qué datos faltan para que la Ficha esté completa. Puedes editarla las veces que necesites hasta:",
                    Avisos: new[]
                    {
                        "La fecha límite de requisitos que fija la administración.",
                        "El momento en que un juez registra la evaluación de tu equipo: desde ahí la Ficha y la lista de integrantes quedan cerradas."
                    }),
                new SeccionManual("Descargar tu constancia",
                    "Cuando el jurado cierra la evaluación de tu equipo como Final, aparece en tu portal el botón para descargar tu constancia en PDF. " +
                    "Si tu equipo obtuvo 1.º, 2.º o 3.er lugar en su programa educativo, la constancia lo indica."),
                new SeccionManual("Problemas frecuentes",
                    Avisos: new[]
                    {
                        "\"Correo o contraseña incorrectos\": revisa que sea tu correo institucional completo.",
                        "\"Tu cuenta fue desactivada\": contacta a la administración del concurso.",
                        "Página de \"demasiados intentos\": espera 5 minutos o pide a la administración que desbloquee tu cuenta.",
                        "No puedo editar la Ficha: ya pasó la fecha límite de requisitos o tu equipo ya fue evaluado."
                    })
            });

        private static readonly SeccionManual[] SeccionesJuez =
        {
            Acceso,
            new("Obtener tu cuenta de juez",
                "La administración puede crearte la cuenta, o puedes solicitarla tú.",
                new[]
                {
                    "En la pantalla de inicio, en la pestaña \"Jueces / Admin\", presiona \"Solicita acceso\" (o usa \"Continuar con Google\").",
                    "Captura tu nombre, tu correo @uabc.edu.mx, tu Matrícula / No. de empleado (5 dígitos) y una contraseña.",
                    "Tu solicitud queda pendiente hasta que la administración la apruebe; después ya puedes entrar."
                },
                new[] { "Si la administración te creó la cuenta con una contraseña temporal, el sistema te pedirá cambiarla al primer ingreso." }),
            new("Menú del juez",
                "En el menú lateral encontrarás: Historial (todas las evaluaciones), Nueva Evaluación, Recorrido PDF (el mapa de altares que " +
                "sube la administración) y Manuales (estas guías en PDF)."),
            new("Buscar un equipo y revisar su Ficha",
                null,
                new[]
                {
                    "Entra a \"Nueva Evaluación\" y busca el equipo por nombre o por programa educativo.",
                    "Selecciónalo para ver su Ficha: integrantes, Narrador, maestro encargado, difunto y ubicación.",
                    "Revisa el aviso de estado de la Ficha: si falta información o Narrador, puedes guardar en Preliminar, pero no cerrar como Final."
                }),
            new("Calificar el altar",
                "La calificación tiene dos partes: la lista de elementos y los criterios del jurado.",
                new[]
                {
                    "Elige si el altar es de 3 o de 7 niveles y su tipo, y revisa o completa la semblanza (gustos y temática del difunto).",
                    "Para cada elemento indica el nivel donde quedó colocado y califícalo: No lo tiene, Regular o Bueno. Marca \"¿Tematizado?\" si alude al difunto o a la temática. Toca (i) para ver el mini-manual del elemento.",
                    "El Objetivo Cultural se calcula solo con la lista de elementos.",
                    "Mueve los controles de 0 a 10 de Creatividad, Valoración General, Distribución por Niveles y Explicación oral. Cada uno trae su guía de qué evaluar.",
                    "Revisa la nota final ponderada que se actualiza en vivo."
                },
                new[]
                {
                    "Cada elemento tematizado suma un bonus a Creatividad (tope 10).",
                    "Los pesos de cada criterio los fija la administración en la Configuración."
                }),
            new("Guardar como Preliminar o cerrar como Final",
                null,
                new[]
                {
                    "\"Guardar preliminar\" guarda lo capturado; puedes volver y modificarlo las veces que necesites.",
                    "\"Cerrar como Final\" abre una ventana de confirmación. Al confirmar, la evaluación ya no se puede modificar y cuenta para el ranking, las constancias y los reportes."
                },
                new[]
                {
                    "Solo se puede cerrar como Final si la Ficha está completa y el equipo tiene Narrador.",
                    "Si otro juez modificó la misma evaluación mientras la tenías abierta, el sistema no guarda tus cambios y te avisa: revisa los valores actuales y vuelve a guardar."
                }),
            new("Historial, lugares y empates",
                "El Historial muestra todas las evaluaciones con su nota, lugar y estado. Los lugares (1.º a 3.º) son por programa educativo y periodo, " +
                "y solo cuentan las evaluaciones Final.",
                Avisos: new[]
                {
                    "Si dos equipos empatan en nota, desempata la Creatividad.",
                    "Si siguen empatados, comparten el lugar y aparecen marcados como \"Empate: lo resuelve el comité\"."
                }),
            new("Constancias",
                "Desde el detalle de una evaluación Final puedes descargar la constancia grupal del equipo, las individuales de los integrantes (en un ZIP), " +
                "la del maestro encargado y la tuya como juez, o enviarlas todas por correo a los participantes.")
        };

        public static readonly ManualUsuario Juez = new(Concurso, "Juez",
            "Esta guía es para el jurado del Concurso de Altares: cómo entrar, evaluar un altar paso a paso, cerrar la evaluación y obtener las constancias.",
            SeccionesJuez);

        public static readonly ManualUsuario Administrador = new(Concurso, "Administrador",
            "Esta guía es para la administración del Concurso de Altares. Incluye todo lo que hace un juez (secciones 1 a 8) y las herramientas " +
            "de administración: jueces, registrantes y equipos, configuración del periodo y reporte de cierre.",
            SeccionesJuez.Concat(new SeccionManual[]
            {
                new("Gestión de jueces",
                    "En \"Jueces\" ves todas las cuentas del jurado y la administración.",
                    new[]
                    {
                        "\"Nuevo usuario\" crea una cuenta con usuario y contraseña; puedes darle rol de Juez o de Administrador.",
                        "Las solicitudes nuevas aparecen como \"Pendiente\": presiona \"Aprobar\" para habilitarlas.",
                        "El lápiz edita los datos, el rol o la contraseña.",
                        "El ícono rojo desactiva la cuenta (pide confirmación) y el ícono verde la reactiva.",
                        "Si una cuenta aparece como \"Bloqueada\" por intentos fallidos, el candado abierto la desbloquea."
                    },
                    new[] { "No puedes desactivar tu propia cuenta." }),
                new("Registrantes y equipos",
                    "Tiene dos pestañas: Registrantes (cuentas de participantes) y Equipos (fichas de registro).",
                    new[]
                    {
                        "En Registrantes puedes editar los datos de una persona, desactivarla o reactivarla, y desbloquearla si quedó en pausa por intentos fallidos.",
                        "En Equipos ves el estado de cada Ficha. \"Validar ficha\" la marca como revisada por la administración (no rellena datos faltantes).",
                        "Editar un equipo permite corregir sus datos, el programa educativo y el maestro encargado. Si el equipo ya tiene evaluación Final y cambias su programa educativo, los lugares de ambos programas se recalculan solos.",
                        "Mientras el equipo no tenga evaluación, la administración puede agregar o quitar integrantes directamente y designar al Narrador."
                    },
                    new[] { "Agrega directamente solo a personas que aceptaron participar: los organizadores, en cambio, invitan y la persona acepta." }),
                new("Configuración del periodo",
                    null,
                    new[]
                    {
                        "Fechas límite: la de inscripción (crear equipos) y la de requisitos (editar Ficha e integrantes). La de requisitos no puede ser anterior a la de inscripción; el año debe estar entre 2000 y 2099. Vacío = sin límite.",
                        "Recorrido: sube el PDF con el mapa de altares (máximo 25 MB).",
                        "Pesos de la nota final: el porcentaje de cada criterio. Deben sumar 100%; la suma se muestra en vivo.",
                        "Umbral de privacidad del reporte: mínimo 3 personas por categoría demográfica."
                    },
                    new[] { "Los pesos se aplican a las evaluaciones que se calculen después del cambio." }),
                new("Reporte de Periodo",
                    "Muestra el resumen del periodo (participación, distribución por programa educativo, resultados de las evaluaciones Final con el podio por carrera y datos demográficos agrupados) y lo descarga en PDF. " +
                    "Las categorías con menos personas que el umbral de privacidad se agrupan como \"Otros / grupo reducido\".")
            }).ToList());

        public static ManualUsuario? PorRol(string rol) => rol switch
        {
            "Participante" => Participante,
            "Juez" => Juez,
            "Administrador" => Administrador,
            _ => null
        };
    }
}
