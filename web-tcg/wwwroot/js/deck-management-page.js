/**
 * Gestion des decks - Page dédiée
 * Inclut la sélection des variantes de cartes (Normal, Full Art, Alternate Art)
 */

(function () {
    'use strict';

    // Éléments DOM
    const DOM = {
        deckEmptyState: null,
        deckListContainer: null,
        deckList: null,

        deckModal: null,
        deckModalBackdrop: null,
        deckModalTitle: null,
        deckModalClose: null,

        deckId: null,
        deckName: null,
        deckLeaderSelect: null,
        deckCardGrid: null,
        deckCountLabel: null,
        deckError: null,

        deckSaveButton: null,
        deckDeleteButton: null,

        createButtonHeader: null,
        createButtonEmpty: null
    };

    // État de l'application
    const state = {
        leaders: [],
        cardsGrouped: new Map(), // gameplayCardId -> { name, type, variants: [{ cardId, artType, ... }] }
        selectedCards: new Map(), // cardId (variant spécifique) -> quantity
        isEditing: false,
        currentEditingDeckId: null
    };

    /**
     * Initialisation au chargement du DOM
     */
    function initDOM() {
        console.log('Initialisation du DOM...');

        DOM.deckEmptyState = document.getElementById('worldsDeckEmptyState');
        DOM.deckListContainer = document.querySelector('.worlds-deck-list-container');
        DOM.deckList = document.getElementById('worldsDeckList');

        DOM.deckModal = document.getElementById('worldsDeckModal');
        DOM.deckModalBackdrop = document.getElementById('worldsDeckModalBackdrop');
        DOM.deckModalTitle = document.getElementById('worldsDeckModalTitle');
        DOM.deckModalClose = document.getElementById('worldsDeckModalClose');

        DOM.deckId = document.getElementById('worldsDeckId');
        DOM.deckName = document.getElementById('worldsDeckName');
        DOM.deckLeaderSelect = document.getElementById('worldsDeckLeaderSelect');
        DOM.deckCardGrid = document.getElementById('worldsDeckCardGrid');
        DOM.deckCountLabel = document.getElementById('worldsDeckCountLabel');
        DOM.deckError = document.getElementById('worldsDeckError');

        DOM.deckSaveButton = document.getElementById('worldsDeckSaveButton');
        DOM.deckDeleteButton = document.getElementById('worldsDeckDeleteButton');

        DOM.createButtonHeader = document.getElementById('worldsDeckCreateButton') || document.getElementById('worldsCreateDeckButton');
        DOM.createButtonEmpty = document.getElementById('worldsDeckCreateButtonEmpty');
    }

    /**
     * Initialisation complète
     */
    async function init() {
        try {
            console.log('Démarrage de l\'initialisation...');
            initDOM();

            attachEventListeners();
            await loadDecks();

            if (typeof lucide !== 'undefined') {
                lucide.createIcons();
            }

            console.log('Initialisation complétée');
        } catch (error) {
            console.error('Erreur lors de l\'initialisation:', error);
            setError('Erreur lors du chargement de la page');
        }
    }

    /**
     * Charger les leaders et les cartes groupées par gameplay card
     */
    async function loadBuilderData() {
        try {
            console.log('Chargement des données du builder...');

            const response = await fetch('/Deck/BuilderData');
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const data = await response.json();
            console.log('Données reçues:', data);

            state.leaders = data.leaders || [];
            const playableCards = data.playableCards || [];

            // Grouper les cartes par gameplayCardId (pour gérer les variantes)
            state.cardsGrouped.clear();
            playableCards.forEach(card => {
                if (!state.cardsGrouped.has(card.gameplayCardId)) {
                    state.cardsGrouped.set(card.gameplayCardId, {
                        name: card.name,
                        type: card.type,
                        variants: []
                    });
                }
                state.cardsGrouped.get(card.gameplayCardId).variants.push({
                    cardId: card.cardId,
                    artType: card.artType,
                    ownedQuantity: card.ownedQuantity
                });
            });

            console.log(`Leaders: ${state.leaders.length}, Groupes de cartes: ${state.cardsGrouped.size}`);
            populateLeaderSelect();
        } catch (error) {
            console.error('Erreur loadBuilderData:', error);
            throw error;
        }
    }

    /**
     * Remplir le select des leaders
     */
    function populateLeaderSelect() {
        if (!DOM.deckLeaderSelect) return;

        DOM.deckLeaderSelect.innerHTML = '<option value="">Sélectionnez un Leader</option>';
        state.leaders.forEach(leader => {
            const option = document.createElement('option');
            option.value = leader.cardId;
            option.textContent = leader.name;
            DOM.deckLeaderSelect.appendChild(option);
        });
    }

    /**
     * Charger la liste des decks existants
     */
    async function loadDecks() {
        try {
            console.log('Chargement des decks...');

            const response = await fetch('/Deck/MyDecks');
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const decks = await response.json();
            console.log(`Decks chargés: ${decks.length}`);

            if (decks.length === 0) {
                showEmptyState();
            } else {
                renderDecksList(decks);
            }
        } catch (error) {
            console.error('Erreur loadDecks:', error);
            setError('Erreur lors du chargement des decks');
        }
    }

    /**
     * Afficher l'état vide
     */
    function showEmptyState() {
        if (DOM.deckEmptyState) DOM.deckEmptyState.style.display = '';
        if (DOM.deckListContainer) DOM.deckListContainer.style.display = 'none';
        if (DOM.deckList) DOM.deckList.innerHTML = '';
    }

    /**
     * Afficher la liste des decks
     */
    function renderDecksList(decks) {
        if (!DOM.deckList) return;

        if (DOM.deckEmptyState) DOM.deckEmptyState.style.display = 'none';
        if (DOM.deckListContainer) DOM.deckListContainer.style.display = '';

        DOM.deckList.innerHTML = decks.map(deck => `
            <div class="worlds-deck-card" data-deck-id="${deck.id}">
                <div class="worlds-deck-leader-preview" data-card-component="${deck.leaderCardId}"></div>
                <div class="worlds-deck-card-header">
                    <strong>${escapeHtml(deck.name)}</strong>
                    <small>${escapeHtml(deck.leaderName || 'Pas de leader')}</small>
                </div>

                <div class="worlds-deck-card-stats">
                    <div class="worlds-deck-stat-item ${deck.isComplete ? 'complete' : 'incomplete'}">
                        <i data-lucide="layers-3"></i>
                        <span>${deck.totalCards} / 40</span>
                    </div>
                </div>

                <div class="worlds-deck-card-actions">
                    <button type="button" class="worlds-btn worlds-btn-secondary" data-action="edit">
                        <i data-lucide="edit-3"></i>
                        <span>Éditer</span>
                    </button>
                    <button type="button" class="worlds-btn worlds-btn-ghost" data-action="delete">
                        <i data-lucide="trash-2"></i>
                    </button>
                </div>
            </div>
        `).join('');

        window.worldsCardComponents?.hydrate(DOM.deckList);

        // Attacher les événements
        document.querySelectorAll('.worlds-deck-card').forEach(card => {
            const deckId = card.dataset.deckId;
            card.querySelector('[data-action="edit"]').addEventListener('click', () => editDeck(deckId));
            card.querySelector('[data-action="delete"]').addEventListener('click', () => deleteDeckConfirm(deckId));
        });

        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
        }
    }

    /**
     * Ouvrir la modale de création
     */
    async function openCreateModal() {
        try { await loadBuilderData(); } catch (error) { setError('Impossible de charger la collection.'); return; }
        console.log('Ouverture de la modale de création');
        state.isEditing = false;
        state.currentEditingDeckId = null;
        state.selectedCards.clear();

        DOM.deckId.value = '';
        DOM.deckName.value = '';
        DOM.deckLeaderSelect.value = '';
        DOM.deckError.textContent = '';
        DOM.deckModalTitle.textContent = 'Nouveau deck';
        if (DOM.deckDeleteButton) DOM.deckDeleteButton.hidden = true;

        renderCardGrid();
        showModal();
    }

    /**
     * Éditer un deck existant
     */
    async function editDeck(deckId) {
        try {
            console.log('Édition du deck:', deckId);
            await loadBuilderData();

            const response = await fetch(`/Deck/${deckId}`);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const deck = await response.json();
            console.log('Deck chargé:', deck);

            state.isEditing = true;
            state.currentEditingDeckId = deckId;
            state.selectedCards.clear();

            // Charger les cartes du deck
            if (deck.cards && Array.isArray(deck.cards)) {
                deck.cards.forEach(card => {
                    state.selectedCards.set(card.cardId, card.quantity);
                    if (!Array.from(state.cardsGrouped.values()).some(group => group.variants.some(variant => variant.cardId === card.cardId))) {
                        state.cardsGrouped.set(`missing-${card.cardId}`, { name: 'Carte indisponible', variants: [{ cardId: card.cardId, ownedQuantity: 0 }] });
                    }
                });
            }

            DOM.deckId.value = deck.id;
            DOM.deckName.value = deck.name;
            DOM.deckLeaderSelect.value = deck.leaderCardId;
            DOM.deckError.textContent = '';
            DOM.deckModalTitle.textContent = `Éditer: ${deck.name}`;
            if (DOM.deckDeleteButton) DOM.deckDeleteButton.hidden = false;

            renderCardGrid();
            showModal();
        } catch (error) {
            console.error('Erreur editDeck:', error);
            setError('Erreur lors du chargement du deck');
        }
    }

    /**
     * Rendre la grille de sélection des cartes
     */
    function renderCardGrid() {
        if (!DOM.deckCardGrid) return;

        console.log('Rendu de la grille, cartes groupées:', state.cardsGrouped.size);

        DOM.deckCardGrid.innerHTML = Array.from(state.cardsGrouped.entries())
            .map(([gameplayId, card]) => renderCardGroup(gameplayId, card))
            .join('');

        // Attacher les événements
        document.querySelectorAll('[data-variant-id]').forEach(btn => {
            const variantId = btn.dataset.variantId;
            const action = btn.dataset.action;

            if (action === 'increase') {
                btn.addEventListener('click', () => increaseVariant(parseInt(variantId)));
            } else if (action === 'decrease') {
                btn.addEventListener('click', () => decreaseVariant(parseInt(variantId)));
            }
        });

        updateCardCount();
        window.worldsCardComponents?.hydrate(DOM.deckCardGrid);
        if (typeof lucide !== 'undefined') {
            lucide.createIcons();
        }
    }

    /**
     * Rendre un groupe de variantes pour une carte
     */
    function renderCardGroup(gameplayId, card) {
        const totalQty = card.variants.reduce((sum, v) => sum + (state.selectedCards.get(v.cardId) || 0), 0);
        const isMaxed = totalQty >= 4;

        return `
            <div class="worlds-card-group" data-gameplay-id="${gameplayId}">
                <div class="worlds-card-group-header">
                    <h4>${escapeHtml(card.name)}</h4>
                    <span class="worlds-card-group-total">${totalQty}/4</span>
                </div>

                <div class="worlds-variants-list">
                    ${card.variants.map((variant, idx) => {
                        const qty = state.selectedCards.get(variant.cardId) || 0;
                        const total = Array.from(state.selectedCards.values()).reduce((sum, value) => sum + value, 0);
                        const canAdd = qty < variant.ownedQuantity && !isMaxed && total < 40;
                        const canRemove = qty > 0;

                        return `
                            <div class="worlds-variant-item" data-variant-id="${variant.cardId}">
                                <div class="worlds-variant-preview">
                                    <div data-card-component="${variant.cardId}"></div>
                                </div>

                                <div class="worlds-variant-controls">
                                    <button class="worlds-qty-btn" data-action="decrease" data-variant-id="${variant.cardId}" ${!canRemove ? 'disabled' : ''}>−</button>
                                    <span class="worlds-qty-value">${qty}</span>
                                    <button class="worlds-qty-btn" data-action="increase" data-variant-id="${variant.cardId}" ${!canAdd ? 'disabled' : ''}>+</button>
                                </div>

                                <small class="worlds-variant-owned">${getArtTypeBadge(variant.artType)} · Possédée: ${variant.ownedQuantity}</small>
                            </div>
                        `;
                    }).join('')}
                </div>
            </div>
        `;
    }

    /**
     * Obtenir le badge du type d'art
     */
    function getArtTypeBadge(artType) {
        return { 1: 'Normal', 2: 'Full Art', 3: 'Alternate Art' }[artType] || 'Variante';
    }

    /**
     * Augmenter la quantité d'une variante
     */
    function increaseVariant(cardId) {
        const current = state.selectedCards.get(cardId) || 0;
        const group = Array.from(state.cardsGrouped.values()).find(card => card.variants.some(variant => variant.cardId === cardId));
        const variant = group?.variants.find(variant => variant.cardId === cardId);
        const groupTotal = group?.variants.reduce((sum, item) => sum + (state.selectedCards.get(item.cardId) || 0), 0);
        if (!variant || current >= variant.ownedQuantity || groupTotal >= 4) return;
        if (current < 4) {
            const total = Array.from(state.selectedCards.values()).reduce((a, b) => a + b, 0);
            if (total < 40) {
                state.selectedCards.set(cardId, current + 1);
                renderCardGrid();
            } else {
                setError('Le deck ne peut pas dépasser 40 cartes');
            }
        }
    }

    /**
     * Réduire la quantité d'une variante
     */
    function decreaseVariant(cardId) {
        const current = state.selectedCards.get(cardId) || 0;
        if (current > 0) {
            state.selectedCards.set(cardId, current - 1);
            if (current === 1) {
                state.selectedCards.delete(cardId);
            }
            renderCardGrid();
        }
    }

    /**
     * Mettre à jour le compteur de cartes
     */
    function updateCardCount() {
        const total = Array.from(state.selectedCards.values()).reduce((a, b) => a + b, 0);
        if (DOM.deckCountLabel) {
            DOM.deckCountLabel.textContent = `${total} / 40 cartes`;
        }

        const canSave = total <= 40 && DOM.deckLeaderSelect.value && DOM.deckName.value.trim() && !state.saving;
        if (DOM.deckSaveButton) {
            DOM.deckSaveButton.disabled = !canSave;
        }
    }

    /**
     * Enregistrer le deck
     */
    async function saveDeck() {
        if (state.saving || DOM.deckSaveButton.disabled) return;
        state.saving = true;
        updateCardCount();
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) throw new Error('Token CSRF manquant');

            const cards = Array.from(state.selectedCards.entries()).map(([cardId, quantity]) => ({
                cardId: cardId,
                quantity: quantity
            }));

            const request = {
                id: DOM.deckId.value || undefined,
                name: DOM.deckName.value.trim(),
                leaderCardId: parseInt(DOM.deckLeaderSelect.value),
                cards: cards
            };

            console.log('Envoi du deck:', request);

            const response = await fetch('/Deck/Save', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(request)
            });

            if (!response.ok) {
                throw new Error(await response.text() || 'Erreur lors de l\'enregistrement');
            }

            state.saving = false;
            closeModal();
            await loadDecks();
            document.dispatchEvent(new CustomEvent('worlds:decks-updated'));
            console.log('Deck enregistré');
        } catch (error) {
            console.error('Erreur saveDeck:', error);
            setError(error.message);
        } finally {
            state.saving = false;
            updateCardCount();
        }
    }

    /**
     * Supprimer un deck (avec confirmation)
     */
    function deleteDeckConfirm(deckId) {
        if (!confirm('Êtes-vous sûr de vouloir supprimer ce deck?')) return;
        deleteDeck(deckId);
    }

    /**
     * Supprimer un deck
     */
    async function deleteDeck(deckId) {
        if (state.saving) return;
        state.saving = true;
        updateCardCount();
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (!token) throw new Error('Token CSRF manquant');

            const response = await fetch(`/Deck/${deckId}/Delete`, {
                method: 'POST',
                headers: { 'RequestVerificationToken': token }
            });

            if (!response.ok) throw new Error(await response.text() || `HTTP ${response.status}`);

            state.saving = false;
            closeModal();
            await loadDecks();
            document.dispatchEvent(new CustomEvent('worlds:decks-updated'));
            console.log('Deck supprimé');
        } catch (error) {
            console.error('Erreur deleteDeck:', error);
            setError(error.message);
        } finally {
            state.saving = false;
            updateCardCount();
        }
    }

    /**
     * Afficher la modale
     */
    function showModal() {
        renderLeaderPreview();
        if (DOM.deckModal) {
            DOM.deckModal.removeAttribute('hidden');
            state.previousFocus = document.activeElement;
            state.previousOverflow = document.body.style.overflow;
            document.body.style.overflow = 'hidden';
            DOM.deckName.focus();
        }
    }

    /**
     * Fermer la modale
     */
    function closeModal() {
        if (state.saving) return;
        if (DOM.deckModal) {
            DOM.deckModal.setAttribute('hidden', '');
            document.body.style.overflow = state.previousOverflow || '';
            state.previousFocus?.focus();
        }
        state.selectedCards.clear();
    }

    /**
     * Afficher une erreur
     */
    function setError(message) {
        if (DOM.deckModal?.hidden && DOM.deckList) {
            let alert = document.getElementById('worldsDeckPageError');
            if (!alert) {
                alert = document.createElement('p');
                alert.id = 'worldsDeckPageError';
                alert.className = 'worlds-deck-error';
                alert.setAttribute('role', 'alert');
                DOM.deckList.before(alert);
            }
            alert.textContent = message;
        } else if (DOM.deckError) {
            DOM.deckError.textContent = message;
        }
    }

    /**
     * Échapper le HTML
     */
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    /**
     * Attacher tous les événements
     */
    function attachEventListeners() {
        console.log('Attachement des événements');

        DOM.createButtonHeader?.addEventListener('click', openCreateModal);
        DOM.createButtonEmpty?.addEventListener('click', openCreateModal);
        DOM.deckModalClose?.addEventListener('click', closeModal);
        DOM.deckModalBackdrop?.addEventListener('click', closeModal);
        DOM.deckSaveButton?.addEventListener('click', saveDeck);
        DOM.deckLeaderSelect?.addEventListener('change', updateCardCount);
        DOM.deckLeaderSelect?.addEventListener('change', renderLeaderPreview);
        DOM.deckName?.addEventListener('input', updateCardCount);
        document.addEventListener('keydown', event => {
            if (DOM.deckModal.hidden) return;
            if (event.key === 'Escape' && !DOM.deckModal.hidden) closeModal();
            if (event.key === 'Tab') {
                const elements = Array.from(DOM.deckModal.querySelectorAll('button:not(:disabled), input:not([type="hidden"]), select')).filter(element => !element.hidden && element.getClientRects().length);
                const first = elements[0], last = elements[elements.length - 1];
                if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
                else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
            }
        });

        DOM.deckDeleteButton?.addEventListener('click', () => {
            const deckId = DOM.deckId.value;
            if (deckId) deleteDeckConfirm(deckId);
        });
    }

    function renderLeaderPreview() {
        let preview = document.getElementById('worldsDeckLeaderPreview');
        if (!preview) {
            preview = document.createElement('div');
            preview.id = 'worldsDeckLeaderPreview';
            preview.className = 'worlds-deck-leader-preview';
            DOM.deckLeaderSelect.parentElement.appendChild(preview);
        }
        preview.innerHTML = DOM.deckLeaderSelect.value ? `<div data-card-component="${Number(DOM.deckLeaderSelect.value)}"></div>` : '';
        window.worldsCardComponents?.hydrate(preview);
    }

    // Initialiser au chargement du DOM
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
