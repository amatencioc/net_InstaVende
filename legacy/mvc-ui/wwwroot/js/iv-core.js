// ── InstaVende core helpers ────────────────────────────────────────────────
// Se carga en _Layout después de Bootstrap y antes de los scripts de página.
// Expone window.IV y mantiene window.ivConfirm / window.ivAlert por compatibilidad.
//
// Contrato JSON del servidor (TenantController):
//   éxito → HTTP 2xx  { ok: true,  data: ... }
//   error → HTTP 4xx/5xx { ok: false, error: "mensaje para el usuario" }
(function (window, document) {
  'use strict';

  // ── Antiforgery ──────────────────────────────────────────────────────────
  function token() {
    var meta = document.querySelector('meta[name="request-verification-token"]');
    if (meta) return meta.getAttribute('content');
    var input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
  }

  // ── HTTP ─────────────────────────────────────────────────────────────────
  // Lanza IV.HttpError con el mensaje del servidor cuando la respuesta no es 2xx
  // o cuando el cuerpo trae { ok: false }.
  function HttpError(message, status, body) {
    this.name = 'HttpError';
    this.message = message;
    this.status = status;
    this.body = body;
  }
  HttpError.prototype = Object.create(Error.prototype);

  function parse(response) {
    var isJson = (response.headers.get('content-type') || '').indexOf('application/json') !== -1;
    var bodyPromise = isJson ? response.json() : Promise.resolve(null);
    return bodyPromise.then(function (body) {
      if (!response.ok || (body && body.ok === false)) {
        var msg = (body && body.error) ||
          (response.status === 401 || response.status === 403 ? 'No tienes permiso para esta acción.' :
           response.status === 404 ? 'El recurso no existe o fue eliminado.' :
           'Ocurrió un error inesperado. Intenta de nuevo.');
        throw new HttpError(msg, response.status, body);
      }
      return body;
    });
  }

  function request(url, init) {
    init = init || {};
    init.headers = Object.assign({ 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest' }, init.headers || {});
    init.credentials = 'same-origin';
    return fetch(url, init).then(parse, function () {
      throw new HttpError('Error de conexión. Revisa tu internet e intenta de nuevo.', 0, null);
    });
  }

  function getJson(url, params) {
    if (params) {
      var qs = new URLSearchParams();
      Object.keys(params).forEach(function (k) {
        if (params[k] !== null && params[k] !== undefined && params[k] !== '') qs.append(k, params[k]);
      });
      var s = qs.toString();
      if (s) url += (url.indexOf('?') === -1 ? '?' : '&') + s;
    }
    return request(url, { method: 'GET', cache: 'no-store' });
  }

  function postJson(url, data) {
    return request(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data === undefined ? {} : data)
    });
  }

  function postForm(url, formData) {
    return request(url, { method: 'POST', body: formData });
  }

  // ── Escape ───────────────────────────────────────────────────────────────
  var ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;', '`': '&#96;' };
  function escapeHtml(value) {
    return String(value === null || value === undefined ? '' : value).replace(/[&<>"'`]/g, function (c) { return ESC[c]; });
  }

  // Solo permite URLs relativas, http(s), mailto y tel en atributos href/src.
  function safeUrl(value) {
    var s = String(value || '').trim();
    if (!s) return '';
    if (/^(https?:|mailto:|tel:)/i.test(s) || /^\/(?!\/)/.test(s)) return s;
    return '';
  }

  // ── Formato ──────────────────────────────────────────────────────────────
  var currencyFormatter = null;
  function money(n) {
    if (!currencyFormatter) {
      var symbol = document.body ? (document.body.getAttribute('data-currency-symbol') || 'S/') : 'S/';
      currencyFormatter = {
        symbol: symbol,
        fmt: new Intl.NumberFormat('es-PE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      };
    }
    return currencyFormatter.symbol + ' ' + currencyFormatter.fmt.format(Number(n) || 0);
  }

  // Fechas del servidor llegan en ISO-8601 UTC ("...Z"); se muestran en hora local del navegador.
  function dateTime(iso, opts) {
    if (!iso) return '';
    var d = new Date(iso);
    if (isNaN(d)) return '';
    return d.toLocaleString('es-PE', opts || { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' });
  }

  // Valor para <input type="datetime-local"> en hora local (no UTC).
  function toLocalInput(iso) {
    var d = iso ? new Date(iso) : new Date();
    if (isNaN(d)) return '';
    var pad = function (n) { return String(n).padStart(2, '0'); };
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes());
  }

  function debounce(fn, ms) {
    var t = null;
    return function () {
      var args = arguments, self = this;
      clearTimeout(t);
      t = setTimeout(function () { fn.apply(self, args); }, ms || 300);
    };
  }

  // ── UI ───────────────────────────────────────────────────────────────────
  function modal(idOrEl) {
    var el = typeof idOrEl === 'string' ? document.getElementById(idOrEl) : idOrEl;
    return el ? window.bootstrap.Modal.getOrCreateInstance(el) : null;
  }

  var TOAST_ICONS = { success: 'bi-check-circle-fill', error: 'bi-exclamation-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
  function toast(message, type) {
    type = type === 'danger' ? 'error' : (type || 'success');
    var host = document.getElementById('ivToastHost');
    if (!host) {
      host = document.createElement('div');
      host.id = 'ivToastHost';
      host.className = 'iv-toast-host';
      host.setAttribute('aria-live', 'polite');
      document.body.appendChild(host);
    }
    var el = document.createElement('div');
    el.className = 'iv-toast iv-toast-' + type;
    el.setAttribute('role', type === 'error' ? 'alert' : 'status');
    var icon = document.createElement('i');
    icon.className = 'bi ' + (TOAST_ICONS[type] || TOAST_ICONS.info);
    var text = document.createElement('span');
    text.textContent = message;
    el.appendChild(icon);
    el.appendChild(text);
    host.appendChild(el);
    setTimeout(function () { el.classList.add('hide'); setTimeout(function () { el.remove(); }, 300); }, 3500);
  }

  // Muestra el error de una promesa rechazada como toast.
  function handleError(err) {
    toast(err && err.message ? err.message : 'Ocurrió un error inesperado.', 'error');
    if (err && !(err instanceof HttpError) && window.console) console.error(err);
  }

  // Deshabilita un botón mientras la promesa está pendiente.
  function busy(button, promise) {
    if (!button) return promise;
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    return promise.finally(function () { button.disabled = false; button.removeAttribute('aria-busy'); });
  }

  // ── Confirm / alert modal (markup en _Layout) ───────────────────────────
  var confirmApi = (function () {
    var backdrop = document.getElementById('ivConfirmBackdrop');
    if (!backdrop) {
      return {
        confirm: function (opts) { return Promise.resolve(window.confirm(typeof opts === 'string' ? opts : (opts.message || opts.title || '¿Confirmar?'))); },
        alert: function (message) { window.alert(message); return Promise.resolve(true); }
      };
    }
    var iconWrap = document.getElementById('ivConfirmIcon');
    var iconEl = document.getElementById('ivConfirmIconI');
    var titleEl = document.getElementById('ivConfirmTitle');
    var msgEl = document.getElementById('ivConfirmMessage');
    var okBtn = document.getElementById('ivConfirmOk');
    var cancelBtn = document.getElementById('ivConfirmCancel');
    var pending = null;
    var icons = { danger: 'bi-exclamation-triangle-fill', warning: 'bi-exclamation-circle-fill', info: 'bi-info-circle-fill' };
    var okColors = { danger: 'btn-danger', warning: 'btn-warning', info: 'btn-primary' };

    function open(opts) {
      var type = opts.type || 'danger';
      titleEl.textContent = opts.title || '¿Estás seguro?';
      msgEl.textContent = opts.message || '';
      iconWrap.className = 'iv-confirm-icon ' + type;
      iconEl.className = 'bi ' + (icons[type] || icons.danger);
      okBtn.textContent = opts.okLabel || 'Confirmar';
      okBtn.className = 'btn ' + (okColors[type] || 'btn-danger');
      cancelBtn.style.display = opts.alertOnly ? 'none' : '';
      backdrop.classList.add('show');
      setTimeout(function () { okBtn.focus(); }, 50);
    }
    function settle(result) {
      backdrop.classList.remove('show');
      if (pending) { var r = pending; pending = null; r(result); }
    }
    okBtn.addEventListener('click', function () { settle(true); });
    cancelBtn.addEventListener('click', function () { settle(false); });
    backdrop.addEventListener('click', function (e) { if (e.target === backdrop) settle(false); });
    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && backdrop.classList.contains('show')) settle(false);
    });

    return {
      confirm: function (opts) {
        if (pending) settle(false);
        return new Promise(function (resolve) { pending = resolve; open(typeof opts === 'string' ? { message: opts } : opts); });
      },
      alert: function (message, type) {
        if (pending) settle(false);
        return new Promise(function (resolve) { pending = resolve; open({ message: message, type: type || 'info', okLabel: 'Aceptar', alertOnly: true }); });
      }
    };
  })();

  window.IV = {
    token: token,
    getJson: getJson,
    postJson: postJson,
    postForm: postForm,
    HttpError: HttpError,
    escapeHtml: escapeHtml,
    safeUrl: safeUrl,
    money: money,
    dateTime: dateTime,
    toLocalInput: toLocalInput,
    debounce: debounce,
    modal: modal,
    toast: toast,
    handleError: handleError,
    busy: busy,
    confirm: confirmApi.confirm,
    alert: confirmApi.alert
  };

  // Compatibilidad con vistas existentes
  window.ivConfirm = confirmApi.confirm;
  window.ivAlert = confirmApi.alert;
})(window, document);
