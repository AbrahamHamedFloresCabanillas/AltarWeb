using Concurso.Core.Pdf;

namespace Catrinas.Web.Servicios
{
    // Contenido de los manuales de usuario del Concurso de Catrinas, uno por rol (peticion del propietario, 2026-10-22).
    // Describe las pantallas tal como funcionan hoy: si cambia un flujo, actualizar aqui el manual correspondiente.
    // La maquetacion del PDF es comun a las dos apps (Concurso.Core.Pdf.ManualUsuarioPdf).
    public static class ManualesCatrinas
    {
        private const string Concurso = "Concurso de Catrinas";

        private static readonly SeccionManual Acceso = new(
            "Entrar al sistema",
            "Abre la dirección del Concurso de Catrinas en tu navegador. La pantalla de inicio tiene dos pestañas: " +
            "\"Participantes\" y \"Jurado / Admin\". Es una plataforma independiente del Concurso de Altares: necesitas una cuenta aquí aunque ya tengas una allá.",
            Avisos: new[]
            {
                "Después de 5 contraseñas incorrectas seguidas, la cuenta queda en pausa 5 minutos. Si alguien la bloquea a propósito, la administración puede desbloquearla.",
                "Usa siempre tu correo institucional @uabc.edu.mx.",
                "Para salir, usa el ícono de salida junto a tu nombre."
            });

        public static readonly ManualUsuario Participante = new(Concurso, "Participante",
            "Esta guía es para quienes inscriben una Catrina: cómo crear tu cuenta, armar tu inscripción, invitar a tu equipo, " +
            "designar a quien personifica y descargar tu constancia.",
            new[]
            {
                Acceso,
                new SeccionManual("Crear tu cuenta",
                    "En la pestaña \"Participantes\" elige \"Crea tu cuenta\" o \"Continuar con Google\" con tu cuenta institucional.",
                    new[]
                    {
                        "Escribe tu nombre completo y tu Matrícula / No. de empleado: 7 dígitos si eres alumno, 5 si eres maestro o administrativo (en ese caso indica cuál).",
                        "Escribe tu correo @uabc.edu.mx y, si quieres, teléfono, género, carrera y autodescripción cultural (opcionales).",
                        "Elige tu contraseña, confírmala y registra. Entrarás directo a tu panel."
                    },
                    new[] { "Los datos de género y autodescripción solo se usan en reportes agregados; nunca se publican por persona." }),
                new SeccionManual("Crear tu inscripción",
                    "Una inscripción puede ser de una sola persona o de un equipo. Quien la crea es su responsable y es quien la edita.",
                    new[]
                    {
                        "En tu panel presiona \"Crear inscripción\".",
                        "Escribe el nombre de la inscripción y elige la carrera con la que concursa.",
                        "Si quieres, escribe la Matrícula / No. de empleado del maestro encargado (opcional).",
                        "Si tu equipo también participa en el Concurso de Altares, puedes anotar el código público de su equipo (por ejemplo ALT-2026-2-0042) y su nombre. Es solo una referencia.",
                        "Guarda."
                    },
                    new[] { "Solo puedes estar en una inscripción por periodo." }),
                new SeccionManual("Invitar integrantes",
                    "Los integrantes no se agregan solos: el responsable los invita y cada persona acepta desde su panel.",
                    new[]
                    {
                        "En \"Editar\", escribe la Matrícula / No. de empleado de la persona y presiona \"Invitar\".",
                        "La persona ve la invitación en su panel y presiona \"Aceptar\" o \"Rechazar\". Solo queda en la inscripción si acepta.",
                        "Mientras no acepte, verás la invitación como pendiente (solo con la matrícula) y puedes cancelarla."
                    },
                    new[]
                    {
                        "Por privacidad, el sistema responde siempre el mismo mensaje al invitar: no confirma si la matrícula existe ni si la persona ya está en otra inscripción.",
                        "La persona debe tener cuenta en el Concurso de Catrinas. Pídele que se registre primero."
                    }),
                new SeccionManual("Recibir una invitación",
                    "Si un responsable te invita y aún no estás en una inscripción, verás la invitación en tu panel con el nombre de la inscripción, " +
                    "la carrera y quién te invita. Presiona \"Aceptar\" para unirte o \"Rechazar\" para descartarla. Al aceptar, tus demás invitaciones del periodo se descartan."),
                new SeccionManual("Designar quién personifica a la Catrina",
                    "Cada inscripción necesita exactamente una persona que personifique a la Catrina.",
                    new[] { "En \"Editar\", junto al integrante, presiona \"Personificará\". Si eliges a otra persona, la anterior vuelve a ser integrante." },
                    new[] { "Sin persona que personifique, el jurado no puede cerrar la evaluación como Final." }),
                new SeccionManual("Editar tu inscripción",
                    "Tu panel indica qué falta para que la inscripción esté lista. Puedes editarla (datos, integrantes e invitaciones) hasta:",
                    Avisos: new[]
                    {
                        "La fecha límite de requisitos que fija la administración.",
                        "El momento en que el jurado registra su evaluación: desde ahí la inscripción queda cerrada."
                    }),
                new SeccionManual("Descargar tu constancia",
                    "Cuando el jurado cierra la evaluación como Final, en tu panel aparece \"Descargar mi constancia\" (PDF). " +
                    "Si la inscripción obtuvo 1.º, 2.º o 3.er lugar en su carrera, la constancia lo indica."),
                new SeccionManual("Problemas frecuentes",
                    Avisos: new[]
                    {
                        "\"Correo o contraseña incorrectos\": revisa que sea tu correo institucional completo y tu cuenta del Concurso de Catrinas.",
                        "\"Tu cuenta fue desactivada\": contacta a la administración del concurso.",
                        "Página de \"demasiados intentos\": espera 5 minutos o pide a la administración que desbloquee tu cuenta.",
                        "No puedo editar: ya pasó la fecha límite de requisitos o el jurado ya registró la evaluación."
                    })
            });

