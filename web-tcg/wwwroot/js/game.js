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

    let resultShown = false;
    let announcementTimer;
    let latestState = null;
    let selectedHandCardId = null;
    let selectedHandTargetId = null;
    let selectedBoardCardId = null;
    let actionPending = false;
    let gameFinished = false;
    let inspectedCardId = null;
    let dialogSignature = null;
    let targeting = null;
    let costSelection = null, authorizedCosts = null;
    function openCostSelection(card, requirements, proceed, abilityId = null) {
        if (requirements.some(r => r.eligibleIds.length < r.count)) { showStatus("Pas assez de cartes de main autorisées pour payer cette capacité.", true); return; }
        costSelection = { sourceId: card.instanceId, abilityId, requirements, proceed,
            choices: requirements.map(r => ({ conditionIndex: r.conditionIndex, cardIds: [] })) };
        renderCostSelection(); updateControls();
    }
    function cancelCostSelection() {
        costSelection = null; authorizedCosts = null; el.dialog.close(); updateControls(); showStatus("");
    }
    function renderCostSelection() {
        if (!costSelection) return;
        const selection = costSelection, source = visibleCard(selection.sourceId);
        if (!source || !latestState.isMyTurn || gameFinished) { cancelCostSelection(); return; }
        el.dialogTitle.textContent = "Payer les conditions de la capacité";
        el.dialogStatus.textContent = "Choisissez dans votre main. Paiement à la confirmation ; chaque carte ne peut être choisie qu’une fois.";
        el.dialogPreview.replaceChildren(createCardElement(source)); el.dialogActions.replaceChildren();
        selection.requirements.forEach((requirement,index) => {
            const choice=selection.choices[index];
            choice.cardIds=choice.cardIds.filter(id=>latestState.me.hand.some(c=>c.instanceId===id) && requirement.eligibleIds.includes(id));
            const group=document.createElement("section"); group.className="game-hand-cost-group";
            const title=document.createElement("strong"); title.textContent=`${requirement.discard ? "Défausser" : "Dévoiler"} ${requirement.count} carte(s) · ${choice.cardIds.length}/${requirement.count}`;
            group.appendChild(title);
            const grid=document.createElement("div"); grid.className="game-hand-cost-cards";
            latestState.me.hand.filter(c=>requirement.eligibleIds.includes(c.instanceId)).forEach(card=>{
                const button=document.createElement("button"); button.type="button"; button.className="game-hand-cost-card";
                const chosen=choice.cardIds.includes(card.instanceId);
                button.classList.toggle("is-chosen",chosen); button.setAttribute("aria-pressed",String(chosen)); button.setAttribute("aria-label",`${card.name} · ${chosen ? "sélectionnée" : "choisir"}`);
                button.disabled=!chosen && (choice.cardIds.length>=requirement.count || selection.choices.some(other=>other!==choice && other.cardIds.includes(card.instanceId)));
                const visual=createCardElement(card); visual.removeAttribute("role"); visual.removeAttribute("tabindex");
                button.appendChild(visual);
                button.addEventListener("click",()=>{ choice.cardIds=chosen ? choice.cardIds.filter(id=>id!==card.instanceId) : [...choice.cardIds,card.instanceId]; renderCostSelection(); });
                grid.appendChild(button);
            });
            group.appendChild(grid); el.dialogActions.appendChild(group);
        });
        const confirm=document.createElement("button"); confirm.type="button"; confirm.className="game-dialog-action"; confirm.classList.add("is-primary"); confirm.textContent="Confirmer les cartes et effectuer l’action";
        confirm.disabled=selection.requirements.some((r,i)=>selection.choices[i].cardIds.length!==r.count);
        confirm.addEventListener("click",()=>{ authorizedCosts={ sourceId:selection.sourceId, abilityId:selection.abilityId, selections:selection.choices.map(c=>({conditionIndex:c.conditionIndex,cardIds:[...c.cardIds]})) }; costSelection=null; selection.proceed(); });
        const cancel=document.createElement("button"); cancel.type="button"; cancel.className="game-dialog-action"; cancel.textContent="Annuler"; cancel.addEventListener("click",cancelCostSelection);
        el.dialogActions.appendChild(confirm); el.dialogActions.appendChild(cancel); if (!el.dialog.open) el.dialog.showModal();
    }
    let opponentPreview = null, opponentPreviewTimer, pendingOpponentAttack = null;
    function publishPreview(sourceId = null, abilityId = null, targetId = null, slot = null) {
        if (!latestState || gameFinished || !latestState.isMyTurn) return;
        connection.invoke("PreviewAction", matchId, sourceId, abilityId, targetId, slot).catch(() => {});
    }

    function renderOpponentPreview() {
        const label = document.getElementById("gameOpponentAction");
        const line = document.getElementById("gameOpponentActionLine");
        if (!label || !line) return;
        label.hidden = !opponentPreview; line.hidden = true;
        const nodes = [...page.querySelectorAll(".game-card[data-instance-id]")];
        nodes.forEach(node => {
            node.classList.toggle("is-opponent-source", !!opponentPreview?.sourceId && node.dataset.instanceId === opponentPreview.sourceId);
            node.classList.toggle("is-opponent-target", !!opponentPreview?.targetId && node.dataset.instanceId === opponentPreview.targetId);
        });
        [...el.opponentCharacters.children].forEach((node, index) => node.classList.toggle("is-opponent-slot", !!opponentPreview && opponentPreview.slot === index));
        if (!opponentPreview) return;
        const source = nodes.find(n => n.dataset.instanceId === opponentPreview.sourceId);
        const target = nodes.find(n => n.dataset.instanceId === opponentPreview.targetId);
        const targetCard = visibleCard(opponentPreview.targetId);
        label.textContent = `${latestState.opponent.displayName} · ${opponentPreview.confirmed ? "Action confirmée" : "Préparation"} : ${opponentPreview.label}${targetCard ? ` → ${targetCard.name}` : ""}`;
        if (!source || !target) return;
        const a = source.getBoundingClientRect(), b = target.getBoundingClientRect();
        const x = a.left + a.width / 2, y = a.top + a.height / 2;
        const dx = b.left + b.width / 2 - x, dy = b.top + b.height / 2 - y;
        Object.assign(line.style, { left: `${x}px`, top: `${y}px`, width: `${Math.hypot(dx, dy)}px`, transform: `rotate(${Math.atan2(dy, dx)}rad)` });
        line.hidden = false;
    }

    function clearOpponentPreview() {
        window.clearTimeout(opponentPreviewTimer); opponentPreview = null; renderOpponentPreview();
    }
    let handDrag = null;
    let ignoreHandClickUntil = 0;
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

    function cardEffects(card) {
        const effects = [];
        const add = (key,icon,label,description) => effects.push({key,icon,label,description});
        if (card.isImprisoned) add('prison','lock-keyhole','Prison','Ne peut effectuer aucune action jusqu’à la fin de son prochain tour.');
        if (card.isExhausted) add('exhausted','hourglass','Épuisé','Ne peut effectuer aucune action avant d’être prêt à nouveau.');
        if (card.isBlocking) add('taunt','shield-alert','Provocation','Les attaques adverses doivent cibler un personnage avec Provocation en priorité.');
        if (card.hasShield) add('shield','shield','Bouclier','Absorbe la prochaine attaque reçue, puis disparaît.');
        if (card.isStealthed) add('camouflage','eye-off','Camouflage','Ne peut pas être ciblé par l’adversaire. Pour un Personnage, nécessite un autre Personnage allié sur le terrain.');
        const multiplier = Number(card.damageMultiplier ?? 1);
        if (Number.isFinite(multiplier) && multiplier > 1) add('multiplier','swords',`Dégâts ×${multiplier}`,`Les dégâts de cette carte sont multipliés par ${multiplier} tant que ce bonus est actif.`);
        const attacks = Number(card.extraSkillUses || 0), bonus = Number(card.damageBonus || 0);
        if (attacks > 0) add('extra-attack','repeat-2',`+${attacks} attaque(s)`,`Autorise ${attacks} utilisation(s) supplémentaire(s) de compétence avant l’épuisement de cette carte pendant ce tour.`);
        if (Number.isFinite(bonus) && bonus !== 0) add('damage-bonus',bonus > 0 ? 'trending-up' : 'trending-down',`Dégâts ${bonus > 0 ? '+' : ''}${bonus}`,`Les dégâts de cette carte sont ${bonus > 0 ? 'augmentés' : 'réduits'} de ${Math.abs(bonus)} tant que ce bonus ou malus est actif.`);
        return effects;
    }
    function cardStates(card) { return cardEffects(card).map(effect => effect.label); }
    const effectTooltip = document.getElementById('gameEffectTooltip');
    function hideEffectTooltip() {
        if (!effectTooltip) return;
        effectTooltip.hidePopover?.(); effectTooltip.hidden = true;
    }
    function showEffectTooltip(badge,effect) {
        if (!effectTooltip) return;
        document.getElementById('gameEffectTooltipTitle').textContent = effect.label;
        document.getElementById('gameEffectTooltipDescription').textContent = effect.description;
        effectTooltip.hidden = false; effectTooltip.showPopover?.();
        const rect = badge.getBoundingClientRect();
        const width = effectTooltip.offsetWidth || 260, height = effectTooltip.offsetHeight || 90;
        effectTooltip.style.left = `${Math.max(8,Math.min(rect.left,window.innerWidth-width-8))}px`;
        effectTooltip.style.top = `${Math.max(8,rect.top-height-8 >= 8 ? rect.top-height-8 : Math.min(rect.bottom+8,window.innerHeight-height-8))}px`;
    }
    let effectIconsQueued = false;
    function queueEffectIcons() {
        if (effectIconsQueued || !window.lucide) return;
        effectIconsQueued = true;
        Promise.resolve().then(() => { effectIconsQueued = false; window.lucide.createIcons(); });
    }
    window.addEventListener('resize',hideEffectTooltip);
    document.addEventListener?.('scroll',hideEffectTooltip,true);

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
            if (card.maxHealth > 0) {
                const ratio = card.currentHealth / card.maxHealth;
                hp.classList.add(ratio > .75 ? "is-healthy" : ratio > .25 ? "is-wounded" : "is-critical");
            }
            hp.textContent = `${card.currentHealth}/${card.maxHealth ?? "?"}`;
            div.appendChild(hp);
        }

        const effects = cardEffects(card);
        if (effects.length) {
            const badges = document.createElement("div"); badges.className = "game-card-states";
            badges.setAttribute("aria-label", `États : ${effects.map(effect => effect.label).join(", ")}`);
            effects.forEach(effect => {
                const badge = document.createElement('span'); badge.className = 'game-effect-icon'; badge.dataset.effect = effect.key;
                badge.tabIndex = 0; badge.setAttribute('role','img'); badge.setAttribute('aria-label',`${effect.label} : ${effect.description}`);
                badge.setAttribute('aria-describedby','gameEffectTooltip');
                const icon = document.createElement('i'); icon.setAttribute('data-lucide',effect.icon); icon.setAttribute('aria-hidden','true'); badge.appendChild(icon);
                badge.addEventListener('mouseenter',() => showEffectTooltip(badge,effect));
                badge.addEventListener('mouseleave',hideEffectTooltip);
                badge.addEventListener('focus',() => showEffectTooltip(badge,effect));
                badge.addEventListener('blur',hideEffectTooltip);
                badge.addEventListener('click',event => { event.stopPropagation(); showEffectTooltip(badge,effect); });
                badge.addEventListener('pointerdown',event => event.stopPropagation());
                badge.addEventListener('keydown',event => {
                    event.stopPropagation();
                    if (event.key === 'Escape') hideEffectTooltip();
                    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); showEffectTooltip(badge,effect); }
                });
                badges.appendChild(badge);
            });
            div.appendChild(badges);
            queueEffectIcons();
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
            cardEl.classList.toggle("is-playable", state.isMyTurn && !gameFinished && card.usageCost <= player.money && (card.type !== 2 || player.characters.some(c => !c)) && (card.type !== 4 || !player.location));

            attachHandDrag(cardEl, card);
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
        const width = Math.max(28, Math.floor(Math.min(180, (board.clientWidth - 88) / 5.85, (height - 64) * 63 / 88)));
        page.style.setProperty("--game-card-width", `${width}px`);
        updateHandSpacing();
        if (opponentPreview) renderOpponentPreview();
    }
    if (window.ResizeObserver) {
        const resizeObserver = new window.ResizeObserver(fitBoard);
        resizeObserver.observe(page);
        resizeObserver.observe(page.querySelector(".game-center"));
    }
    window.addEventListener("resize", fitBoard);
    fitBoard();

    function renderState(state) {
        if (latestState && Number.isFinite(state.version) && Number.isFinite(latestState.version) && state.version < latestState.version) return;
        hideEffectTooltip();
        const previous = latestState;
        if (previous !== state) clockReceivedAt = Date.now();
        const positions = new Map();
        if (previous && !window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
            page.querySelectorAll(".game-card[data-instance-id]").forEach(node => {
                const onBoard = [previous.me, previous.opponent].some(player => [player.leader, player.location, ...player.characters].some(card => card?.instanceId === node.dataset.instanceId));
                positions.set(node.dataset.instanceId, { rect: node.getBoundingClientRect(), node, onBoard, self: !!node.closest(".game-self-zone") });
            });
        }
        latestState = state;
        if (handDrag && (!state.isMyTurn || state.status === 3 || !state.me.hand.some(c => c.instanceId === handDrag.cardId))) endHandDrag();
        gameFinished = gameFinished || state.status === 3;
        renderGameplayGuide(state, previous);
        if (!state.isMyTurn || !state.me.hand.some(card => card.instanceId === selectedHandCardId))
            selectedHandCardId = null;

        el.activePlayer.textContent =
            state.isMyTurn ? "Vous" : state.opponent.displayName;

        updateControls();

        renderPlayer(state, state.opponent, "opponent");
        renderPlayer(state, state.me, "self");
        refreshTargeting();
        if (opponentPreview && (state.isMyTurn || gameFinished || state.version > opponentPreview.version && !opponentPreview.confirmed)) clearOpponentPreview();
        else renderOpponentPreview();
        updateClocks();
        if (previous && previous !== state) notifyStatChanges(previous, state);
        if (el.dialog.open) { if (costSelection) renderCostSelection(); else renderCardDialog(); }
        if (previous !== state && positions.size) animateCards(positions);
        if (pendingOpponentAttack && state.version >= pendingOpponentAttack.version) {
            animateAttack(pendingOpponentAttack.animation); pendingOpponentAttack = null;
        }
        if (state.status === 3) {
            document.getElementById("gameOpeningDialog")?.close();
            showResult(state);
        } else showOpeningToss(state);
    }

    function renderGameplayGuide(state, previous) {
        const phase = document.getElementById("gamePhase");
        const hand = document.getElementById("gameOpponentHand");
        if (!phase) return;
        page.classList.toggle("is-my-turn", state.isMyTurn && !gameFinished);
        phase.textContent = gameFinished ? "Partie terminée" : `Tour ${state.turnNumber} · ${state.isMyTurn ? "À vous de jouer" : "Tour adverse"}`;
        el.turnLabel.textContent = `TOUR ${state.turnNumber}`;
        hand.textContent = `Main adverse : ${state.opponent.handCount} carte(s)`;
        const announcement = document.getElementById("gameTurnAnnouncement");
        if (!gameFinished && announcement && (!previous || previous.turnNumber !== state.turnNumber)) {
            window.clearTimeout(announcementTimer);
            announcement.textContent = state.isMyTurn ? `Tour ${state.turnNumber} · À vous de jouer` : `Tour ${state.turnNumber} · ${state.opponent.displayName}`;
            announcement.hidden = false;
            announcement.classList.remove("is-visible"); void announcement.offsetWidth; announcement.classList.add("is-visible");
            announcementTimer = window.setTimeout(() => { announcement.hidden = true; }, 2200);
            if (previous) showStatus("");
        }
    }

    let openingShown = false;
    function showOpeningToss(state) {
        if (openingShown || state.status !== 2 || state.turnNumber > 2 || !state.firstPlayerId) return;
        const winner = [state.me, state.opponent].find(player => player.userId === state.firstPlayerId);
        const dialog = document.getElementById("gameOpeningDialog");
        if (!winner || !dialog) return;
        openingShown = true;
        const storageKey = `worlds-tcg-opening:${matchId}`;
        try {
            if (window.sessionStorage?.getItem(storageKey)) return;
            window.sessionStorage?.setItem(storageKey, "seen");
        } catch (_) { /* The in-memory flag also prevents repeats when storage is unavailable. */ }
        const avatar = (player, prefix) => {
            document.getElementById(`gameOpening${prefix}`).textContent = player.displayName;
            const image = document.getElementById(`gameOpening${prefix}Avatar`);
            const initial = document.getElementById(`gameOpening${prefix}Initial`);
            initial.textContent = (player.displayName || "?").slice(0, 1).toUpperCase();
            image.hidden = !player.avatarUrl; initial.hidden = !!player.avatarUrl;
            if (player.avatarUrl) {
                image.src = player.avatarUrl;
                image.addEventListener("error", () => { image.hidden = true; initial.hidden = false; }, { once: true });
            }
        };
        avatar(state.me, "Me"); avatar(state.opponent, "Opponent");
        const coin = document.getElementById("gameOpeningCoin");
        coin.style.setProperty("--coin-end", winner.userId === state.me.userId ? "1800deg" : "1980deg");
        coin.classList.add("is-tossing");
        dialog.addEventListener("cancel", event => event.preventDefault());
        dialog.showModal();
        const reduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        window.setTimeout(() => {
            if (!dialog.open || gameFinished) return;
            document.getElementById("gameOpeningTitle").textContent = `${winner.displayName} commence`;
            document.getElementById("gameOpeningStatus").textContent = winner.userId === state.me.userId
                ? "Vous jouez en premier. À vous de jouer !" : "Votre adversaire joue en premier.";
            window.setTimeout(() => dialog.close(), 2200);
        }, reduced ? 150 : 1800);
    }

    function showResult(state) {
        const dialog = document.getElementById("gameResultDialog");
        if (!dialog || !state.winnerUserId) return;
        gameFinished = true;
        const won = state.winnerUserId === state.me.userId;
        dialog.classList.toggle("is-victory", won);
        document.getElementById("gameResultTitle").textContent = won ? "Victoire !" : "Défaite";
        document.getElementById("gameResultEmblem").textContent = won ? "♛" : "◇";
        const reasons = { 1: "LeaderDefeated", 2: "Surrender", 3: "Inactivity", 4: "TimeExpired" };
        const reason = reasons[state.endReason] || state.endReason;
        document.getElementById("gameResultReason").textContent = reason === "LeaderDefeated" ? (won ? "Le leader adverse a été vaincu." : "Votre leader a été vaincu.")
            : reason === "Surrender" ? (won ? "Votre adversaire a abandonné." : "Vous avez abandonné la partie.")
            : reason === "TimeExpired" ? "La partie s’est terminée au temps." : reason === "Inactivity" ? "La partie s’est terminée pour inactivité." : "La partie est terminée.";
        const known = state.xpEarned != null && state.cardBalanceEarned != null;
        document.getElementById("gameResultXp").textContent = known ? `+${state.xpEarned} XP` : "—";
        document.getElementById("gameResultMoney").textContent = known ? `+${Number(state.cardBalanceEarned).toLocaleString("fr-FR", { maximumFractionDigits: 0 })}` : "—";
        document.getElementById("gameResultRewardNote").textContent = known ? (won ? "Ces récompenses ont été ajoutées à votre compte." : "Aucune récompense attribuée pour cette partie.") : "Les montants enregistrés ne sont pas disponibles pour cette partie.";
        selectedHandCardId = null; targeting = null;
        el.dialog.close(); refreshTargeting(); updateControls();
        el.surrenderButton.disabled = true;
        if (!resultShown) { resultShown = true; dialog.showModal(); }
    }
    document.getElementById("gameResultClose")?.addEventListener("click", () => document.getElementById("gameResultDialog").close());

    function updateControls() {
        el.endTurnButton.disabled = !latestState?.isMyTurn || actionPending || gameFinished || !!targeting || !!costSelection;
        el.playCardButton.hidden = !latestState?.me.hand.some(card => card.instanceId === selectedHandCardId && card.type === 2);
        el.playCardButton.disabled = actionPending || !latestState?.isMyTurn || gameFinished;
        // A state update can arrive before the Hub invocation finishes (including the AI turn).
        // Refresh the open inspector when the pending flag changes, without reopening it.
        if (latestState && el.dialog.open && !costSelection && !targeting) {
            const errorMessage = el.dialogStatus.classList.contains("is-error") ? el.dialogStatus.textContent : null;
            renderCardDialog();
            if (errorMessage) {
                el.dialogStatus.textContent = errorMessage;
                el.dialogStatus.classList.add("is-error");
                el.dialogStatus.setAttribute("role", "alert");
            }
        }
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
                for (const [field, text] of [["hasShield", "bouclier"], ["isStealthed", "camouflage"]]) {
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
            ], { duration: 620, easing: "cubic-bezier(.2,.8,.2,1)" });
        });
        positions.forEach((previous, id) => {
            if (currentIds.has(id) || !previous.node.animate) return;
            const ghost = previous.node.cloneNode(true);
            ghost.classList.add("game-card-moving");
            ghost.setAttribute("aria-hidden", "true");
            ghost.style.visibility = "visible";
            const rect = previous.rect;
            Object.assign(ghost.style, { position: "fixed", left: `${rect.left}px`, top: `${rect.top}px`, width: `${rect.width}px`, height: `${rect.height}px`, margin: "0" });
            document.body.appendChild(ghost);
            const graveyard = previous.self ? el.selfGraveyard : el.opponentGraveyard;
            const target = graveyard.getBoundingClientRect();
            const dx = target.left + target.width / 2 - rect.left - rect.width / 2;
            const dy = target.top + target.height / 2 - rect.top - rect.height / 2;
            let burst = null;
            if (previous.onBoard) {
                ghost.classList.add("game-card-dying");
                burst = document.createElement("div"); burst.className = "game-death-burst"; burst.setAttribute("aria-hidden", "true");
                Object.assign(burst.style, { left: `${rect.left}px`, top: `${rect.top}px`, width: `${rect.width}px`, height: `${rect.height}px` });
                document.body.appendChild(burst);
            }
            const frames = previous.onBoard ? [
                { transform: "scale(1) rotate(0deg)", opacity: 1, filter: "brightness(1) saturate(1) blur(0px)", offset: 0 },
                { transform: "scale(1.06) rotate(-3deg)", opacity: 1, filter: "brightness(1.7) saturate(.5) blur(0px)", offset: .14 },
                { transform: "translateY(8px) scale(.98) rotate(5deg)", opacity: .85, filter: "brightness(.7) saturate(0) blur(.4px)", offset: .36 },
                { transform: `translate(${dx * .25}px,${dy * .25}px) scale(.8) rotate(-8deg)`, opacity: .6, filter: "brightness(.6) saturate(0) blur(1px)", offset: .6 },
                { transform: `translate(${dx}px,${dy}px) scale(.12) rotate(-18deg)`, opacity: 0, filter: "brightness(.4) saturate(0) blur(4px)", offset: 1 }
            ] : [{ transform: "none", opacity: 1 }, { transform: `translate(${dx}px,${dy}px) scale(.35)`, opacity: 0 }];
            const animation = ghost.animate(frames, { duration: previous.onBoard ? 1050 : 520, easing: "cubic-bezier(.3,.05,.35,1)", fill: "forwards" });
            if (previous.onBoard) graveyard.animate?.([
                { boxShadow: "0 0 0 0 #f4c43000", borderColor: "#ffffff12" },
                { boxShadow: "0 0 18px 3px #f4c43077", borderColor: "#f4c430" },
                { boxShadow: "0 0 0 0 #f4c43000", borderColor: "#ffffff12" }
            ], { duration: 450, delay: 750, easing: "ease-out" });
            const finish = () => { ghost.remove(); burst?.remove(); };
            animation.finished.then(finish, finish);
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

    function handPlayRefusal(card) {
        if (gameFinished) return "La partie est terminée.";
        if (!latestState?.isMyTurn) return "Attendez votre tour pour jouer une carte.";
        if (actionPending) return "Une action est déjà en cours…";
        if (card.usageCost > latestState.me.money) return `Énergie insuffisante : ${card.usageCost} nécessaire, ${latestState.me.money} disponible.`;
        if (card.type === 2 && latestState.me.characters.every(Boolean)) return "Les quatre emplacements sont occupés.";
        if (card.type === 4 && latestState.me.location) return "Un lieu est déjà posé.";
        return "";
    }

    function onHandCardClick(card) {
        if (targeting || Date.now() < ignoreHandClickUntil) return;
        const refusal = handPlayRefusal(card);
        if (refusal) { showStatus(refusal, true); return; }
        selectedHandCardId = card.instanceId;
        selectedHandTargetId = null;
        if (card.type === 2) {
            publishPreview(card.instanceId);
            renderState(latestState);
            showStatus("Choisissez un emplacement libre.");
        } else playSelectedCard(null);
    }

    // Déplacement à la souris, au stylet et au doigt ; aucune pose avant le dépôt.
    function attachHandDrag(node, card) {
        if (card.type !== 2) return;
        node.classList.add("is-draggable");
        node.addEventListener("pointerdown", event => {
            if (targeting || event.button > 0 || handDrag) return;
            const refusal = handPlayRefusal(card);
            if (refusal) { showStatus(refusal, true); return; }
            handDrag = { cardId: card.instanceId, pointerId: event.pointerId, startX: event.clientX, startY: event.clientY, node, ghost: null, slot: null };
            publishPreview(card.instanceId);
            node.setPointerCapture?.(event.pointerId);
        });
        node.addEventListener("pointermove", event => {
            const drag = handDrag;
            if (!drag || drag.pointerId !== event.pointerId) return;
            if (!drag.ghost && Math.hypot(event.clientX - drag.startX, event.clientY - drag.startY) < 7) return;
            event.preventDefault();
            if (!drag.ghost) {
                const rect = node.getBoundingClientRect();
                drag.ghost = node.cloneNode(true);
                drag.ghost.classList.add("game-drag-ghost");
                drag.ghost.setAttribute("aria-hidden", "true");
                Object.assign(drag.ghost.style, { position: "fixed", width: `${rect.width}px`, height: `${rect.height}px`, pointerEvents: "none", margin: "0" });
                document.body.appendChild(drag.ghost);
                node.classList.add("is-dragging");
                page.classList.add("is-dragging-hand");
                showStatus("Déposez sur un emplacement libre.");
            }
            drag.ghost.style.left = `${event.clientX - drag.ghost.getBoundingClientRect().width / 2}px`;
            drag.ghost.style.top = `${event.clientY - drag.ghost.getBoundingClientRect().height / 2}px`;
            const hit = document.elementFromPoint(event.clientX, event.clientY)?.closest(".game-character-slot");
            const slot = hit?.parentElement === el.selfCharacters ? Number(hit.dataset.slot) : null;
            const previousSlot = drag.slot;
            drag.slot = slot !== null && Number.isInteger(slot) && !latestState.me.characters[slot] ? slot : null;
            if (previousSlot !== drag.slot) publishPreview(drag.cardId, null, null, drag.slot);
            [...el.selfCharacters.children].forEach((element, index) => element.classList.toggle("is-drop-target", index === drag.slot));
        });
        node.addEventListener("pointerup", event => {
            if (!handDrag || handDrag.pointerId !== event.pointerId) return;
            const drag = handDrag, dropped = !!drag.ghost;
            endHandDrag();
            if (!dropped) return;
            ignoreHandClickUntil = Date.now() + 400;
            if (drag.slot === null) { showStatus(""); return; }
            const current = latestState.me.hand.find(c => c.instanceId === drag.cardId);
            const refusal = current ? handPlayRefusal(current) : "Cette carte n’est plus dans votre main.";
            if (refusal) { showStatus(refusal, true); return; }
            selectedHandCardId = drag.cardId; selectedHandTargetId = null;
            playSelectedCard(drag.slot);
        });
        node.addEventListener("pointercancel", endHandDrag);
        node.addEventListener("lostpointercapture", () => { if (handDrag?.node === node) endHandDrag(); });
        node.addEventListener("dragstart", event => event.preventDefault());
    }

    function endHandDrag() {
        if (!handDrag) return;
        const drag = handDrag; handDrag = null;
        publishPreview();
        drag.ghost?.remove(); drag.node.classList.remove("is-dragging");
        if (drag.node.hasPointerCapture?.(drag.pointerId)) drag.node.releasePointerCapture(drag.pointerId);
        page.classList.remove("is-dragging-hand");
        [...el.selfCharacters.children].forEach(node => node.classList.remove("is-drop-target"));
    }

    function onEmptySlotClick(slotIndex) {
        if (targeting) return;
        if (!latestState?.me.hand.some(card => card.instanceId === selectedHandCardId && card.type === 2))
            return;

        playSelectedCard(slotIndex);
    }

    async function playSelectedCard(slot) {
        if (!selectedHandCardId || actionPending || !latestState?.isMyTurn || gameFinished)
            return;

        const cardInstanceId = selectedHandCardId;
        const selectedCard = latestState.me.hand.find(c => c.instanceId === cardInstanceId);
        if (!selectedCard) return;
        const refusal = handPlayRefusal(selectedCard);
        if (refusal) { showStatus(refusal, true); return; }
        const needsTarget = (selectedCard?.playTargetTypes || []).some(type => [10,12,20,21,22].includes(type));
        if (needsTarget && !selectedHandTargetId) { beginTargeting(selectedCard, null, slot); return; }
        if (selectedCard.playHandCosts?.length && authorizedCosts?.sourceId !== cardInstanceId) { openCostSelection(selectedCard,selectedCard.playHandCosts,()=>playSelectedCard(slot)); return; }
        actionPending = true;
        el.dialog.close();
        renderState(latestState);
        try {
            if (authorizedCosts?.sourceId === cardInstanceId) await connection.invoke("PlayCardWithCosts",matchId,cardInstanceId,slot,selectedHandTargetId,authorizedCosts.selections);
            else if (needsTarget) await connection.invoke("PlayCardWithTarget", matchId, cardInstanceId, slot, selectedHandTargetId);
            else await connection.invoke("PlayCard", matchId, cardInstanceId, slot);
            selectedHandCardId = null;
            selectedHandTargetId = null;
            showStatus("");
        } catch (error) {
            showError(error);

        } finally {
            authorizedCosts = null;
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
            publishPreview(targeting.sourceId, targeting.abilityId ?? null, targeting.targetId, targeting.slot);
            refreshTargeting();
            renderTargetConfirmation();
            return;
        }
        openCardDialog(card.instanceId);
    }

    document.getElementById("gameCardDialogClose").addEventListener("click", () => { if(costSelection) cancelCostSelection(); else if (targeting) cancelTargeting(); else el.dialog.close(); });
    el.dialog.addEventListener("click", event => { if (event.target === el.dialog) { if(costSelection) cancelCostSelection(); else if (targeting) cancelTargeting(); else el.dialog.close(); } });
    el.dialog.addEventListener("cancel", () => { if(costSelection) cancelCostSelection(); else cancelTargeting(); });
    document.getElementById("gameTargetCancel").addEventListener("click", cancelTargeting);
    document.addEventListener?.("keydown", event => {
        if (event.key !== "Escape") return;
        if (targeting) cancelTargeting();
        if (handDrag) { endHandDrag(); ignoreHandClickUntil = Date.now() + 400; showStatus("Déplacement annulé."); }
    });

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
            targets: [latestState.me.leader, ...latestState.me.characters, latestState.opponent.leader, ...latestState.opponent.characters].filter(Boolean).map(c => [c.instanceId, c.isBlocking, c.isStealthed, c.isImprisoned]) });
        if (signature === dialogSignature) return;
        dialogSignature = signature;
        el.dialogTitle.textContent = card.name;
        el.dialogStatus.classList.remove("is-error");
        el.dialogStatus.setAttribute("role", "status");
        el.dialogPreview.replaceChildren(createCardElement(card));
        el.dialogActions.replaceChildren();
        const hand = latestState.me.hand.some(c => c.instanceId === card.instanceId);
        const unit = latestState.me.leader.instanceId === card.instanceId || latestState.me.characters.some(c => c?.instanceId === card.instanceId);
        const stateReason = card.isImprisoned ? "Cette carte est en prison jusqu’à la fin de son prochain tour." : card.isExhausted ? "Cette carte est épuisée." : "";
        const turnReason = gameFinished ? "La partie est terminée." : !own ? "Carte adverse : consultation uniquement." : !latestState.isMyTurn ? "Attendez votre tour pour agir." : actionPending ? "Action en cours…" : "";
        el.dialogStatus.textContent = [turnReason, ...cardStates(card)].filter(Boolean).join(" · ");
        function button(label, action, reason = "") {
            const b = document.createElement("button"); b.type = "button";
            b.className = "game-dialog-action"; b.textContent = label; b.disabled = !!reason;
            b.classList.toggle("is-primary", /^(Utiliser|Poser|Jouer)/.test(label));
            b.title = reason; b.addEventListener("click", action); el.dialogActions.appendChild(b);
            if (reason && reason !== turnReason && reason !== stateReason) { const note = document.createElement("div"); note.className = "game-action-refusal"; note.setAttribute("role", "status"); note.textContent = reason; el.dialogActions.appendChild(note); }
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
        for (const ability of card.abilities || []) {
            const detail = document.createElement("div"); detail.className = "game-dialog-ability";
            const name = document.createElement("strong"); name.textContent = ability.type === 1 || ability.usageCost > 0 ? `${ability.name} · ${ability.usageCost} énergie` : ability.name;
            const description = document.createElement("p"); description.textContent = ability.description;
            detail.appendChild(name); detail.appendChild(description); el.dialogActions.appendChild(detail);
            if (ability.handCosts?.length) { const costs=document.createElement("p"); costs.className="game-hand-cost-description";
                costs.textContent="Pour activer : "+ability.handCosts.map(r=>`${r.discard ? "défausser" : "dévoiler"} ${r.count} carte(s) autorisée(s) de votre main`).join(" ET "); detail.appendChild(costs); }
            if (!own || !unit || ability.type === 3) continue;
            const reason = turnReason || (ability.usageCost > latestState.me.money ? "Énergie insuffisante." : "") ||
                (ability.type === 1 && latestState.me.turnsPlayed <= 1 ? "Disponible à partir de votre deuxième tour." : "") ||
                (card.isImprisoned ? "Cette carte est en prison jusqu’à la fin de son prochain tour." : card.isExhausted ? "Cette carte est épuisée." : "") ||
                (ability.cooldown > 0 ? `Disponible dans ${ability.cooldown} tour(s).` : "") || ability.unavailableReason || "";
            const types = (ability.targetTypes || []).filter(type => [10, 12, 20, 21, 22].includes(type));
            if (!types.length) button(`Utiliser ${ability.name}`, () => executeCardAction("UseAbility", card.instanceId, ability.id, null), reason);
            else {
                button(`Utiliser ${ability.name}`, () => beginTargeting(card,ability), reason || (!targetsFor(card,ability).length ? ability.targetRefusalReason || "Aucune cible ne remplit les conditions de cette capacité (ciblage, blocage, camouflage ou origines)." : ""));
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
        // Automatic recipients (self, players, groups) don't restrict the card clicked for a targeted action.
        const types = ((ability ? ability.targetTypes : card.playTargetTypes) || []).filter(type => [10,12,20,21,22].includes(type));
        const candidates = selectableTargets(types);
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
        publishPreview(card.instanceId, ability?.id ?? null, null, slot);
        dialogSignature = null;
        el.dialog.close();
        refreshTargeting(); updateControls();
        showStatus("Cliquez sur une carte en surbrillance pour choisir la cible.");
    }

    function cancelTargeting() {
        publishPreview();
        targeting = null; selectedHandTargetId = null;
        el.dialog.close(); refreshTargeting(); updateControls();
        showStatus("Sélection annulée. Aucune action effectuée.");
    }

    function refreshTargeting() {
        if (targeting) {
            const source = visibleCard(targeting.sourceId);
            const ability = source?.abilities?.find(a => a.id === targeting.abilityId);
            if (!source || !latestState.isMyTurn || gameFinished || (targeting.abilityId != null && (!ability || source.isImprisoned || source.isExhausted || ability.cooldown > 0 || ability.unavailableReason))) {
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
        for (const [text, action] of [["Confirmer l’action",confirmTargeting],["Changer de cible",() => { targeting.targetId = null; publishPreview(targeting.sourceId, targeting.abilityId ?? null, null, targeting.slot); el.dialog.close(); refreshTargeting(); }],["Annuler",cancelTargeting]]) {
            const button = document.createElement("button"); button.type = "button"; button.className = "game-dialog-action"; button.textContent = text; button.addEventListener("click",action); el.dialogActions.appendChild(button);
            button.classList.toggle("is-primary", text === "Confirmer l’action");
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
        const source=visibleCard(args[0]), ability=source?.abilities?.find(a=>a.id===args[1]);
        if (method==="UseAbility" && ability?.handCosts?.length && !(authorizedCosts?.sourceId===args[0] && authorizedCosts.abilityId===args[1])) {
            openCostSelection(source,ability.handCosts,()=>executeCardAction(method,...args,attack),args[1]); return;
        }
        actionPending = true; el.dialog.close(); updateControls();
        if (method === "UseAbility" && args[2]) {
            const target = [...page.querySelectorAll(".game-card[data-instance-id]")].find(node => node.dataset.instanceId === args[2]);
            target?.scrollIntoView?.({ block: "nearest", inline: "nearest" });
        }
        try { if (method==="UseAbility" && authorizedCosts) await connection.invoke("UseAbilityWithCosts",matchId,...args,authorizedCosts.selections);
            else await connection.invoke(method, matchId, ...args); animateAttack(attack); showStatus(""); }
        catch (error) {
            showError(error); openCardDialog(inspectedCardId);
            el.dialogStatus.textContent = el.actionStatus.textContent;
            el.dialogStatus.classList.add("is-error");
            el.dialogStatus.setAttribute("role", "alert");
        }
        finally {
            authorizedCosts = null;
            actionPending = false; updateControls();
            if (el.dialog.open) {
                const message = el.dialogStatus.textContent;
                renderCardDialog();
                el.dialogStatus.textContent = message;
                el.dialogStatus.classList.add("is-error");
            }
        }
    }

    el.endTurnButton.addEventListener("click", async () => {
        if (targeting || actionPending || gameFinished || !latestState?.isMyTurn) return;
        actionPending = true; updateControls();
        showStatus("Fin de votre tour…");
        try { await connection.invoke("EndTurn", matchId); }
        catch (error) { showError(error); }
        finally { actionPending = false; updateControls(); }
    });

    el.surrenderButton.addEventListener("click", async () => {
        if (!await window.worldsConfirmation.ask("Voulez-vous vraiment abandonner la partie ?", { title: 'Abandonner la partie', confirmText: 'Abandonner' }))
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
    let revealPanelTimer;
    connection.on("GameCardsRevealed", event => {
        if (!latestState || event.userId!==latestState.opponent.userId) return;
        const panel=document.getElementById("gameRevealedCards"); if(!panel) return;
        panel.replaceChildren();
        const title=document.createElement("strong"); title.textContent=`${latestState.opponent.displayName} dévoile ${event.cards.length} carte(s) pour son action`;
        const cards=document.createElement("div"); cards.className="game-revealed-card-list";
        event.cards.forEach(card=>{ const visual=document.createElement("div"); visual.className="game-revealed-card";
            const component=document.createElement("div"); component.dataset.cardComponent=card.cardId; visual.appendChild(component); cards.appendChild(visual); });
        const close=document.createElement("button"); close.className="game-dialog-action"; close.type="button"; close.textContent="Fermer"; close.addEventListener("click",()=>{panel.hidden=true;});
        panel.appendChild(title); panel.appendChild(cards); panel.appendChild(close); panel.hidden=false; window.worldsCardComponents?.hydrate(panel);
        window.clearTimeout(revealPanelTimer); revealPanelTimer=window.setTimeout(()=>{panel.hidden=true;panel.replaceChildren();},10000);
    });
    connection.on("GameActionPreviewUpdated", preview => {
        if (!latestState || preview.userId !== latestState.opponent.userId || preview.version < latestState.version) return;
        if (!preview.label) { clearOpponentPreview(); return; }
        opponentPreview = preview; renderOpponentPreview();
        window.clearTimeout(opponentPreviewTimer);
        opponentPreviewTimer = window.setTimeout(clearOpponentPreview, preview.confirmed ? 1800 : 15000);
        if (preview.confirmed && preview.isAttack) pendingOpponentAttack = { version: preview.version, animation: captureAttack(preview.sourceId, preview.targetId) };
    });

    connection.on("GameLogAdded", data => appendLog(data.message, data.createdAt));

    connection.on("ChatMessageReceived", data => appendChatMessage(data));

    connection.on("GameFinished", data => {
        document.getElementById("gameOpeningDialog")?.close();
        if (latestState) showResult({ ...latestState, winnerUserId: data.winnerUserId, endReason: data.reason,
            xpEarned: data.winnerUserId === latestState.me.userId ? data.xpReward : 0,
            cardBalanceEarned: data.winnerUserId === latestState.me.userId ? data.cardBalanceReward : 0 });
        gameFinished = true;
        updateClocks();
        appendLog(
            `Partie terminée. ${data.winnerUserId === latestState?.me.userId ? "Victoire !" : "Défaite."}`);

        el.endTurnButton.disabled = true;
        selectedHandCardId = null;
        updateControls();
    });

    connection.start()
        .then(async () => { await connection.invoke("JoinGame", matchId); connectionStatus("En direct"); })
        .catch(showError);

    function connectionStatus(text, offline = false) {
        const node = document.getElementById("gameConnection");
        if (node) { node.textContent = text; node.hidden = !offline; node.classList.toggle("is-offline", offline); }
        if (offline) { actionPending = true; updateControls(); }
    }
    connection.onreconnecting?.(() => { clearOpponentPreview(); connectionStatus("Reconnexion…", true); });
    connection.onclose?.(() => { clearOpponentPreview(); connectionStatus("Hors connexion", true); showStatus("Connexion interrompue. Rechargez la page pour reprendre la partie.", true); });
    connection.onreconnected?.(async () => {
        try { await connection.invoke("JoinGame", matchId); actionPending = false; updateControls(); connectionStatus("En direct"); }
        catch (error) { showError(error); }
    });

    if (window.lucide) {
        window.lucide.createIcons();
    }
})();
