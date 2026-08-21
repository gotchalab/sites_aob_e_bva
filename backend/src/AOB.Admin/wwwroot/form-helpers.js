window.aobFormHelpers = {
  scrollToFirstInvalid: () => {
    const el = document.querySelector('.is-invalid, [data-field-error="1"]');
    if (!el) return;
    el.scrollIntoView({ behavior: 'smooth', block: 'center' });
    if (typeof el.focus === 'function') {
      try { el.focus({ preventScroll: true }); } catch { /* noop */ }
    }
  }
};

// Fecha a sidebar mobile ao clicar num link do menu (a nav é offcanvas
// controlada por checkbox #admin-sidebar-toggle — este listener limpa o
// estado após navegação SPA-like do Blazor).
(function () {
  document.addEventListener('click', function (ev) {
    var t = ev.target;
    if (!t) return;
    var link = t.closest && t.closest('.admin-nav a');
    if (!link) return;
    var cb = document.getElementById('admin-sidebar-toggle');
    if (cb && cb.checked) cb.checked = false;
  }, true);
})();
