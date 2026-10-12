(() => {
    'use strict';
    const dialog = document.getElementById('popupConfirmation');
    const title = document.getElementById('popupConfirmationTitle');
    const message = document.getElementById('popupConfirmationMessage');
    const cancel = document.getElementById('popupConfirmationCancel');
    const accept = document.getElementById('popupConfirmationAccept');
    let pending = null, previousFocus = null;
    const approvedForms = new WeakSet(), waitingForms = new WeakSet();

    function finish(accepted) {
        if (!pending) return;
        const resolve = pending; pending = null;
        if (dialog.open) dialog.close();
        if (previousFocus?.isConnected) previousFocus.focus();
        resolve(accepted);
    }
    function ask(text, options = {}) {
        if (!dialog || pending) return Promise.resolve(false);
        title.textContent = options.title || 'Confirmer l’action';
        message.textContent = text;
        accept.textContent = options.confirmText || 'Confirmer';
        cancel.textContent = options.cancelText || 'Annuler';
        previousFocus = document.activeElement;
        return new Promise(resolve => {
            pending = resolve;
            try { dialog.showModal(); cancel.focus(); }
            catch { finish(false); }
        });
    }
    function confirmForm(event, text, options = {}) {
        const form = event.target;
        if (approvedForms.has(form)) { approvedForms.delete(form); return true; }
        event.preventDefault();
        event.stopImmediatePropagation?.();
        if (waitingForms.has(form)) return false;
        waitingForms.add(form);
        const submitter = event.submitter;
        ask(text, options).then(accepted => {
            waitingForms.delete(form);
            if (!accepted || !form.isConnected) return;
            approvedForms.add(form);
            try { form.requestSubmit(submitter || undefined); }
            finally { approvedForms.delete(form); }
        });
        return false;
    }
    window.worldsConfirmation = { ask, confirmForm, get isOpen() { return !!pending; } };
    cancel?.addEventListener('click', () => finish(false));
    accept?.addEventListener('click', () => finish(true));
    dialog?.addEventListener('cancel', event => { event.preventDefault(); finish(false); });
    dialog?.addEventListener('close', () => { if (!dialog.open) finish(false); });
    dialog?.addEventListener('click', event => { if (event.target === dialog) finish(false); });
    // Keep game and deck keyboard shortcuts from handling the popup's keys.
    dialog?.addEventListener('keydown', event => { event.stopPropagation(); });
    document.addEventListener('submit', event => {
        if (event.target.matches('[data-popup-confirm]'))
            confirmForm(event, event.target.dataset.popupConfirm, { title: event.target.dataset.confirmTitle, confirmText: event.target.dataset.confirmText });
    }, true);
})();
