// Selector de fecha propio de AltarWeb. Sustituye al picker nativo del navegador (que no se puede
// tematizar y cambia de forma en cada navegador) sobre cualquier <input type="date">.
//
// Decision de interaccion: elegir un dia APLICA la fecha y cierra. Es lo que ya hacia el picker
// anterior y es coherente con el resto del formulario, donde nada se guarda hasta pulsar "Guardar";
// agregar un paso "Aplicar" solo sumaria un clic. Lo que si se agrego es el feedback que faltaba:
// el modal dice que campo se esta editando y cual es el valor actual, "hoy" se marca con un punto
// ademas del color, y hay accion explicita para vaciar un campo opcional.
var AltarDatePicker = (function () {
    var MESES = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio',
                 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
    var DIAS_LARGOS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];

    var inputActivo = null;
    var disparador = null;   // elemento que tenia el foco, para devolverlo al cerrar
    var mesActual = new Date();
    var seleccionada = null;

    function $(id) { return document.getElementById(id); }
    function pad(n) { return n < 10 ? '0' + n : '' + n; }
    function aIso(f) { return f.getFullYear() + '-' + pad(f.getMonth() + 1) + '-' + pad(f.getDate()); }
    function mismoDia(a, b) { return !!a && !!b && a.toDateString() === b.toDateString(); }

    function parsear(valor) {
        if (!valor) return null;
        var f = new Date(valor + 'T00:00:00');
        return isNaN(f.getTime()) ? null : f;
    }

    function formatoLargo(f) {
        return DIAS_LARGOS[f.getDay()] + ', ' + f.getDate() + ' de ' + MESES[f.getMonth()].toLowerCase() + ' de ' + f.getFullYear();
    }

    // El <label> del .altar-field que contiene al input; asi el modal puede decir que se esta editando.
    function etiquetaDe(input) {
        if (input.dataset.altarDatepickerLabel) return input.dataset.altarDatepickerLabel;
        var campo = input.closest('.altar-field');
        var label = campo ? campo.querySelector('.altar-label, label') : null;
        var texto = label ? label.textContent.replace(/\*/g, '').trim() : '';
        return texto || 'Selecciona una fecha';
    }

    function abrir(input, origen) {
        if (input.disabled) return;

        inputActivo = input;
        disparador = origen || input;
        seleccionada = parsear(input.value);
        mesActual = new Date((seleccionada || new Date()).getFullYear(), (seleccionada || new Date()).getMonth(), 1);

        $('altar-datepicker-campo').textContent = etiquetaDe(input);
        $('altar-datepicker-limpiar').hidden = input.required || !input.value;

        render();
        $('altar-datepicker-modal').classList.add('open');
        document.body.classList.add('altar-modal-abierto');

        enfocarDiaVisible();
    }

    function cerrar() {
        $('altar-datepicker-modal').classList.remove('open');
        document.body.classList.remove('altar-modal-abierto');
        if (disparador && typeof disparador.focus === 'function') disparador.focus();
        inputActivo = null;
        disparador = null;
    }

    // render() reconstruye la cuadricula, asi que el boton que tenia el foco deja de existir y el
    // foco se caeria al <body>. Si quien navegaba estaba dentro de la cuadricula (o llego por
    // teclado), se devuelve el foco a un dia del mes nuevo.
    function cambiarMes(delta, enfocarDia) {
        var recuperar = enfocarDia || focoEnCuadricula();
        mesActual = new Date(mesActual.getFullYear(), mesActual.getMonth() + delta, 1);
        render();
        if (recuperar) enfocarDiaVisible();
    }

    function cambiarAnio(delta, enfocarDia) {
        var recuperar = enfocarDia || focoEnCuadricula();
        mesActual = new Date(mesActual.getFullYear() + delta, mesActual.getMonth(), 1);
        render();
        if (recuperar) enfocarDiaVisible();
    }

    function focoEnCuadricula() {
        var activo = document.activeElement;
        return !!(activo && activo.dataset && activo.dataset.iso);
    }

    function enfocarDiaVisible() {
        var grid = $('altar-datepicker-grid');
        var btn = grid.querySelector('.selected:not(.fuera)')
            || grid.querySelector('.today:not(.fuera)')
            || grid.querySelector('.altar-datepicker-day:not(.fuera)');
        if (btn) btn.focus();
    }

    function hoy() { aplicar(new Date()); }

    function limpiar() {
        if (!inputActivo || inputActivo.required) return;
        escribir('');
        cerrar();
    }

    function aplicar(fecha) {
        if (!inputActivo) return;
        escribir(aIso(fecha));
        cerrar();
    }

    function escribir(valor) {
        inputActivo.value = valor;
        // Los dos eventos: 'input' para listeners en vivo y 'change' para validacion unobtrusive.
        inputActivo.dispatchEvent(new Event('input', { bubbles: true }));
        inputActivo.dispatchEvent(new Event('change', { bubbles: true }));
        actualizarTextoDeApoyo(inputActivo);
    }

    // Debajo del input se escribe la fecha en palabras, para que el valor ISO del campo no sea la
    // unica confirmacion de lo que se eligio.
    function actualizarTextoDeApoyo(input) {
        var eco = input.parentElement ? input.parentElement.querySelector('[data-altar-date-eco]') : null;
        if (!eco) return;
        var f = parsear(input.value);
        eco.textContent = f ? formatoLargo(f) : 'Sin fecha límite configurada.';
    }

    function render() {
        $('altar-datepicker-titulo').textContent = MESES[mesActual.getMonth()] + ' ' + mesActual.getFullYear();
        $('altar-datepicker-actual').textContent = seleccionada
            ? 'Seleccionada: ' + formatoLargo(seleccionada)
            : 'Sin fecha seleccionada';

        var grid = $('altar-datepicker-grid');
        grid.innerHTML = '';

        var anio = mesActual.getFullYear();
        var mes = mesActual.getMonth();
        var primerDia = new Date(anio, mes, 1).getDay();
        var diasEnMes = new Date(anio, mes + 1, 0).getDate();
        var diasMesPrevio = new Date(anio, mes, 0).getDate();
        var fechaHoy = new Date();

        // Los dias de los meses vecinos se dibujan (en vez de dejar huecos) para que la cuadricula no
        // "salte" de tamano al cambiar de mes, pero con la clase .fuera para que se distingan.
        for (var i = primerDia - 1; i >= 0; i--) {
            grid.appendChild(celda(new Date(anio, mes - 1, diasMesPrevio - i), true, fechaHoy));
        }
        for (var d = 1; d <= diasEnMes; d++) {
            grid.appendChild(celda(new Date(anio, mes, d), false, fechaHoy));
        }
        var restantes = (7 - (grid.children.length % 7)) % 7;
        for (var j = 1; j <= restantes; j++) {
            grid.appendChild(celda(new Date(anio, mes + 1, j), true, fechaHoy));
        }
    }

    function celda(fecha, fuera, fechaHoy) {
        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'altar-datepicker-day' + (fuera ? ' fuera' : '');
        btn.textContent = fecha.getDate();
        btn.dataset.iso = aIso(fecha);
        btn.setAttribute('aria-label', formatoLargo(fecha));

        if (mismoDia(fecha, seleccionada)) {
            btn.classList.add('selected');
            btn.setAttribute('aria-current', 'date');
        }
        if (mismoDia(fecha, fechaHoy)) {
            btn.classList.add('today');
            btn.title = 'Hoy';
        }

        btn.addEventListener('click', function () { aplicar(fecha); });
        return btn;
    }

    // Flechas para moverse por la cuadricula; si el dia cae fuera del mes visible, se cambia de mes.
    function moverFoco(delta) {
        // Si el foco no esta en un dia (por ejemplo esta en un boton de navegacion), la primera
        // flecha simplemente entra a la cuadricula en vez de no hacer nada.
        if (!focoEnCuadricula()) {
            enfocarDiaVisible();
            return;
        }

        var destino = parsear(document.activeElement.dataset.iso);
        destino.setDate(destino.getDate() + delta);

        if (destino.getMonth() !== mesActual.getMonth() || destino.getFullYear() !== mesActual.getFullYear()) {
            mesActual = new Date(destino.getFullYear(), destino.getMonth(), 1);
            render();
        }
        var btn = $('altar-datepicker-grid').querySelector('[data-iso="' + aIso(destino) + '"]');
        if (btn) btn.focus();
    }

    function alTeclado(e) {
        if (!$('altar-datepicker-modal').classList.contains('open')) return;

        switch (e.key) {
            case 'Escape':    e.preventDefault(); cerrar(); break;
            case 'ArrowLeft': e.preventDefault(); moverFoco(-1); break;
            case 'ArrowRight':e.preventDefault(); moverFoco(1); break;
            case 'ArrowUp':   e.preventDefault(); moverFoco(-7); break;
            case 'ArrowDown': e.preventDefault(); moverFoco(7); break;
            case 'PageUp':    e.preventDefault(); cambiarMes(-1, true); break;
            case 'PageDown':  e.preventDefault(); cambiarMes(1, true); break;
            case 'Home':      e.preventDefault(); cambiarAnio(-1, true); break;
            case 'End':       e.preventDefault(); cambiarAnio(1, true); break;
        }
    }

    function inicializar() {
        var modal = $('altar-datepicker-modal');
        if (!modal) return;

        if (!modal.dataset.listo) {
            modal.dataset.listo = 'true';
            // Clic en el fondo (no en el panel) cierra.
            modal.addEventListener('mousedown', function (e) { if (e.target === modal) cerrar(); });
            document.addEventListener('keydown', alTeclado);
        }

        document.querySelectorAll('input[type="date"]').forEach(function (input) {
            if (input.dataset.altarDatepickerReady) return;
            input.dataset.altarDatepickerReady = 'true';

            // readonly suprime el picker nativo sin deshabilitar el envio del campo.
            input.setAttribute('readonly', 'readonly');
            input.setAttribute('role', 'combobox');
            input.setAttribute('aria-haspopup', 'dialog');
            if (!input.disabled) input.classList.add('altar-input-fecha');

            input.addEventListener('click', function () { abrir(input); });
            input.addEventListener('keydown', function (e) {
                // El input se puede enfocar con Tab; Enter/Espacio/flecha abajo abren el calendario.
                if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
                    e.preventDefault();
                    abrir(input);
                }
            });

            actualizarTextoDeApoyo(input);
        });
    }

    document.addEventListener('DOMContentLoaded', inicializar);

    return {
        abrir: abrir, cerrar: cerrar, cambiarMes: cambiarMes, cambiarAnio: cambiarAnio,
        hoy: hoy, limpiar: limpiar, inicializar: inicializar
    };
})();
