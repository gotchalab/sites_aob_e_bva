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
