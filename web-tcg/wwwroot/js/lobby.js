(function () {
    'use strict';

    const deckSelect = document.getElementById('worldsDeckSelect');
    const pvpButton = document.getElementById('worldsPlayPvpButton');
    const aiButton = document.getElementById('worldsPlayAiButton');
    const statusEl = document.getElementById('worldsPlayStatus');

    if (!deckSelect || !pvpButton || !aiButton) {
        return;
    }

    function getAntiForgeryToken() {
        const token = document.querySelector(
            'input[name="__RequestVerificationToken"]'
        );
        return token ? token.value : '';
    }

    function setStatus(message, isError) {
        if (!statusEl) return;
        statusEl.textContent = message || '';
        statusEl.classList.toggle('worlds-play-status-error', !!isError);
    }

    function setButtonsDisabled(disabled) {
        pvpButton.disabled = disabled;
        aiButton.disabled = disabled;
        deckSelect.disabled = disabled;
    }

    let connection = null;
    let inQueue = false;

    async function ensureConnection() {
        if (connection) return connection;

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/lobby')
            .withAutomaticReconnect()
            .build();

        connection.on('QueueStatusUpdated', (status) => {
            if (status === 'waiting') {
                setStatus(
                    'Recherche d\'un adversaire en cours...',
                    false);
            }
        });

        connection.on('MatchFound', (matchId) => {
            setStatus('Adversaire trouvé ! Lancement de la partie...', false);
            window.location.href = `/Game/${matchId}`;
        });

        connection.onclose(() => {
            inQueue = false;
            setButtonsDisabled(false);
            pvpButton.textContent = '';
        });

        await connection.start();

        return connection;
    }

    async function joinPvpQueue() {
        const deckId = deckSelect.value;

        if (!deckId) {
            setStatus('Veuillez sélectionner un deck.', true);
            return;
        }

        setStatus('');
        setButtonsDisabled(true);

        try {
            const hub = await ensureConnection();

            setStatus('Recherche d\'un adversaire en cours...', false);
            inQueue = true;

            await hub.invoke('JoinQueue', deckId);

        } catch (err) {
            setStatus(
                'Erreur lors de la recherche de partie.',
                true);
            setButtonsDisabled(false);
            inQueue = false;
        }
    }

    async function leaveQueueIfNeeded() {
        if (connection && inQueue) {
            try {
                await connection.invoke('LeaveQueue');
            } catch (err) {
                // Ignoré : la déconnexion gérera le nettoyage côté serveur.
            }
            inQueue = false;
        }
    }

    async function playAgainstAi() {
        const deckId = deckSelect.value;

        if (!deckId) {
            setStatus('Veuillez sélectionner un deck.', true);
            return;
        }

        setStatus('Lancement de la partie contre l\'IA...', false);
        setButtonsDisabled(true);

        await leaveQueueIfNeeded();

        try {
            const response = await fetch('/Home/PlayAi', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: `deckId=${encodeURIComponent(deckId)}`,
                redirect: 'follow'
            });

            if (response.redirected) {
                window.location.href = response.url;
                return;
            }

            if (!response.ok) {
                const text = await response.text();
                setStatus(
                    text || 'Impossible de lancer la partie contre l\'IA.',
                    true);
                setButtonsDisabled(false);
                return;
            }

            window.location.reload();

        } catch (err) {
            setStatus(
                'Erreur lors du lancement de la partie contre l\'IA.',
                true);
            setButtonsDisabled(false);
        }
    }

    pvpButton.addEventListener('click', () => {
        if (inQueue) {
            leaveQueueIfNeeded().then(() => {
                setStatus('');
                setButtonsDisabled(false);
            });
            return;
        }
        joinPvpQueue();
    });

    aiButton.addEventListener('click', playAgainstAi);

    window.addEventListener('beforeunload', () => {
        leaveQueueIfNeeded();
    });

})();
