(function () {
    "use strict";

    const page = document.getElementById("gamePage");

    if (!page)
        return;

    const matchId = page.dataset.matchId;

    const el = {
        opponentAvatar: document.getElementById("opponentAvatar"),
        opponentName: document.getElementById("opponentName"),
        opponentTimer: document.getElementById("opponentTimer"),
        opponentMoney: document.getElementById("opponentMoney"),
        opponentLocation: document.getElementById("opponentLocation"),
        opponentCharacters: document.getElementById("opponentCharacters"),
        opponentLeader: document.getElementById("opponentLeader"),
        opponentDeck: document.getElementById("opponentDeck"),
        opponentGraveyard: document.getElementById("opponentGraveyard"),

        selfAvatar: document.getElementById("selfAvatar"),
        selfName: document.getElementById("selfName"),
        selfTimer: document.getElementById("selfTimer"),
        selfMoney: document.getElementById("selfMoney"),
        selfLocation: document.getElementById("selfLocation"),
        selfCharacters: document.getElementById("selfCharacters"),
        selfLeader: document.getElementById("selfLeader"),
        selfDeck: document.getElementById("selfDeck"),
        selfGraveyard: document.getElementById("selfGraveyard"),
        selfHand: document.getElementById("selfHand"),

        turnLabel: document.getElementById("gameTurnLabel"),
        activePlayer: document.getElementById("gameActivePlayer"),
        endTurnButton: document.getElementById("endTurnButton"),
        actionStatus: document.getElementById("gameActionStatus"),
        playCardButton: document.getElementById("playCardButton"),
        statNotifications: document.getElementById("gameStatNotifications"),
        dialog: document.getElementById("gameCardDialog"),
        dialogPreview: document.getElementById("gameCardDialogPreview"),
        dialogTitle: document.getElementById("gameCardDialogTitle"),
        dialogStatus: document.getElementById("gameCardDialogStatus"),
        dialogActions: document.getElementById("gameCardDialogActions"),
        surrenderButton: document.getElementById("surrenderButton"),

        history: document.getElementById("gameHistory"),
        chatMessages: document.getElementById("gameChatMessages"),
        chatForm: document.getElementById("gameChatForm"),
        chatInput: document.getElementById("gameChatInput"),

        tabs: document.querySelectorAll(".game-sidebar-tab"),
        panels: document.querySelectorAll(".game-sidebar-panel")
    };

    let latestState = null;
    let selectedHandCardId = null;
    let selectedHandTargetId = null;
    let selectedBoardCardId = null;
    let actionPending = false;
    let gameFinished = false;
    let inspectedCardId = null;
    let dialogSignature = null;
    let targeting = null;
    let clockReceivedAt = Date.now();
    const clockTicker = window.setInterval(updateClocks, 250);
    window.addEventListener("pagehide", () => window.clearInterval(clockTicker));

    function updateClocks() {
        if (!latestState) return;
        if (gameFinished && latestState.status !== 3) return;
        const anchor = Date.parse(latestState.clockAnchorUtc), server = Date.parse(latestState.serverTimeUtc);
        const sinceAnchor = Number.isFinite(anchor) && Number.isFinite(server) && anchor > 946684800000 ? Math.max(0, server - anchor) : 0;
        const elapsed = gameFinished || latestState.status === 3 ? 0 : Math.floor((sinceAnchor + Math.max(0, Date.now() - clockReceivedAt)) / 1000);
        const ticking = !gameFinished && (latestState.status === undefined || latestState.status === 2);
        el.selfTimer.textContent = formatTime(latestState.me.remainingSeconds - (ticking && latestState.isMyTurn ? elapsed : 0));
        el.opponentTimer.textContent = formatTime(latestState.opponent.remainingSeconds - (ticking && !latestState.isMyTurn ? elapsed : 0));
    }

    function cardStates(card) {
        const states = [];
        if (card.isExhausted) states.push("Épuisé");
        if (card.isBlocking) states.push("En blocage");
        if (card.hasShield) states.push("Bouclier");
        if (card.isStealthed) states.push("Furtif");
        if (card.damageMultiplier && card.damageMultiplier !== 1) states.push(`Dégâts ×${card.damageMultiplier}`);
        if (card.extraSkillUses > 0) states.push(`+${card.extraSkillUses} attaque(s)`);
        if (card.damageBonus > 0) states.push(`Dégâts +${card.damageBonus}`);
        return states;
    }

    function showStatus(message, error = false) {
        el.actionStatus.textContent = message;
        el.actionStatus.classList.toggle("is-error", error);
        el.actionStatus.setAttribute("role", error ? "alert" : "status");
    }

    function showError(error) {
        showStatus((error.message || String(error)).replace(/^.*HubException:\s*/, ""), true);
        el.dialogStatus.classList.add("is-error");
        el.dialogStatus.setAttribute("role", "alert");
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/game")
        .withAutomaticReconnect()
        .build();

    // =========================================================
    // ONGLETS (HISTORIQUE / CHAT)
    // =========================================================

    el.tabs.forEach(tab => {
        tab.addEventListener("click", () => {
            el.tabs.forEach(t => t.classList.remove("active"));
            tab.classList.add("active");

            const target = tab.dataset.tab;

            el.panels.forEach(panel => {
                panel.classList.toggle(
                    "active",
                    panel.dataset.panel === target);
            });
        });
    });

    // =========================================================
    // RENDU DE L'ETAT
    // =========================================================

    function formatTime(totalSeconds) {
        const seconds = Math.max(0, totalSeconds | 0);
        const minutes = Math.floor(seconds / 60);
        const remain = seconds % 60;

        return `${minutes.toString().padStart(2, "0")}:${remain.toString().padStart(2, "0")}`;
    }

    function createCardElement(card, options) {
        options = options || {};

        const div = document.createElement("div");
        div.className = "game-card";
        div.dataset.instanceId = card.instanceId;

        if (card.isExhausted)
            div.classList.add("is-exhausted");

        if (card.isBlocking)
            div.classList.add("is-blocking");

        if (card.hasShield)
            div.classList.add("has-shield");

        if (card.isStealthed)
            div.classList.add("is-stealthed");

        const visual = document.createElement("div");
        visual.className = "game-card-visual";
        visual.dataset.cardComponent = card.cardId;
        div.appendChild(visual);
        window.worldsCardComponents?.hydrate(div);

        if (card.currentHealth !== null && card.currentHealth !== undefined) {
            const hp = document.createElement("span");
            hp.className = "game-card-hp";
            hp.textContent = `${card.currentHealth}/${card.maxHealth ?? "?"}`;
            div.appendChild(hp);
        }

        const states = cardStates(card);
        if (states.length) {
            const badges = document.createElement("div"); badges.className = "game-card-states";
            badges.setAttribute("aria-label", `États : ${states.join(", ")}`);
            states.forEach(state => { const badge = document.createElement("span"); badge.textContent = state; badges.appendChild(badge); });
            div.appendChild(badges);
        }

        if (options.selectable) {
            div.tabIndex = 0;
            div.setAttribute("role", "button");
            div.setAttribute("aria-label", card.type === 1 ? card.name : `${card.name}, coût ${card.usageCost}`);
            div.addEventListener("click", () => options.onClick(card));
            div.addEventListener("keydown", event => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    options.onClick(card);
                }
            });
        }

        return div;
    }

    function renderPlayer(state, player, side) {
        const isSelf = side === "self";
        const money = isSelf ? el.selfMoney : el.opponentMoney;
        const timer = isSelf ? el.selfTimer : el.opponentTimer;
        const avatar = isSelf ? el.selfAvatar : el.opponentAvatar;
        const name = isSelf ? el.selfName : el.opponentName;
        const locationSlot = isSelf ? el.selfLocation : el.opponentLocation;
        const charactersRow = isSelf ? el.selfCharacters : el.opponentCharacters;
        const leaderSlot = isSelf ? el.selfLeader : el.opponentLeader;
        const deckSlot = isSelf ? el.selfDeck : el.opponentDeck;
        const graveyardSlot = isSelf ? el.selfGraveyard : el.opponentGraveyard;

        money.textContent = player.money;
        timer.textContent = formatTime(player.remainingSeconds);
        name.textContent = player.displayName;

        if (player.avatarUrl)
            avatar.src = player.avatarUrl;

        deckSlot.querySelector("strong").textContent = player.deckCount;
        graveyardSlot.querySelector("strong").textContent = player.graveyardCount;

        locationSlot.innerHTML = "";

        if (player.location) {
            locationSlot.appendChild(
                createCardElement(player.location, { selectable: true, onClick: onBoardCardClick }));
        }
        else {
            const span = document.createElement("span");
            span.textContent = "LIEU";
            locationSlot.appendChild(span);
        }

        leaderSlot.innerHTML = "";
        leaderSlot.appendChild(
            createCardElement(
                player.leader,
                {
                        selectable: true,
                        onClick: card => onBoardCardClick(card)
                    }));

        charactersRow.innerHTML = "";

        for (let i = 0; i < 4; i++) {
            const slot = document.createElement("div");
            slot.className = "game-character-slot";
            slot.dataset.slot = i.toString();

            const card = player.characters[i];

            if (card) {
                const cardEl = createCardElement(
                    card,
                    {
                            selectable: true,
                            onClick: c => onBoardCardClick(c)
                        });

                slot.appendChild(cardEl);
            }
            else if (isSelf) {
                slot.classList.toggle("is-available", !!selectedHandCardId && state.isMyTurn);
                slot.textContent = selectedHandCardId ? "Poser ici" : "Emplacement libre";
                slot.tabIndex = 0;
                slot.setAttribute("role", "button");
                slot.setAttribute("aria-label", `Poser dans l’emplacement ${i + 1}`);
                slot.addEventListener("click", () => onEmptySlotClick(i));
                slot.addEventListener("keydown", event => {
                    if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        onEmptySlotClick(i);
                    }
                });
            }

            charactersRow.appendChild(slot);
        }

        if (isSelf) {
            renderHand(state, player);
        }
    }

    function renderHand(state, player) {
        el.selfHand.innerHTML = "";

        player.hand.forEach(card => {
            const cardEl = createCardElement(
                card,
                {
                    selectable: true,
                    onClick: c => onHandCardClick(c)
                });

            if (card.instanceId === selectedHandCardId)
                cardEl.classList.add("is-selected");

            el.selfHand.appendChild(cardEl);
        });
        updateHandSpacing();
    }

    function updateHandSpacing() {
        const cards = el.selfHand.children;
        if (!cards.length || !el.selfHand.clientWidth) return;
        const width = cards[0].getBoundingClientRect().width;
        const step = cards.length > 1 ? Math.min(width, Math.max(0, (el.selfHand.clientWidth - 24 - width) / (cards.length - 1))) : width;
        el.selfHand.style.setProperty("--hand-overlap", `${step - width}px`);
    }

    function fitBoard() {
        if (window.innerHeight) page.style.height = `${Math.max(0, window.innerHeight - page.getBoundingClientRect().top)}px`;
        const board = page.querySelector(".game-board");
        if (!board?.clientHeight) return;
        const rows = page.querySelectorAll(".game-board-row");
        const height = Math.min(...[...rows].map(row => row.clientHeight));
        const width = Math.max(28, Math.floor(Math.min(180, (board.clientWidth - 88) / 5.85, (height - 24) * 63 / 88)));
        page.style.setProperty("--game-card-width", `${width}px`);
        updateHandSpacing();
    }
    if (window.ResizeObserver) {
        const resizeObserver = new window.ResizeObserver(fitBoard);
        resizeObserver.observe(page);
        resizeObserver.observe(page.querySelector(".game-center"));
    }
    window.addEventListener("resize", fitBoard);
    fitBoard();

    function renderState(state) {
        const previous = latestState;
        if (previous !== state) clockReceivedAt = Date.now();
        const positions = new Map();
        if (previous && !window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
            page.querySelectorAll(".game-card[data-instance-id]").forEach(node => {
                positions.set(node.dataset.instanceId, { rect: node.getBoundingClientRect(), node, self: !!node.closest(".game-self-zone") });
            });
        }
        latestState = state;
        if (!state.isMyTurn || !state.me.hand.some(card => card.instanceId === selectedHandCardId))
            selectedHandCardId = null;

        el.activePlayer.textContent =
            state.isMyTurn ? "Vous" : state.opponent.displayName;

        updateControls();

        renderPlayer(state, state.opponent, "opponent");
        renderPlayer(state, state.me, "self");
        refreshTargeting();
        updateClocks();
        if (previous && previous !== state) notifyStatChanges(previous, state);
        if (el.dialog.open) renderCardDialog();
        if (previous !== state && positions.size) animateCards(positions);
    }

    function updateControls() {
        el.endTurnButton.disabled = !latestState?.isMyTurn || actionPending || gameFinished || !!targeting;
        el.playCardButton.hidden = !selectedHandCardId;
        el.playCardButton.disabled = actionPending || !latestState?.isMyTurn || gameFinished;
    }

    function notifyStatChanges(previous, next) {
        let anchor = null;
        let offset = 0;
        function notify(message, gain, shortText) {
            if (!el.statNotifications || !anchor) return;
            const item = document.createElement("div");
            item.className = `game-stat-float ${gain ? "is-gain" : "is-loss"}`;
            item.textContent = shortText;
            item.title = message;
            item.setAttribute("aria-label", message);
            const rect = anchor.getBoundingClientRect(), board = page.getBoundingClientRect();
            item.style.left = `${Math.max(8, Math.min(board.width - 70, rect.right - board.left + 5))}px`;
            item.style.top = `${rect.top - board.top + offset}px`;
            offset += 25;
            el.statNotifications.appendChild(item);
            while (el.statNotifications.children.length > 12) el.statNotifications.children[0].remove();
            window.setTimeout(() => item.remove(), 2000);
        }
        function delta(label, oldValue, value, benefitSign = 1) {
            if (typeof oldValue !== "number" || typeof value !== "number" || oldValue === value) return;
            const difference = Math.round((value - oldValue) * 100) / 100;
            const signed = `${difference > 0 ? "+" : ""}${difference}`;
            notify(`${label} : ${signed} (${oldValue} → ${value})`, difference * benefitSign > 0, signed);
        }
        const visible = player => [player.leader, player.location, ...player.characters, ...player.hand].filter(Boolean);
        for (const side of ["me", "opponent"]) {
            const before = previous[side], after = next[side];
            const owner = side === "me" ? "Vous" : after.displayName;
            anchor = side === "me" ? el.selfMoney : el.opponentMoney;
            offset = 0;
            delta(`${owner} · énergie`, before.money, after.money);
            const oldCards = new Map(visible(before).map(card => [card.instanceId, card]));
            for (const card of visible(after)) {
                const old = oldCards.get(card.instanceId);
                if (!old) continue;
                anchor = [...page.querySelectorAll(".game-card[data-instance-id]")].find(node => node.dataset.instanceId === card.instanceId);
                offset = 0;
                const label = `${owner} · ${card.name}`;
                delta(`${label} · PV`, old.currentHealth, card.currentHealth);
                delta(`${label} · PV maximum`, old.maxHealth, card.maxHealth);
                delta(`${label} · coût`, old.usageCost, card.usageCost, -1);
                delta(`${label} · multiplicateur de dégâts`, old.damageMultiplier, card.damageMultiplier);
                delta(`${label} · bonus de dégâts`, old.damageBonus, card.damageBonus);
                delta(`${label} · utilisations supplémentaires`, old.extraSkillUses, card.extraSkillUses);
                for (const [field, text] of [["hasShield", "bouclier"], ["isStealthed", "furtivité"]]) {
                    if (typeof old[field] === "boolean" && old[field] !== card[field])
                        notify(`${label} : ${card[field] ? "gain" : "perte"} de ${text}`, !!card[field], `${card[field] ? "+" : "−"} ${text}`);
                }
                const oldAbilities = new Map((old.abilities || []).map(ability => [ability.id, ability]));
                for (const ability of card.abilities || []) {
                    const oldAbility = oldAbilities.get(ability.id);
                    if (oldAbility) delta(`${label} · ${ability.name} · coût`, oldAbility.usageCost, ability.usageCost, -1);
                }
            }
        }
    }

    function animateCards(positions) {
        const currentIds = new Set();
        page.querySelectorAll(".game-card[data-instance-id]").forEach(node => {
            currentIds.add(node.dataset.instanceId);
            const previous = positions.get(node.dataset.instanceId);
            const target = node.getBoundingClientRect();
            const source = previous?.rect || (node.closest(".game-self-zone") ? el.selfDeck : el.opponentDeck).getBoundingClientRect();
            if (!target.width || !source.width || !node.animate) return;
            const dx = source.left - target.left, dy = source.top - target.top;
            if (Math.abs(dx) + Math.abs(dy) < 2) return;
            node.animate([
                { transformOrigin: "top left", transform: `translate(${dx}px, ${dy}px) scale(${source.width / target.width}, ${source.height / target.height})`, opacity: previous ? 1 : 0.35 },
                { transformOrigin: "top left", transform: "translate(0, 0) scale(1)", opacity: 1 }
            ], { duration: 420, easing: "cubic-bezier(.2,.8,.2,1)" });
        });
        positions.forEach((previous, id) => {
            if (currentIds.has(id) || !previous.node.animate) return;
            const ghost = previous.node.cloneNode(true);
            ghost.classList.add("game-card-moving");
            const rect = previous.rect;
            Object.assign(ghost.style, { position: "fixed", left: `${rect.left}px`, top: `${rect.top}px`, width: `${rect.width}px`, height: `${rect.height}px`, margin: "0" });
            document.body.appendChild(ghost);
            const target = (previous.self ? el.selfGraveyard : el.opponentGraveyard).getBoundingClientRect();
            const animation = ghost.animate([{ transform: "none", opacity: 1 }, { transform: `translate(${target.left - rect.left}px, ${target.top - rect.top}px) scale(.35)`, opacity: 0 }], { duration: 360, easing: "ease-in" });
            animation.finished.then(() => ghost.remove(), () => ghost.remove());
        });
    }

    function appendLog(message, createdAt) {
        const line = document.createElement("div");
        line.className = "game-log-line";

        const time = createdAt
            ? new Date(createdAt).toLocaleTimeString()
            : "";

        line.textContent = time
            ? `[${time}] ${message}`
            : message;

        el.history.appendChild(line);
        el.history.scrollTop = el.history.scrollHeight;
    }

    function appendChatMessage(data) {
        const line = document.createElement("div");
        line.className = "game-chat-line";

        const time = new Date(data.createdAt).toLocaleTimeString();

        line.innerHTML =
            `<strong>${escapeHtml(data.username)}</strong> ` +
            `<span class="game-chat-time">${time}</span><br/>` +
            escapeHtml(data.message);

        el.chatMessages.appendChild(line);
        el.chatMessages.scrollTop = el.chatMessages.scrollHeight;
    }

    function escapeHtml(value) {
        const div = document.createElement("div");
        div.textContent = value ?? "";
        return div.innerHTML;
    }

    // =========================================================
    // ACTIONS JOUEUR
    // =========================================================

    function onHandCardClick(card) {
        if (targeting) return;
        if (!latestState || actionPending) return;
        selectedHandCardId = latestState.isMyTurn && !gameFinished ? card.instanceId : null;
        renderState(latestState);
        openCardDialog(card.instanceId);
    }

    function onEmptySlotClick(slotIndex) {
        if (targeting) return;
        if (!selectedHandCardId)
            return;

        playSelectedCard(slotIndex);
    }

    async function playSelectedCard(slot) {
        if (!selectedHandCardId || actionPending || !latestState?.isMyTurn || gameFinished)
            return;

        const cardInstanceId = selectedHandCardId;
        const selectedCard = latestState.me.hand.find(c => c.instanceId === cardInstanceId);
        const needsTarget = (selectedCard?.playTargetTypes || []).some(type => [10,12,20,21,22].includes(type));
        if (needsTarget && !selectedHandTargetId) { beginTargeting(selectedCard, null, slot); return; }
        actionPending = true;
        el.dialog.close();
        renderState(latestState);
        try {
            if (needsTarget) await connection.invoke("PlayCardWithTarget", matchId, cardInstanceId, slot, selectedHandTargetId);
            else await connection.invoke("PlayCard", matchId, cardInstanceId, slot);
            selectedHandCardId = null;
            selectedHandTargetId = null;
            showStatus("Carte jouée. Sélectionnez une autre carte ou terminez le tour.");
        } catch (error) {
            showError(error);
            openCardDialog(cardInstanceId);
            el.dialogStatus.textContent = el.actionStatus.textContent;
        } finally {
            actionPending = false;
            updateControls();
            if (el.dialog.open) {
                const message = el.dialogStatus.textContent;
                renderCardDialog();
                el.dialogStatus.textContent = message;
            }
        }
    }

    el.playCardButton.addEventListener("click", () => {
        const slot = latestState?.me.characters.findIndex(card => !card);
        if (slot === -1) { showStatus("Les quatre emplacements sont occupés.", true); return; }
        playSelectedCard(slot);
    });

    function onBoardCardClick(card) {
        if (targeting) {
            if (!targeting.ids.includes(card.instanceId) || actionPending) return;
            targeting.targetId = card.instanceId;
            refreshTargeting();
            renderTargetConfirmation();
            return;
        }
        openCardDialog(card.instanceId);
    }

    document.getElementById("gameCardDialogClose").addEventListener("click", () => { if (targeting) cancelTargeting(); else el.dialog.close(); });
    el.dialog.addEventListener("click", event => { if (event.target === el.dialog) { if (targeting) cancelTargeting(); else el.dialog.close(); } });
    el.dialog.addEventListener("cancel", () => cancelTargeting());
    document.getElementById("gameTargetCancel").addEventListener("click", cancelTargeting);
    document.addEventListener?.("keydown", event => { if (event.key === "Escape" && targeting) cancelTargeting(); });

    function openCardDialog(id) {
        inspectedCardId = id;
        dialogSignature = null;
        renderCardDialog();
        if (!el.dialog.open) el.dialog.showModal();
    }

    function renderCardDialog() {
        if (targeting?.targetId) { renderTargetConfirmation(); return; }
        const visible = player => [player.leader, player.location, ...player.characters, ...player.hand].filter(Boolean);
        const own = visible(latestState.me).find(card => card.instanceId === inspectedCardId);
        const card = own || visible(latestState.opponent).find(card => card.instanceId === inspectedCardId);
        if (!card) { el.dialog.close(); return; }
        const signature = JSON.stringify({ card, own: !!own, turn: latestState.isMyTurn, money: latestState.me.money,
            turnsPlayed: latestState.me.turnsPlayed, actionPending, gameFinished,
            targets: [latestState.me.leader, ...latestState.me.characters, latestState.opponent.leader, ...latestState.opponent.characters].filter(Boolean).map(c => [c.instanceId, c.isBlocking, c.isStealthed]) });
        if (signature === dialogSignature) return;
        dialogSignature = signature;
        el.dialogTitle.textContent = card.name;
        el.dialogStatus.classList.remove("is-error");
        el.dialogStatus.setAttribute("role", "status");
        el.dialogPreview.replaceChildren(createCardElement(card));
        el.dialogActions.replaceChildren();
        const hand = latestState.me.hand.some(c => c.instanceId === card.instanceId);
        const unit = latestState.me.leader.instanceId === card.instanceId || latestState.me.characters.some(c => c?.instanceId === card.instanceId);
        const turnReason = gameFinished ? "La partie est terminée." : !own ? "Carte adverse : consultation uniquement." : !latestState.isMyTurn ? "Attendez votre tour pour agir." : actionPending ? "Action en cours…" : "";
        el.dialogStatus.textContent = [turnReason || (hand ? "Choisissez où jouer cette carte." : "Choisissez une action ou consultez les capacités."), ...cardStates(card)].join(" · ");
        function button(label, action, reason = "") {
            const b = document.createElement("button"); b.type = "button";
            b.className = "game-dialog-action"; b.textContent = label; b.disabled = !!reason;
            b.title = reason; b.addEventListener("click", action); el.dialogActions.appendChild(b);
            if (reason) { const note = document.createElement("div"); note.className = "game-action-refusal"; note.setAttribute("role", "status"); note.textContent = reason; el.dialogActions.appendChild(note); }
        }
        if (hand && own) {
            let reason = turnReason || (card.usageCost > latestState.me.money ? `Énergie insuffisante : ${card.usageCost} nécessaire, ${latestState.me.money} disponible.` : "");
            const playTypes = (card.playTargetTypes || []).filter(type => [10,12,20,21,22].includes(type));
            if (playTypes.length) {
                if (!targetsFor(card, null).length) reason ||= "Aucune cible ne remplit les conditions de cette carte.";
            }
            if (reason && !actionPending) showStatus(reason, true);
            const play = slot => { selectedHandCardId = card.instanceId; selectedHandTargetId = null; if (playTypes.length) beginTargeting(card,null,slot); else playSelectedCard(slot); };
            if (card.type === 2) {
                latestState.me.characters.forEach((c, slot) => { if (!c) button(`Poser · emplacement ${slot + 1}`, () => play(slot), reason); });
                if (latestState.me.characters.every(Boolean)) el.dialogStatus.textContent = "Les quatre emplacements sont occupés.";
            } else button("Jouer la carte", () => play(null), reason || (card.type === 4 && latestState.me.location ? "Un lieu est déjà posé." : ""));
        }
        if (own && unit) {
            const phase = latestState.me.turnsPlayed <= 1 ? "Disponible à partir de votre deuxième tour." : "";
            if (card.type !== 1) button("Passer en blocage", () => executeCardAction("Block", card.instanceId), turnReason || phase || (card.isExhausted ? "Cette carte est épuisée." : ""));
        }
        for (const ability of card.abilities || []) {
            const detail = document.createElement("div"); detail.className = "game-dialog-ability";
            const name = document.createElement("strong"); name.textContent = `${ability.name} · ${ability.usageCost} énergie`;
            const description = document.createElement("p"); description.textContent = ability.description;
            detail.appendChild(name); detail.appendChild(description); el.dialogActions.appendChild(detail);
            if (!own || !unit || ability.type === 3) continue;
            const reason = turnReason || (ability.usageCost > latestState.me.money ? "Énergie insuffisante." : "") ||
                (ability.type === 1 && latestState.me.turnsPlayed <= 1 ? "Disponible à partir de votre deuxième tour." : "") ||
                (card.isExhausted ? "Cette carte est épuisée." : "") ||
                (ability.cooldown > 0 ? `Disponible dans ${ability.cooldown} tour(s).` : "") || ability.unavailableReason || "";
            const types = (ability.targetTypes || []).filter(type => [10, 12, 20, 21, 22].includes(type));
            if (!types.length) button(`Utiliser ${ability.name}`, () => executeCardAction("UseAbility", card.instanceId, ability.id, null), reason);
            else {
                button(`Utiliser ${ability.name}`, () => beginTargeting(card,ability), reason || (!targetsFor(card,ability).length ? "Aucune cible ne remplit les conditions de cette capacité (ciblage, blocage, camouflage ou origines)." : ""));
            }
        }
    }

    function selectableTargets(types) {
        return [...latestState.me.characters.filter(Boolean).map(c=>({card:c,type:10,owner:"Vous"})), {card:latestState.me.leader,type:11,owner:"Vous"},
            ...latestState.opponent.characters.filter(Boolean).map(c=>({card:c,type:20,owner:"Adversaire"})), {card:latestState.opponent.leader,type:21,owner:"Adversaire"}]
            .filter(t=>types.every(type=>type===t.type || type===12 && [10,11].includes(t.type) || type===22 && [20,21].includes(t.type)));
    }

    function targetsFor(card, ability) {
        const ids = ability ? ability.targetIds : card.playTargetIds;
        const candidates = selectableTargets((ability ? ability.targetTypes : card.playTargetTypes) || []);
        return ids ? candidates.filter(t => ids.includes(t.card.instanceId)) : candidates;
    }

    function visibleCard(id) {
        return [latestState.me, latestState.opponent].flatMap(p => [p.leader,p.location,...p.characters,...p.hand]).find(c => c?.instanceId === id);
    }

    function beginTargeting(card, ability, slot = null) {
        if (actionPending || !latestState.isMyTurn || gameFinished) return;
        const ids = targetsFor(card,ability).map(t => t.card.instanceId);
        if (!ids.length) { showStatus("Aucune cible ne remplit les conditions de cette action.",true); return; }
        targeting = { sourceId: card.instanceId, abilityId: ability?.id, label: ability?.name || "Jouer la carte", slot, ids, isAttack: !!ability?.isAttack };
        dialogSignature = null;
        el.dialog.close();
        refreshTargeting(); updateControls();
        showStatus("Cliquez sur une carte en surbrillance pour choisir la cible.");
    }

    function cancelTargeting() {
        targeting = null; selectedHandTargetId = null;
        el.dialog.close(); refreshTargeting(); updateControls();
        showStatus("Sélection annulée. Aucune action effectuée.");
    }

    function refreshTargeting() {
        if (targeting) {
            const source = visibleCard(targeting.sourceId);
            const ability = source?.abilities?.find(a => a.id === targeting.abilityId);
            if (!source || !latestState.isMyTurn || gameFinished || (targeting.abilityId != null && (!ability || source.isExhausted || ability.cooldown > 0 || ability.unavailableReason))) {
                cancelTargeting(); return;
            }
            targeting.ids = targetsFor(source,ability).map(t => t.card.instanceId);
            if (!targeting.ids.length || targeting.targetId && !targeting.ids.includes(targeting.targetId)) { cancelTargeting(); showStatus("La cible n'est plus disponible. Sélectionnez à nouveau votre action.",true); return; }
        }
        page.classList.toggle("is-targeting",!!targeting);
        document.getElementById("gameTargetBar").hidden = !targeting;
        document.getElementById("gameTargetLabel").textContent = targeting ? `${visibleCard(targeting.sourceId)?.name} · ${targeting.label} : choisissez une cible en surbrillance` : "";
        page.querySelectorAll(".game-card[data-instance-id]").forEach(node => {
            node.classList.toggle("is-targetable",!!targeting?.ids.includes(node.dataset.instanceId));
            node.classList.toggle("is-action-source",targeting?.sourceId === node.dataset.instanceId);
            node.classList.toggle("is-action-target",targeting?.targetId === node.dataset.instanceId);
        });
    }

    function renderTargetConfirmation() {
        if (!targeting?.targetId) return;
        const source = visibleCard(targeting.sourceId), target = visibleCard(targeting.targetId);
        if (!source || !target) { cancelTargeting(); return; }
        el.dialogTitle.textContent = targeting.label;
        el.dialogStatus.classList.remove("is-error");
        el.dialogStatus.textContent = `${source.name} → ${target.name}. Confirmez cette action ou annulez.`;
        el.dialogPreview.replaceChildren(createCardElement(source));
        el.dialogActions.replaceChildren();
        const preview = document.createElement("div"); preview.className = "game-confirm-target";
        const label = document.createElement("strong"); label.textContent = `Cible : ${target.name}`;
        preview.appendChild(label); preview.appendChild(createCardElement(target)); el.dialogActions.appendChild(preview);
        for (const [text, action] of [["Confirmer l’action",confirmTargeting],["Changer de cible",() => { targeting.targetId = null; el.dialog.close(); refreshTargeting(); }],["Annuler",cancelTargeting]]) {
            const button = document.createElement("button"); button.type = "button"; button.className = "game-dialog-action"; button.textContent = text; button.addEventListener("click",action); el.dialogActions.appendChild(button);
        }
        if (!el.dialog.open) el.dialog.showModal();
    }

    async function confirmTargeting() {
        if (!targeting || actionPending) return;
        const selection = targeting;
        const animation = selection.isAttack ? captureAttack(selection.sourceId,selection.targetId) : null;
        targeting = null; refreshTargeting();
        if (selection.abilityId != null) await executeCardAction("UseAbility",selection.sourceId,selection.abilityId,selection.targetId,animation);
        else { selectedHandCardId = selection.sourceId; selectedHandTargetId = selection.targetId; await playSelectedCard(selection.slot); }
    }

    function captureAttack(sourceId,targetId) {
        if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) return null;
        const nodes = [...page.querySelectorAll(".game-card[data-instance-id]")];
        const source = nodes.find(n => n.dataset.instanceId === sourceId), target = nodes.find(n => n.dataset.instanceId === targetId);
        if (!source || !target) return null;
        return { ghost: source.cloneNode(true), source: source.getBoundingClientRect(), target: target.getBoundingClientRect(), sourceId, targetId };
    }

    function animateAttack(attack) {
        if (!attack) return;
        const { ghost, source, target } = attack;
        ghost.classList.add("game-attack-ghost");
        Object.assign(ghost.style,{ position:"fixed", left:`${source.left}px`, top:`${source.top}px`, width:`${source.width}px`, height:`${source.height}px`, margin:"0", zIndex:"10000", pointerEvents:"none" });
        ghost.setAttribute("aria-hidden","true"); document.body.appendChild(ghost);
        const dx = target.left + target.width / 2 - source.left - source.width / 2;
        const dy = target.top + target.height / 2 - source.top - source.height / 2;
        const sourceNode = [...page.querySelectorAll(".game-card[data-instance-id]")].find(n => n.dataset.instanceId === attack.sourceId);
        if (sourceNode) sourceNode.style.visibility = "hidden";
        const finish = () => { ghost.remove(); if (sourceNode) sourceNode.style.visibility = ""; };
        ghost.animate([{ transform:"translate(0,0)" },{ transform:`translate(${dx * .82}px,${dy * .82}px) scale(1.06)`,offset:.45 },{ transform:"translate(0,0)" }],{ duration:650,easing:"ease-in-out" }).finished.then(finish,finish);
        const targetNode = [...page.querySelectorAll(".game-card[data-instance-id]")].find(n => n.dataset.instanceId === attack.targetId);
        targetNode?.animate([{ transform:"translateX(0)" },{ transform:"translateX(-8px)" },{ transform:"translateX(8px)" },{ transform:"translateX(0)" }],{ duration:250,delay:280 });
    }

    async function executeCardAction(method, ...args) {
        if (actionPending || !latestState.isMyTurn || gameFinished) return;
        const attack = args.length > 3 ? args.pop() : null;
        actionPending = true; el.dialog.close(); updateControls();
        if (method === "UseAbility" && args[2]) {
            const target = [...page.querySelectorAll(".game-card[data-instance-id]")].find(node => node.dataset.instanceId === args[2]);
            target?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
        }
        try { await connection.invoke(method, matchId, ...args); animateAttack(attack); showStatus("Action effectuée."); }
        catch (error) { showError(error); openCardDialog(inspectedCardId); el.dialogStatus.textContent = el.actionStatus.textContent; }
        finally {
            actionPending = false; updateControls();
            if (el.dialog.open) {
                const message = el.dialogStatus.textContent;
                renderCardDialog();
                el.dialogStatus.textContent = message;
                el.dialogStatus.classList.add("is-error");
            }
        }
    }

    el.endTurnButton.addEventListener("click", () => {
        if (targeting || actionPending) return;
        connection.invoke("EndTurn", matchId)
            .catch(showError);
    });

    el.surrenderButton.addEventListener("click", () => {
        if (!confirm("Voulez-vous vraiment abandonner la partie ?"))
            return;

        connection.invoke("Surrender", matchId)
            .catch(showError);
    });

    el.chatForm.addEventListener("submit", e => {
        e.preventDefault();

        const message = el.chatInput.value.trim();

        if (!message)
            return;

        connection.invoke("SendChatMessage", matchId, message)
            .catch(showError);

        el.chatInput.value = "";
    });

    // =========================================================
    // EVENEMENTS SERVEUR
    // =========================================================

    connection.on("GameStateUpdated", state => renderState(state));

    connection.on("GameLogAdded", data => appendLog(data.message, data.createdAt));

    connection.on("ChatMessageReceived", data => appendChatMessage(data));

    connection.on("GameFinished", data => {
        gameFinished = true;
        updateClocks();
        appendLog(
            `Partie terminée. Vainqueur : ${data.winnerUserId} (+${data.xpReward} XP).`);

        el.endTurnButton.disabled = true;
        selectedHandCardId = null;
        updateControls();
    });

    connection.start()
        .then(() => connection.invoke("JoinGame", matchId))
        .catch(showError);

    connection.onreconnected?.(() => connection.invoke("JoinGame", matchId).catch(showError));

    if (window.lucide) {
        window.lucide.createIcons();
    }
})();
