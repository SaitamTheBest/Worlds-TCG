(() => {
    'use strict';
    const root = document.querySelector('[data-market-list], [data-market-auction]');
    if (!root) return;
    const feedback = root.querySelector('[data-market-feedback]');
    const connectionLabel = root.querySelector('[data-market-connection]');
    let clockOffset = 0, timer, refreshRequest, busy = false;
    const notify = (message, error = false) => {
        feedback.hidden = false;
        feedback.className = `market-feedback ${error ? 'error' : 'success'}`;
        feedback.textContent = message;
        feedback.setAttribute('role', error ? 'alert' : 'status');
    };
    const updateState = () => {
        const stamp = root.querySelector('[data-server-utc]');
        if (stamp) clockOffset = Date.parse(stamp.dataset.serverUtc) - Date.now();
        const state = root.querySelector('.market-auction-state');
        if (state) {
            const bidForm = root.querySelector('[data-market-bid]');
            const cancelForm = root.querySelector('[data-market-cancel]');
            if (bidForm) {
                bidForm.querySelector('input[name=amount]').min = state.dataset.minimum;
                bidForm.querySelector('button').disabled = busy || state.dataset.active !== 'true';
            }
            if (cancelForm) cancelForm.querySelector('button').disabled = busy || state.dataset.canCancel !== 'true';
        }
        tick();
    };
    const tick = () => {
        const now = Date.now() + clockOffset;
        root.querySelectorAll('[data-market-end]').forEach(element => {
            const seconds = Math.max(0, Math.ceil((Date.parse(element.dataset.marketEnd) - now) / 1000));
            const days = Math.floor(seconds / 86400);
            const hours = Math.floor(seconds % 86400 / 3600);
            const minutes = Math.floor(seconds % 3600 / 60);
            const rest = seconds % 60;
            element.textContent = seconds === 0 ? 'Terminée' : `${days ? `${days}j ` : ''}${String(hours).padStart(2,'0')}:${String(minutes).padStart(2,'0')}:${String(rest).padStart(2,'0')}`;
            if (seconds === 0 && root.querySelector('.market-auction-state')?.dataset.active === 'true') {
                const button = root.querySelector('[data-market-bid] button');
                if (button) button.disabled = true;
            }
        });
    };
    const refresh = async () => {
        refreshRequest?.abort();
        const current = new AbortController(); refreshRequest = current;
        const target = root.querySelector(root.hasAttribute('data-market-list') ? '[data-market-listings]' : '[data-market-state]');
        const url = new URL(root.dataset.listUrl || root.dataset.stateUrl, location.origin);
        if (root.hasAttribute('data-market-list')) {
            url.searchParams.set('mode',root.dataset.mode);
            url.searchParams.set('search',root.dataset.search);
            url.searchParams.set('page',root.dataset.page);
        }
        try {
            const response = await fetch(url, {signal:current.signal});
            if (!response.ok || response.redirected) throw new Error('Actualisation impossible');
            const html = await response.text();
            if (current.signal.aborted) return;
            target.innerHTML = html;
            window.lucide?.createIcons(); updateState();
        } catch (error) {
            if (error.name !== 'AbortError') notify('Le marché ne peut pas être actualisé. Vérifiez votre connexion ou rechargez la page.', true);
        }
    };
    const scheduleRefresh = () => { clearTimeout(timer); timer = setTimeout(refresh, 250); };
    root.querySelectorAll('[data-market-bid], [data-market-cancel]').forEach(form => {
        form.addEventListener('submit',async event => {
            event.preventDefault(); if (busy) return;
            if (form.hasAttribute('data-market-cancel') && !await window.worldsConfirmation.ask('Annuler cette vente et récupérer la carte ?', { title: 'Annuler la vente', confirmText: 'Annuler la vente', cancelText: 'Garder la vente' })) return;
            const input = form.querySelector('input[name=amount]');
            const amount = input ? Number(input.value) : null;
            if (input && (!Number.isFinite(amount) || amount <= 0 || !input.checkValidity())) { input.reportValidity(); return; }
            busy = true; form.querySelector('button').disabled = true;
            try {
                const response = await fetch(form.action, {
                    method:'POST',
                    headers: {'Content-Type':'application/json','RequestVerificationToken':form.querySelector('input[name=__RequestVerificationToken]').value},
                    body: input ? JSON.stringify({amount}) : undefined
                });
                const data = await response.json().catch(() => ({}));
                if (!response.ok || response.redirected) throw new Error(data.message || 'L’action a échoué. Rechargez la page puis réessayez.');
                notify(data.message); if (input) input.value='';
            } catch (error) { notify(error.message || 'Connexion indisponible. Vérifiez si l’action a été enregistrée avant de réessayer.',true); }
            finally { busy=false; await refresh(); }
        });
    });
    updateState(); setInterval(tick,1000);
    // Reconciliation covers missed messages, server restarts and temporary disconnections.
    setInterval(refresh,15000);
    if (!window.signalR) { connectionLabel.textContent='Actualisation toutes les 15 secondes'; return; }
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/market').withAutomaticReconnect().build();
    const connected = () => { connectionLabel.textContent='● En direct'; connectionLabel.classList.add('connected'); refresh(); };
    connection.on('MarketChanged', event => {
        if (!root.dataset.marketAuction || event.auctionId === root.dataset.marketAuction) scheduleRefresh();
    });
    connection.onreconnecting(() => { connectionLabel.textContent='Reconnexion…'; connectionLabel.classList.remove('connected'); });
    connection.onreconnected(connected);
    const start = async () => {
        try { await connection.start(); connected(); }
        catch { connectionLabel.textContent='Connexion interrompue · nouvelle tentative…'; connectionLabel.classList.remove('connected'); setTimeout(start,5000); }
    };
    connection.onclose(() => { connectionLabel.textContent='Connexion interrompue'; connectionLabel.classList.remove('connected'); setTimeout(start,5000); });
    start();
})();
