(function () {
    'use strict';

    function getAntiForgeryToken() {
        const token = document.querySelector(
            'input[name="__RequestVerificationToken"]'
        );
        return token ? token.value : '';
    }

    const deckList = document.getElementById('worldsDeckList');
    const createButton = document.getElementById('worldsCreateDeckButton');

    const modal = document.getElementById('worldsDeckModal');
    const modalBackdrop = document.getElementById('worldsDeckModalBackdrop');
    const modalClose = document.getElementById('worldsDeckModalClose');
    const modalTitle = document.getElementById('worldsDeckModalTitle');

    const deckIdInput = document.getElementById('worldsDeckId');
    const deckNameInput = document.getElementById('worldsDeckName');
    const leaderSelect = document.getElementById('worldsDeckLeaderSelect');
    const cardGrid = document.getElementById('worldsDeckCardGrid');
    const countLabel = document.getElementById('worldsDeckCountLabel');
    const errorLabel = document.getElementById('worldsDeckError');

    const saveButton = document.getElementById('worldsDeckSaveButton');
    const deleteButton = document.getElementById('worldsDeckDeleteButton');

    if (!deckList || !modal) {
        return;
    }

    let builderData = null;
    let selections = new Map();

    function showError(message) {
        errorLabel.textContent = message || '';
    }

    function updateCountLabel() {
        let total = 0;
        selections.forEach(qty => total += qty);
        countLabel.textContent = `${total} / 40 cartes`;
    }

    function openModal() {
        modal.hidden = false;
        if (window.lucide) window.lucide.createIcons();
    }

    function closeModal() {
        modal.hidden = true;
    }

    async function loadDecks() {
        deckList.innerHTML =
            '<p class="worlds-deck-empty">Chargement de vos decks...</p>';

        try {
            const response = await fetch('/Deck/MyDecks');

            if (!response.ok) {
                throw new Error('Impossible de charger les decks.');
            }

            const decks = await response.json();

            renderDeckList(decks);

        } catch (err) {
            deckList.innerHTML =
                '<p class="worlds-deck-empty">Erreur lors du chargement des decks.</p>';
        }
    }

    function renderDeckList(decks) {
        if (!decks || decks.length === 0) {
            deckList.innerHTML =
                '<p class="worlds-deck-empty">Vous n\'avez encore aucun deck. Créez-en un !</p>';
            return;
        }

        deckList.innerHTML = '';

        decks.forEach(deck => {
            const card = document.createElement('div');
            card.className = 'worlds-deck-card';

            card.innerHTML = `
                <div class="worlds-deck-card-main">
                    <strong>${escapeHtml(deck.name)}</strong>
                    <small>Leader : ${escapeHtml(deck.leaderName)}</small>
                </div>
                <div class="worlds-deck-card-meta">
                    <span class="${deck.isComplete ? 'worlds-deck-ok' : 'worlds-deck-warning'}">
                        ${deck.totalCards} / 40 cartes
                    </span>
                    <button type="button" class="worlds-btn worlds-btn-ghost worlds-deck-edit" data-id="${deck.id}">
                        Modifier
                    </button>
                </div>
            `;

            card.querySelector('.worlds-deck-edit')
                .addEventListener('click', () => editDeck(deck.id));

            deckList.appendChild(card);
        });
    }

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML;
    }

    async function ensureBuilderData() {
        if (builderData) return builderData;

        const response = await fetch('/Deck/BuilderData');

        if (!response.ok) {
            throw new Error('Impossible de charger les cartes.');
        }

        builderData = await response.json();
        return builderData;
    }

    function populateLeaderSelect() {
        leaderSelect.innerHTML =
            '<option value="">Sélectionnez un Leader</option>';

        builderData.leaders.forEach(leader => {
            const option = document.createElement('option');
            option.value = leader.cardId;
            option.textContent = `${leader.name} (x${leader.ownedQuantity})`;
            leaderSelect.appendChild(option);
        });
    }

    function renderCardGrid() {
        cardGrid.innerHTML = '';

        const grouped = new Map();

        builderData.playableCards.forEach(card => {
            if (!grouped.has(card.gameplayCardId)) {
                grouped.set(card.gameplayCardId, []);
            }
            grouped.get(card.gameplayCardId).push(card);
        });

        grouped.forEach(variants => {
            const primary = variants[0];
            const totalOwned = variants.reduce(
                (sum, v) => sum + v.ownedQuantity, 0);

            const currentQty = variants.reduce(
                (sum, v) => sum + (selections.get(v.cardId) || 0), 0);

            const item = document.createElement('div');
            item.className = 'worlds-deck-grid-item';

            item.innerHTML = `
                <div class="worlds-deck-grid-item-info">
                    <strong>${escapeHtml(primary.name)}</strong>
                    <small>Coût ${primary.usageCost} — possédé x${totalOwned}</small>
                </div>
                <div class="worlds-deck-grid-item-controls">
                    <button type="button" class="worlds-qty-btn worlds-qty-minus">-</button>
                    <span class="worlds-qty-value">${currentQty}</span>
                    <button type="button" class="worlds-qty-btn worlds-qty-plus">+</button>
                </div>
            `;

            const qtyValue = item.querySelector('.worlds-qty-value');

            item.querySelector('.worlds-qty-minus')
                .addEventListener('click', () => {
                    adjustQuantity(primary.cardId, variants, -1, qtyValue);
                });

            item.querySelector('.worlds-qty-plus')
                .addEventListener('click', () => {
                    adjustQuantity(primary.cardId, variants, 1, qtyValue);
                });

            cardGrid.appendChild(item);
        });

        updateCountLabel();
    }

    function adjustQuantity(primaryCardId, variants, delta, qtyValueEl) {
        const totalOwned = variants.reduce(
            (sum, v) => sum + v.ownedQuantity, 0);

        const currentQty = variants.reduce(
            (sum, v) => sum + (selections.get(v.cardId) || 0), 0);

        let totalSelected = 0;
        selections.forEach(qty => totalSelected += qty);

        const newQty = currentQty + delta;

        if (newQty < 0) return;
        if (newQty > 4) return;
        if (newQty > totalOwned) return;
        if (delta > 0 && totalSelected >= 40) return;

        if (newQty === 0) {
            selections.delete(primaryCardId);
        } else {
            selections.set(primaryCardId, newQty);
        }

        qtyValueEl.textContent = newQty;
        updateCountLabel();
        showError('');
    }

    async function openCreateModal() {
        showError('');
        deckIdInput.value = '';
        deckNameInput.value = '';
        selections = new Map();
        modalTitle.textContent = 'Nouveau deck';
        deleteButton.hidden = true;

        try {
            await ensureBuilderData();
            populateLeaderSelect();
            renderCardGrid();
            openModal();
        } catch (err) {
            showError('Impossible de charger les cartes de votre collection.');
        }
    }

    async function editDeck(deckId) {
        showError('');

        try {
            await ensureBuilderData();

            const response = await fetch(`/Deck/${deckId}`);

            if (!response.ok) {
                throw new Error('Deck introuvable.');
            }

            const deck = await response.json();

            deckIdInput.value = deck.id;
            deckNameInput.value = deck.name;
            modalTitle.textContent = `Modifier : ${deck.name}`;
            deleteButton.hidden = false;

            selections = new Map();
            deck.cards.forEach(c => selections.set(c.cardId, c.quantity));

            populateLeaderSelect();
            leaderSelect.value = deck.leaderCardId;
            renderCardGrid();

            openModal();

        } catch (err) {
            showError('Impossible de charger ce deck.');
        }
    }

    async function saveDeck() {
        showError('');

        const name = deckNameInput.value.trim();
        const leaderCardId = parseInt(leaderSelect.value, 10);

        if (!name) {
            showError('Le nom du deck est requis.');
            return;
        }

        if (!leaderCardId) {
            showError('Veuillez sélectionner un Leader.');
            return;
        }

        const cards = [];
        selections.forEach((qty, cardId) => {
            cards.push({ cardId: cardId, quantity: qty });
        });

        const payload = {
            id: deckIdInput.value || null,
            name: name,
            leaderCardId: leaderCardId,
            cards: cards
        };

        saveButton.disabled = true;

        try {
            const response = await fetch('/Deck/Save', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken()
                },
                body: JSON.stringify(payload)
            });

            if (!response.ok) {
                const text = await response.text();
                showError(text || 'Erreur lors de l\'enregistrement du deck.');
                return;
            }

            closeModal();
            await loadDecks();
            document.dispatchEvent(new CustomEvent('worlds:decks-updated'));

        } catch (err) {
            showError('Erreur lors de l\'enregistrement du deck.');
        } finally {
            saveButton.disabled = false;
        }
    }

    async function deleteDeck() {
        const deckId = deckIdInput.value;
        if (!deckId) return;

        if (!await window.worldsConfirmation.ask('Supprimer définitivement ce deck ?', { title: 'Supprimer le deck', confirmText: 'Supprimer' })) return;

        try {
            const response = await fetch(`/Deck/${deckId}/Delete`, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': getAntiForgeryToken()
                }
            });

            if (!response.ok) {
                showError('Impossible de supprimer ce deck.');
                return;
            }

            closeModal();
            await loadDecks();
            document.dispatchEvent(new CustomEvent('worlds:decks-updated'));

        } catch (err) {
            showError('Erreur lors de la suppression du deck.');
        }
    }

    createButton?.addEventListener('click', openCreateModal);
    modalClose?.addEventListener('click', closeModal);
    modalBackdrop?.addEventListener('click', closeModal);
    saveButton?.addEventListener('click', saveDeck);
    deleteButton?.addEventListener('click', deleteDeck);

    loadDecks();

})();
