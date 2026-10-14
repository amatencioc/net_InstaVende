// ── Layout autenticado: barra lateral y estado del canal ───────────────────
(function () {
  'use strict';

  var sidebar = document.getElementById('appSidebar');
  if (!sidebar) return;

  var overlay = document.getElementById('sbOverlay');
  var sbToggle = document.getElementById('sbToggle');
  var collapseBtn = document.getElementById('sbCollapseBtn');
  var collapseIcon = document.getElementById('sbCollapseIcon');
  var COLLAPSED_KEY = 'sb_collapsed';

  function applySidebarState(collapsed) {
    sidebar.classList.toggle('collapsed', collapsed);
    document.body.classList.toggle('sb-collapsed', collapsed);
    if (collapseIcon) collapseIcon.className = collapsed ? 'bi bi-layout-sidebar' : 'bi bi-layout-sidebar-reverse';
    if (collapseBtn) collapseBtn.setAttribute('aria-expanded', String(!collapsed));
  }

  function readCollapsed() {
    try { return localStorage.getItem(COLLAPSED_KEY) === '1'; } catch (e) { return false; }
  }

  applySidebarState(readCollapsed());

  if (collapseBtn) {
    collapseBtn.addEventListener('click', function () {
      var collapsed = !sidebar.classList.contains('collapsed');
      applySidebarState(collapsed);
      try { localStorage.setItem(COLLAPSED_KEY, collapsed ? '1' : '0'); } catch (e) { /* almacenamiento no disponible */ }
    });
  }

  function setMobileOpen(open) {
    sidebar.classList.toggle('open', open);
    if (overlay) overlay.classList.toggle('open', open);
    if (sbToggle) sbToggle.setAttribute('aria-expanded', String(open));
  }

  if (sbToggle) sbToggle.addEventListener('click', function () { setMobileOpen(!sidebar.classList.contains('open')); });
  if (overlay) overlay.addEventListener('click', function () { setMobileOpen(false); });

  // Badge "!" en Canales cuando WhatsApp no está conectado
  IV.getJson('/ChannelConfig/IsConnected').then(function (res) {
    var connected = res && res.data && res.data.connected;
    var badge = document.getElementById('sbChannelBadge');
    if (badge) badge.style.display = connected ? 'none' : '';
  }).catch(function () { /* silencioso: es solo un indicador */ });
})();
