(function () {
    'use strict';
    const byId = id => document.getElementById(id);
    const home = !!byId('worldsDeckSelect');
    const pvpSelect = byId(home ? 'worldsDeckSelect' : 'worldsVsPvpDeckSelect');
    const iaSelect = home ? pvpSelect : byId('worldsVsIaDeckSelect');
    const join = byId(home ? 'worldsPlayPvpButton' : 'worldsVsPvpJoinButton');
    const play = byId(home ? 'worldsPlayAiButton' : 'worldsVsIaPlayButton');
    const cancel = byId('worldsVsPvpCancelButton');
    const pvpStatus = byId(home ? 'worldsPlayStatus' : 'worldsVsPvpStatus');
    const iaStatus = home ? pvpStatus : byId('worldsVsIaStatus');
    const pvpSection = byId('worldsVsPvpQueueSection');
    const iaSection = byId('worldsVsIaQueueSection');
    const playersInGame = byId('matchmakingPlayersInGame');
    const playersInQueue = byId('matchmakingPlayersInQueue');
    let activityTimer, activityLoading = false, activityStopped = false;
    async function refreshActivity() {
        if (activityLoading || activityStopped || !playersInGame || !playersInQueue) return;
        window.clearTimeout(activityTimer);
        activityLoading = true;
        try {
            const response = await fetch('/Pvp/Activity', { cache: 'no-store' });
            if (!response.ok || response.redirected) throw new Error('Activité indisponible');
            const data = await response.json();
            playersInGame.textContent = data.playersInGame;
            playersInQueue.textContent = data.playersInQueue;
        } catch {
            playersInGame.textContent = '—'; playersInQueue.textContent = '—';
        } finally {
            activityLoading = false;
            if (!activityStopped) activityTimer = window.setTimeout(refreshActivity, 5000);
        }
    }
    refreshActivity();
    if (!pvpSelect || !iaSelect || !join || !play) return;
    const originalLabel = join.innerHTML;
    let connection, connecting, mode = 'idle';
    function status(element, message, error = false) {
        element.textContent = message;
        element.setAttribute('role', error ? 'alert' : 'status');
        element.classList.toggle(home ? 'worlds-play-status-error' : 'worlds-pvp-status-error', error);
    }
    function update() {
        join.disabled = !pvpSelect.value || (mode !== 'idle' && !(home && mode === 'waiting'));
        play.disabled = !iaSelect.value || mode !== 'idle';
        pvpSelect.disabled = mode !== 'idle';
        iaSelect.disabled = mode !== 'idle';
        if (cancel) cancel.disabled = mode !== 'waiting';
        if (pvpSection) pvpSection.hidden = !['joining', 'waiting', 'matched'].includes(mode);
        if (iaSection) iaSection.hidden = mode !== 'ai';
        if (home) join.innerHTML = mode === 'waiting' ? 'Annuler la recherche' : originalLabel;
        if (window.lucide) window.lucide.createIcons();
    }
    function message(error) {
        const text = error?.message || String(error) || 'La demande a échoué.';
        const marker = text.indexOf('HubException:');
        return marker >= 0 ? text.slice(marker + 'HubException:'.length).trim() : text;
    }
    function reset(messageText = '', isError = false) { mode = 'idle'; update(); status(pvpStatus, messageText, isError); }
    async function ensureConnection() {
        if (connection?.state === signalR.HubConnectionState.Connected) return connection;
        if (connecting) return connecting;
        if (!connection) {
            connection = new signalR.HubConnectionBuilder().withUrl('/hubs/lobby').withAutomaticReconnect([0, 1000, 5000, 10000]).build();
            connection.on('QueueStatusUpdated', value => {
                if (value === 'waiting' && mode === 'joining') { mode = 'waiting'; update(); status(pvpStatus, 'Recherche d’un adversaire en cours…'); }
            });
            connection.on('MatchFound', matchId => {
                mode = 'matched'; update(); status(pvpStatus, 'Adversaire trouvé ! Lancement de la partie…');
                window.location.href = `/Game/${matchId}`;
            });
            connection.on('QueueError', error => reset(error, true));
            connection.onreconnecting(() => { mode = 'reconnecting'; update(); status(pvpStatus, 'Connexion interrompue. Reconnexion en cours…'); });
            connection.onreconnected(() => reset('Connexion rétablie. Vous pouvez relancer la recherche.'));
            connection.onclose(() => { if (mode !== 'matched') reset('Connexion fermée. Vous pouvez réessayer.', true); });
        }
        connecting = connection.start().then(() => connection).finally(() => { connecting = null; });
        return connecting;
    }
    async function leave() {
        if (mode !== 'waiting') return;
        mode = 'leaving'; update();
        try { await connection.invoke('LeaveQueue'); reset('Recherche annulée.'); refreshActivity(); }
        catch (error) { mode = 'waiting'; update(); status(pvpStatus, message(error), true); }
    }
    async function search() {
        if (home && mode === 'waiting') { await leave(); return; }
        if (mode !== 'idle') return;
        if (!pvpSelect.value) { status(pvpStatus, 'Veuillez sélectionner un deck.', true); return; }
        mode = 'joining'; update(); status(pvpStatus, 'Connexion à la recherche…');
        try {
            const hub = await ensureConnection();
            await hub.invoke('JoinQueue', pvpSelect.value);
            refreshActivity();
        } catch (error) { reset(message(error), true); }
    }
    async function playAi() {
        if (mode !== 'idle') return;
        if (!iaSelect.value) { status(iaStatus, 'Veuillez sélectionner un deck.', true); return; }
        mode = 'ai'; update(); status(iaStatus, 'Lancement de la partie contre l’IA…');
        try {
            const response = await fetch('/Pvp/PlayVsIA', {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
                body: JSON.stringify({ deckId: iaSelect.value })
            });
            if (response.redirected) throw new Error('Votre session a expiré. Reconnectez-vous.');
            if (!response.ok) throw new Error(await response.text() || 'Impossible de lancer la partie.');
            const result = await response.json();
            if (!result.matchId) throw new Error('La partie n’a pas pu être créée.');
            mode = 'matched'; update(); window.location.href = `/Game/${result.matchId}`;
        } catch (error) { mode = 'idle'; update(); status(iaStatus, message(error), true); }
    }
    join.addEventListener('click', search);
    cancel?.addEventListener('click', leave);
    play.addEventListener('click', playAi);
    document.addEventListener('worlds:decks-updated', async () => {
        if (mode !== 'idle') return;
        try {
            const response = await fetch('/Deck/MyDecks');
            if (!response.ok || response.redirected) throw new Error('Impossible d’actualiser les decks.');
            const decks = await response.json();
            for (const select of new Set([pvpSelect, iaSelect])) {
                const selected = select.value;
                select.replaceChildren();
                for (const deck of decks) {
                    const option = document.createElement('option'); option.value = deck.id; option.textContent = deck.name; select.appendChild(option);
                }
                if (decks.some(deck => deck.id === selected)) select.value = selected;
            }
            update();
        } catch (error) { status(pvpStatus, message(error), true); }
    });
    window.addEventListener('pagehide', () => { activityStopped = true; window.clearTimeout(activityTimer); if (connection) connection.stop().catch(() => {}); });
    update();
})();