        private static readonly SeccionManual[] SeccionesJurado =
        {
            Acceso,
            new("Obtener tu cuenta del jurado",
                "La administración puede crearte la cuenta, o puedes solicitarla tú.",
                new[]
                {
                    "En la pestaña \"Jurado / Admin\" presiona \"Solicita acceso\" (o usa \"Continuar con Google\").",
                    "Captura tu nombre, correo @uabc.edu.mx, Matrícula / No. de empleado y contraseña.",
                    "Tu solicitud queda pendiente hasta que la administración la apruebe."
                },
                new[] { "Si te crearon la cuenta con una contraseña temporal, el sistema te pedirá cambiarla al primer ingreso." }),
            new("Menú del jurado",
                "En el menú lateral encontrarás: Inscripciones (las del periodo, con filtro por carrera), Ranking por carrera y Manuales (estas guías en PDF)."),
            new("Evaluar una Catrina",
                null,
                new[]
                {
                    "En \"Inscripciones\" filtra por carrera si lo necesitas y entra a la inscripción.",
                    "Revisa los datos de solo lectura: quién personifica, integrantes, maestro encargado y, si la hay, la referencia al equipo de Altares.",
                    "Califica de 0 a 10 los cinco criterios: Vestuario, Maquillaje, Accesorios, Originalidad y creatividad, y Apego a las tradiciones. Debajo de cada uno hay una guía de qué evaluar y su peso.",
                    "Revisa la Nota de Catrina, que se actualiza en vivo."
                },
                new[] { "Los pesos los fija la administración; una evaluación Final conserva los pesos con los que se calculó." }),
            new("Guardar como Preliminar o cerrar como Final",
                null,
                new[]
                {
                    "\"Guardar como Preliminar\" guarda lo capturado; puedes modificarlo después.",
                    "\"Cerrar como Final\" abre una ventana que muestra la nota que quedará fija. Al confirmar ya no se puede modificar y cuenta para el ranking y las constancias."
                },
                new[]
                {
                    "Solo se puede cerrar como Final si la inscripción tiene a alguien que personifique a la Catrina.",
                    "Si otro integrante del jurado modificó la evaluación mientras la tenías abierta, tus cambios no se guardan y el sistema te avisa: revisa los valores actuales."
                }),
            new("Ranking por carrera",
                "Muestra las evaluaciones Final de cada carrera ordenadas por nota. Si dos inscripciones empatan, comparten el lugar (1.º, 1.º, 3.º)."),
            new("Constancias",
                "Desde el detalle de una evaluación Final puedes descargar la constancia grupal, las individuales (ZIP), la del maestro encargado " +
                "(si hay) y la tuya como jurado, o enviarlas todas por correo a los participantes.")
        };

        public static readonly ManualUsuario Jurado = new(Concurso, "Jurado",
            "Esta guía es para el jurado del Concurso de Catrinas: cómo entrar, evaluar una personificación, cerrar la evaluación y obtener las constancias.",
            SeccionesJurado);

        public static readonly ManualUsuario Administracion = new(Concurso, "Administración",
            "Esta guía es para la administración del Concurso de Catrinas. Incluye todo lo que hace el jurado (secciones 1 a 7) y las herramientas " +
            "de administración: cuentas del jurado, participantes y configuración del periodo.",
            SeccionesJurado.Concat(new SeccionManual[]
            {
                new("Cuentas del jurado",
                    "En \"Jurado\" ves todas las cuentas del jurado y la administración.",
                    new[]
                    {
                        "\"Nueva cuenta\" crea una cuenta de jurado o de administración.",
                        "Las solicitudes nuevas aparecen como \"Pendiente\": presiona \"Aprobar\".",
                        "El ícono rojo desactiva la cuenta (pide confirmación) y el ícono verde la reactiva.",
                        "Si una cuenta aparece como \"Bloqueada\" por intentos fallidos, el candado abierto la desbloquea."
                    },
                    new[] { "No puedes modificar tu propia cuenta desde esta pantalla." }),
                new("Participantes",
                    "Lista todas las cuentas de participantes con su matrícula, correo, carrera y género.",
                    new[]
                    {
                        "El ícono rojo desactiva una cuenta y el verde la reactiva.",
                        "Si una cuenta aparece como \"Bloqueada\", el candado abierto la desbloquea."
                    }),
                new("Configuración del periodo",
                    null,
                    new[]
                    {
                        "Fechas límite: la de inscripción (crear inscripciones) y la de requisitos (editar inscripción e integrantes). La de requisitos no puede ser anterior a la de inscripción; el año debe estar entre 2000 y 2099. Vacío = sin límite.",
                        "Pesos de los cinco criterios: deben sumar 100%; la suma total se muestra en vivo.",
                        "Umbral de agrupación de la reportería: mínimo 3 personas por categoría demográfica."
                    },
                    new[] { "Los pesos se aplican a las evaluaciones que se guarden después; las ya guardadas conservan los suyos." })
            }).ToList());
    }
}
