const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = name => fs.readFileSync(path.join(__dirname, '../web-tcg/wwwroot/js', name), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));

class Element {
    constructor() {
        this.value = ''; this.hidden = false; this.disabled = false; this.style = {}; this.dataset = {};
        this.textContent = ''; this.innerHTML = ''; this.listeners = {}; this.children = []; this.isConnected = true;
        const classes = new Set();
        this.classList = { toggle(name,on) { if (on) classes.add(name); else classes.delete(name); }, add(...names) { names.forEach(n => classes.add(n)); }, remove(...names) { names.forEach(n => classes.delete(n)); }, contains(name) { return classes.has(name); } };
    }
    addEventListener(name, handler) { this.listeners[name] = handler; }
    async click() { if (!this.disabled) await this.listeners.click?.({ target: this }); }
    setAttribute(name, value) { if (name === 'hidden') this.hidden = true; else this[name] = value; }
    removeAttribute(name) { if (name === 'hidden') this.hidden = false; }
    appendChild(child) { this.children.push(child); }
    replaceChildren(...children) { this.children = children; }
    querySelectorAll() { return []; }
    before() {}
    focus() {}
}

function lobby(home = false, activity = false) {
    const ids = home ? ['worldsDeckSelect', 'worldsPlayPvpButton', 'worldsPlayAiButton', 'worldsPlayStatus']
        : ['worldsVsPvpDeckSelect', 'worldsVsIaDeckSelect', 'worldsVsPvpJoinButton', 'worldsVsIaPlayButton', 'worldsVsPvpCancelButton', 'worldsVsPvpStatus', 'worldsVsIaStatus', 'worldsVsPvpQueueSection', 'worldsVsIaQueueSection'];
    const elements = Object.fromEntries(ids.map(id => [id, new Element()]));
    if (activity) { elements.matchmakingPlayersInGame = new Element(); elements.matchmakingPlayersInQueue = new Element(); }
    const select = elements[home ? 'worldsDeckSelect' : 'worldsVsPvpDeckSelect']; select.value = 'deck-1';
    if (!home) elements.worldsVsIaDeckSelect.value = 'deck-1';
    const handlers = {};
    const calls = [];
    let failStart = false, failJoin = false;
    const hub = {
        state: 'Disconnected',
        on(name, fn) { handlers[name] = fn; },
        onclose(fn) { handlers.close = fn; }, onreconnecting(fn) { handlers.reconnecting = fn; }, onreconnected(fn) { handlers.reconnected = fn; },
        async start() { if (failStart) throw new Error('Connexion impossible'); this.state = 'Connected'; },
        async invoke(name, ...args) {
            calls.push({ name, args });
            if (name === 'JoinQueue') {
                if (failJoin) throw new Error('Le deck doit contenir exactement 40 cartes. Actuellement : 12.');
                handlers.QueueStatusUpdated('waiting');
            }
        },
        async stop() { this.state = 'Disconnected'; }
    };
    const fetches = [];
    const timers = [];
    let activityFailure = false;
    const context = {
        document: { getElementById: id => elements[id] || null, querySelector: () => ({ value: 'csrf' }), addEventListener() {} },
        window: { location: {}, addEventListener() {}, setTimeout(fn, delay) { timers.push({ fn, delay }); return timers.length; }, clearTimeout() {} },
        signalR: {
            HubConnectionState: { Connected: 'Connected' },
            HubConnectionBuilder: class { withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return hub; } }
        },
        fetch: async (url, options) => {
            fetches.push({ url, options });
            if (url === '/Pvp/Activity') return { ok: !activityFailure, json: async () => ({ playersInGame: 8, playersInQueue: 3 }) };
            return { ok: false, text: async () => 'Le deck doit contenir exactement 40 cartes. Actuellement : 12.' };
        }, console
    };
    vm.runInNewContext(script('pvp-lobby.js'), context);
    return {
        elements, handlers, calls, fetches, hub, timers, failActivity(value) { activityFailure = value; },
        join: elements[home ? 'worldsPlayPvpButton' : 'worldsVsPvpJoinButton'],
        play: elements[home ? 'worldsPlayAiButton' : 'worldsVsIaPlayButton'],
        status: elements[home ? 'worldsPlayStatus' : 'worldsVsPvpStatus'],
        failStart(value) { failStart = value; }, failJoin(value) { failJoin = value; }
    };
}

test('PVP affiche la cause du refus et permet une nouvelle tentative', async () => {
    const app = lobby(); app.failJoin(true);
    await app.join.click();
    assert.match(app.status.textContent, /40 cartes.*12/);
    assert.equal(app.status.role, 'alert');
    assert.equal(app.join.disabled, false);
    app.failJoin(false); await app.join.click();
    assert.equal(app.elements.worldsVsPvpCancelButton.disabled, false);
});

test('échec de connexion puis nouvelle tentative', async () => {
    const app = lobby(); app.failStart(true); await app.join.click();
    assert.match(app.status.textContent, /Connexion impossible/);
    app.failStart(false); await app.join.click();
    assert.equal(app.calls.filter(call => call.name === 'JoinQueue').length, 1);
});

test('annulation, perte de connexion et erreur adverse débloquent les contrôles', async () => {
    const app = lobby(); await app.join.click();
    assert.equal(app.play.disabled, true);
    await app.elements.worldsVsPvpCancelButton.click();
    assert.equal(app.calls.at(-1).name, 'LeaveQueue'); assert.equal(app.play.disabled, false);
    await app.join.click(); app.handlers.QueueError('Le deck adverse est invalide.');
    assert.equal(app.status.textContent, 'Le deck adverse est invalide.'); assert.equal(app.join.disabled, false);
    await app.join.click(); app.handlers.reconnecting(); assert.equal(app.join.disabled, true);
    app.handlers.reconnected(); assert.equal(app.join.disabled, false);
});

test('accueil : le bouton de recherche devient un bouton d’annulation utilisable', async () => {
    const app = lobby(true); await app.join.click();
    assert.equal(app.join.disabled, false); assert.equal(app.join.innerHTML, 'Annuler la recherche');
    await app.join.click(); assert.equal(app.calls.at(-1).name, 'LeaveQueue');
});

test('IA envoie un JSON avec antiforgery et affiche le refus des 40 cartes', async () => {
    const app = lobby(); await app.play.click();
    const { url, options } = app.fetches[0];
    assert.equal(url, '/Pvp/PlayVsIA'); assert.equal(options.headers.RequestVerificationToken, 'csrf');
    assert.deepEqual(JSON.parse(options.body), { deckId: 'deck-1' });
    assert.match(app.elements.worldsVsIaStatus.textContent, /40 cartes.*12/);
    assert.equal(app.play.disabled, false); assert.equal(app.join.disabled, false);
});

function deckBuilder() {
    const elements = new Map();
    const get = id => { if (!elements.has(id)) elements.set(id, new Element()); return elements.get(id); };
    const body = new Element();
    const document = {
        readyState: 'loading', body, activeElement: new Element(),
        getElementById: get, querySelector: () => ({ value: 'csrf' }), querySelectorAll: () => [],
        createElement: () => new Element(), addEventListener() {}, dispatchEvent() {}
    };
    const requests = [];
    const context = {
        document, window: { worldsConfirmation: { ask: async () => true } }, console: { log() {}, error() {} },
        CustomEvent: class {},
        fetch: async (url, options) => {
            requests.push({ url, options });
            if (url === '/Deck/MyDecks') return { ok: true, json: async () => [] };
            return { ok: false, text: async () => 'Vous ne possédez pas suffisamment d’exemplaires.' };
        }
    };
    // Accès aux fonctions privées dans ce contexte de test uniquement.
    const source = script('deck-management-page.js').replace('// Initialiser au chargement du DOM',
        'globalThis.builder = { state, DOM, initDOM, increaseVariant, decreaseVariant, updateCardCount, saveDeck, deleteDeck, deleteDeckConfirm, renderCardGroup, renderCardGrid, filteredCards, changeCardPage, resetCardPage, renderDeckPreview, focusCard, updateFocusControls, attachEventListeners }; // Initialiser au chargement du DOM');
    vm.runInNewContext(source, context);
    context.builder.initDOM();
    get('worldsDeckModal').hidden = false;
    return { ...context.builder, get, requests, confirmation: context.window.worldsConfirmation };
}

test('deck : possession exacte, plafond partagé entre variantes, retrait', () => {
    const app = deckBuilder();
    app.state.cardsGrouped.set(10, { name: 'Carte', variants: [{ cardId: 10, ownedQuantity: 1 }, { cardId: 11, ownedQuantity: 4 }] });
    app.increaseVariant(10); app.increaseVariant(10);
    assert.equal(app.state.selectedCards.get(10), 1);
    for (let i = 0; i < 5; i++) app.increaseVariant(11);
    assert.equal(app.state.selectedCards.get(11), 3);
    app.decreaseVariant(10); app.increaseVariant(11);
    assert.equal(app.state.selectedCards.has(10), false); assert.equal(app.state.selectedCards.get(11), 4);
});

test('deck : plafond global de 40, brouillon enregistrable', () => {
    const app = deckBuilder(); app.state.cardsGrouped.set(1, { name: 'Carte', variants: [{ cardId: 1, ownedQuantity: 4 }] });
    for (let i = 2; i <= 11; i++) app.state.selectedCards.set(i, 4);
    app.increaseVariant(1); assert.equal(app.state.selectedCards.has(1), false);
    app.get('worldsDeckName').value = 'Brouillon'; app.get('worldsDeckLeaderSelect').value = '100';
    app.state.selectedCards.clear(); app.updateCardCount(); assert.equal(app.get('worldsDeckSaveButton').disabled, false);
});

test('deck : erreur serveur lisible, antiforgery et route de suppression correcte', async () => {
    const app = deckBuilder();
    app.get('worldsDeckName').value = 'Brouillon'; app.get('worldsDeckLeaderSelect').value = '100'; app.updateCardCount();
    await app.saveDeck();
    assert.equal(app.requests[0].options.headers.RequestVerificationToken, 'csrf');
    assert.match(app.get('worldsDeckError').textContent, /suffisamment/); assert.equal(app.get('worldsDeckSaveButton').disabled, false);
    await app.deleteDeck('deck-123');
    assert.equal(app.requests.at(-1).url, '/Deck/deck-123/Delete');
});

test('variantes utilisent le composant Card et le vrai ArtType', () => {
    const app = deckBuilder();
    const html = app.renderCardGroup(10, { name: 'Carte', variants: [{ cardId: 11, ownedQuantity: 1, artType: 3 }] });
    assert.match(html, /data-card-component="11"/); assert.match(html, /Alternate Art/); assert.doesNotMatch(html, /<img/);
});

test('deck : annuler la popup ne supprime rien, accepter appelle la suppression protégée', async () => {
    const app=deckBuilder();app.confirmation.ask=async()=>false;
    await app.deleteDeckConfirm('deck-123');assert.equal(app.requests.length,0);
    app.confirmation.ask=async()=>true;
    await app.deleteDeckConfirm('deck-123');assert.equal(app.requests[0].url,'/Deck/deck-123/Delete');
    assert.equal(app.requests[0].options.headers.RequestVerificationToken,'csrf');
});

test('deck : pagination de 50 variantes physiques et sélection conservée hors page', () => {
    const app = deckBuilder();
    for (let id=1;id<=65;id++) app.state.cardsGrouped.set(id,{ name:`Carte ${id}`,variants:[{cardId:id,name:`Carte ${id}`,cardNumber:`P01-${id}`,ownedQuantity:4}] });
    app.renderCardGrid();
    assert.equal((app.get('worldsDeckCardGrid').innerHTML.match(/data-card-component=/g)||[]).length,50);
    app.increaseVariant(1);
    app.changeCardPage(1);
    assert.equal((app.get('worldsDeckCardGrid').innerHTML.match(/data-card-component=/g)||[]).length,15);
    assert.equal(app.state.selectedCards.get(1),1);
    assert.match(app.get('worldsDeckPreviewCards').innerHTML,/data-card-component="1"/);
    assert.match(app.get('worldsDeckPreviewCards').innerHTML,/×1/);
    assert.equal(app.get('worldsDeckNextPage').disabled,true);
    app.changeCardPage(-1);
    assert.equal(app.get('worldsDeckPreviousPage').disabled,true);
    assert.equal(app.state.selectedCards.get(1),1);
});

test('deck : filtres remettent la pagination à la première page sans vider le deck', () => {
    const app = deckBuilder();
    for (let id=1;id<=65;id++) app.state.cardsGrouped.set(id,{ name:`Carte ${id}`,variants:[{cardId:id,name:`Carte ${id}`,cardNumber:`P01-${id}`,cardSetId: id<=55?1:2,ownedQuantity:4}] });
    app.state.selectedCards.set(1,4); app.state.cardPage=2;
    app.get('worldsDeckCardExtension').value='2';
    app.resetCardPage();
    assert.equal(app.state.cardPage,1);
    assert.equal((app.get('worldsDeckCardGrid').innerHTML.match(/data-card-component=/g)||[]).length,10);
    assert.equal(app.get('worldsDeckPreviewCount').textContent,'4 / 40 cartes');
    app.get('worldsDeckCardSearch').value='absent'; app.resetCardPage();
    assert.match(app.get('worldsDeckCardGrid').innerHTML,/Aucune carte/);
    assert.equal(app.state.selectedCards.get(1),4);
});

test('deck : aperçu du Leader, nom, variantes et retrait synchronisés', () => {
    const app=deckBuilder();
    app.state.leaders=[{cardId:100,name:'Leader'}]; app.get('worldsDeckLeaderSelect').value='100';
    app.get('worldsDeckName').value='Mon deck';
    app.state.cardsGrouped.set(1,{name:'Carte',variants:[{cardId:1,ownedQuantity:4,artType:1},{cardId:2,ownedQuantity:4,artType:2}]});
    app.increaseVariant(2);
    assert.match(app.get('worldsDeckPreviewLeader').innerHTML,/data-card-component="100"/);
    assert.equal(app.get('worldsDeckPreviewName').textContent,'Mon deck');
    assert.match(app.get('worldsDeckPreviewCards').innerHTML,/data-card-component="2"/);
    app.decreaseVariant(2);
    assert.doesNotMatch(app.get('worldsDeckPreviewCards').innerHTML,/data-card-component="2"/);
    assert.equal(app.get('worldsDeckPreviewCount').textContent,'0 / 40 cartes');
});

function cardComponentDocument() {
    const baseURI = 'https://worlds-tcg.test/';
    class Resource extends Element {
        constructor(tag, attribute, value) { super(); this.tag = tag; this.attributes = { [attribute]: value }; }
        getAttribute(name) { return this.attributes[name]; }
        get href() { return new URL(this.attributes.href, baseURI).href; }
        set href(value) { this.attributes.href = value; }
        get src() { return new URL(this.attributes.src, baseURI).href; }
        set src(value) { this.attributes.src = value; }
        cloneNode() { return new Resource(this.tag, ...Object.entries(this.attributes)[0]); }
    }
    const templates = new Map();
    const head = new Element();
    head.querySelectorAll = () => head.children.filter(node => node.tag === 'link');
    const document = {
        baseURI, head, get scripts() { return head.children.filter(node => node.tag === 'script'); },
        getElementById: id => templates.get(id) || null,
        createElement(tag) {
            if (tag === 'script') return new Resource('script', 'src', '');
            assert.equal(tag, 'template');
            return {
                set innerHTML(html) {
                    const cardHtml = html.match(/<div class="tcg-card">.*?<\/div>/s)?.[0];
                    const resources = [...html.matchAll(/<(link|script)\b[^>]*(href|src)="([^"]+)"/g)]
                        .map(match => new Resource(match[1], match[2], match[3]));
                    this.content = {
                        querySelectorAll: selector => resources.filter(node => node.tag === (selector.startsWith('link') ? 'link' : 'script')),
                        querySelector: selector => {
                            assert.equal(selector, '.tcg-card');
                            return cardHtml ? { cloneNode: () => ({ outerHTML: cardHtml }) } : null;
                        }
                    };
                }
            };
        }
    };
    return { document, templates };
}

test('composants : cache commun aux copies, reprise après erreur', async () => {
    let requests = 0, fail = true;
    const { document } = cardComponentDocument();
    const context = { window: {}, document, fetch: async () => {
        requests++; return { ok: !fail, text: async () => '<div class="tcg-card">Carte</div>' };
    } };
    vm.runInNewContext(script('card-components.js'), context);
    const first = new Element(); first.dataset.cardComponent = '42';
    const second = new Element(); second.dataset.cardComponent = '42';
    const root = { querySelectorAll: () => [first, second] };
    context.window.worldsCardComponents.hydrate(root); await tick();
    assert.equal(requests, 1); assert.equal(first.textContent, 'Carte indisponible');
    fail = false; context.window.worldsCardComponents.hydrate(root); await tick();
    assert.equal(requests, 2); assert.match(first.children[0].outerHTML, /tcg-card/);
    assert.equal(first.children[0].outerHTML, second.children[0].outerHTML);
    assert.notEqual(first.children[0], second.children[0]);
});

test('composants : ressources persistantes uniques et cartes seules après chaque rendu', async () => {
    const { document, templates } = cardComponentDocument();
    const html = '<link rel="stylesheet" href="/css/card.css"><script src="/js/card-layout.js"></script><div class="tcg-card">Carte</div>';
    const template = document.createElement('template'); template.innerHTML = html;
    templates.set('card-template-42', template);
    let requests = 0;
    const context = { window: {}, document, URL, fetch: async () => {
        requests++; return { ok: true, text: async () => html };
    } };
    vm.runInNewContext(script('card-components.js'), context);
    const first = new Element(); first.dataset.cardComponent = '42';
    const fetched = new Element(); fetched.dataset.cardComponent = '43';
    const root = { querySelectorAll: () => [first, fetched] };
    for (let render = 0; render < 3; render++) {
        first.replaceChildren(); fetched.replaceChildren();
        context.window.worldsCardComponents.hydrate(root); await tick();
        assert.equal(document.head.children.length, 2);
        assert.equal(document.head.children[0].href, 'https://worlds-tcg.test/css/card.css');
        assert.equal(document.scripts[0].src, 'https://worlds-tcg.test/js/card-layout.js');
        for (const card of [first, fetched]) {
            assert.equal(card.children.length, 1);
            assert.equal(card.children[0].outerHTML, '<div class="tcg-card">Carte</div>');
        }
    }
    assert.equal(requests, 1);
});

function game(motion = false) {
    const timeouts = [];
    const animations = [];
    let now = Date.UTC(2026, 9, 2, 12), clockTick, dropHit = null;
    class GameElement extends Element {
        set innerHTML(value) { this._html = value; this.children = []; }
        get innerHTML() { return this._html; }
        querySelector() { return this.strong ||= new GameElement(); }
        showModal() { this.open = true; }
        close() { this.open = false; }
        appendChild(child) { child.parent = this; super.appendChild(child); }
        remove() { if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this); }
        get parentElement() { return this.parent; }
        closest(selector) {
            if (selector === '.game-character-slot') return this.className === 'game-character-slot' ? this : null;
            let node = this;
            while (node) { if (node.fixtureId?.startsWith('self')) return node; node = node.parent; }
            return null;
        }
        getBoundingClientRect() { return this.parent?.fixtureId === 'selfHand' ? { left: 0, right: 96, top: 400, width: 96, height: 134 } : { left: 100, right: 220, top: 100, width: 120, height: 170 }; }
        animate(frames, options) { animations.push({ node: this, frames, options }); return { finished: Promise.resolve() }; }
        cloneNode() { const clone = new GameElement(); clone.dataset = { ...this.dataset }; clone.className = this.className; return clone; }
    }
    const elements = new Proxy({}, { get(target, id) { if (!target[id]) { target[id] = new GameElement(); target[id].fixtureId = id; } return target[id]; } });
    elements.gamePage.dataset.matchId = 'match-1';
    elements.gamePage.querySelectorAll = () => {
        const cards = [];
        function visit(node) { if (node.className === 'game-card') cards.push(node); node.children.forEach(visit); }
        ['selfHand', 'selfLeader', 'selfCharacters', 'opponentLeader', 'opponentCharacters'].forEach(id => visit(elements[id]));
        return cards;
    };
    const handlers = {}, calls = [];
    let failure = null;
    let onPlay = null;
    let onInvoke = null;
    const hub = {
        on(name, fn) { handlers[name] = fn; }, onreconnected() {},
        async start() {}, async invoke(name, ...args) {
            calls.push({ name, args });
            if (onInvoke) await onInvoke(name, ...args);
            if (['PlayCard','UseAbility','PlayCardWithTarget'].includes(name) && failure) throw new Error(failure);
            if (name === 'PlayCard') onPlay?.();
        }
    };
    vm.runInNewContext(script('game.js'), {
        document: { body: new GameElement(), elementFromPoint: () => dropHit, addEventListener() {}, getElementById: id => elements[id], createElement: () => new GameElement(), querySelectorAll: () => [] },
        window: { matchMedia: () => ({ matches: !motion }), setTimeout(fn,delay) { timeouts.push({fn,delay}); return timeouts.length; }, clearTimeout() {}, setInterval(fn) { clockTick = fn; return 1; }, clearInterval() {}, addEventListener() {} }, console,
        Date: class extends Date { static now() { return now; } },
        signalR: { HubConnectionBuilder: class { withUrl() { return this; } withAutomaticReconnect() { return this; } build() { return hub; } } }
    });
    const card = { instanceId: 'hand-1', cardId: 2, name: 'Personnage', type: 2, usageCost: 20 };
    const player = () => ({ displayName: 'Test', money: 40, remainingSeconds: 1200, characters: [null, null, null, null], leader: { ...card, type: 1, instanceId: 'leader' }, hand: [card] });
    const state = { turnNumber: 1, isMyTurn: true, me: { ...player(), userId: 'me', turnsPlayed: 1 }, opponent: { ...player(), userId: 'opponent', handCount: 5 } };
    handlers.GameStateUpdated(state);
    return { elements, handlers, calls, state, animations, runTimeout(delay) { const index=timeouts.findLastIndex(t=>t.delay===delay); assert.notEqual(index,-1); timeouts.splice(index,1)[0].fn(); },
        dragTo(slot, cancel = false) {
            const node = elements.selfHand.children[0];
            node.listeners.pointerdown({ button: 0, pointerId: 1, clientX: 10, clientY: 400 });
            dropHit = slot === null ? null : elements.selfCharacters.children[slot];
            node.listeners.pointermove({ pointerId: 1, clientX: 100, clientY: 100, preventDefault() {} });
            if (cancel) node.listeners.pointercancel();
            else node.listeners.pointerup({ pointerId: 1 });
        },
        advance(ms) { now += ms; clockTick(); }, now: () => now, select: () => elements.selfHand.children[0].click(), fail(message) { failure = message; }, onPlay(fn) { onPlay = fn; }, onInvoke(fn) { onInvoke = fn; } };
}

test('adversaire : préparation visible, annulation et anciennes sélections ignorées', () => {
    const app = game(true);
    const state = { ...app.state, version: 2, isMyTurn: false,
        me: { ...app.state.me, leader: { ...app.state.me.leader, instanceId: 'self' } },
        opponent: { ...app.state.opponent, leader: { ...app.state.opponent.leader, instanceId: 'enemy' } } };
    app.handlers.GameStateUpdated(state);
    const preview = { userId: 'opponent', version: 2, sourceId: 'enemy', targetId: 'self', label: 'Frappe', isAttack: true };
    app.handlers.GameActionPreviewUpdated(preview);
    assert.match(app.elements.gameOpponentAction.textContent, /Préparation.*Frappe/);
    assert.equal(app.elements.opponentLeader.children[0].classList.contains('is-opponent-source'), true);
    assert.equal(app.elements.selfLeader.children[0].classList.contains('is-opponent-target'), true);
    assert.equal(app.elements.gameOpponentActionLine.hidden, false);
    assert.equal(app.animations.some(a => a.options.duration === 650), false);
    app.handlers.GameActionPreviewUpdated({ userId: 'opponent', version: 2, label: '' });
    assert.equal(app.elements.gameOpponentAction.hidden, true);
    assert.equal(app.elements.gameOpponentActionLine.hidden, true);
    assert.equal(app.elements.selfLeader.children[0].classList.contains('is-opponent-target'), false);
    app.handlers.GameActionPreviewUpdated({ ...preview, version: 1 });
    assert.equal(app.elements.gameOpponentAction.hidden, true);
    app.handlers.GameActionPreviewUpdated(preview);
    app.handlers.GameStateUpdated({ ...state, version: 3 });
    assert.equal(app.elements.gameOpponentAction.hidden, true);
});

test('pile ou face : avatars, gagnant serveur, annonce puis fermeture automatique sans répétition', () => {
    const app=game(true);
    app.elements.gameOpeningCoin.style.setProperty=function(name,value) { this[name]=value; };
    const state={...app.state,status:2,firstPlayerId:'opponent',opponent:{...app.state.opponent,displayName:'Adversaire',avatarUrl:'/uploads/avatar.png'}};
    app.handlers.GameStateUpdated(state);
    assert.equal(app.elements.gameOpeningDialog.open,true);
    assert.equal(app.elements.gameOpeningOpponentAvatar.src,'/uploads/avatar.png');
    assert.equal(app.elements.gameOpeningMeAvatar.hidden,true);
    assert.equal(app.elements.gameOpeningMeInitial.textContent,'T');
    assert.equal(app.elements.gameOpeningCoin.style['--coin-end'],'1980deg');
    app.runTimeout(1800);
    assert.equal(app.elements.gameOpeningTitle.textContent,'Adversaire commence');
    app.runTimeout(2200);
    assert.equal(app.elements.gameOpeningDialog.open,false);
    app.handlers.GameStateUpdated(state);
    assert.equal(app.elements.gameOpeningDialog.open,false);
    const resumed=game(); resumed.handlers.GameStateUpdated({...state,turnNumber:5});
    assert.notEqual(resumed.elements.gameOpeningDialog.open,true);
});

test('adversaire : seule une attaque confirmée déclenche le mouvement', () => {
    const app = game(true);
    const state = { ...app.state, version: 1, isMyTurn: false,
        me: { ...app.state.me, leader: { ...app.state.me.leader, instanceId: 'self' } },
        opponent: { ...app.state.opponent, leader: { ...app.state.opponent.leader, instanceId: 'enemy' } } };
    app.handlers.GameStateUpdated(state);
    app.handlers.GameActionPreviewUpdated({ userId: 'opponent', version: 2, sourceId: 'enemy', targetId: 'self', label: 'Frappe', confirmed: true, isAttack: true });
    assert.match(app.elements.gameOpponentAction.textContent, /Action confirmée/);
    assert.equal(app.animations.filter(a => a.options.duration === 650).length, 0);
    app.handlers.GameStateUpdated({ ...state, version: 2 });
    assert.equal(app.animations.filter(a => a.options.duration === 650).length, 1);
    app.handlers.GameActionPreviewUpdated({ userId: 'me', version: 1, sourceId: 'self', targetId: 'enemy', label: 'Frappe', confirmed: true, isAttack: true });
    assert.equal(app.animations.filter(a => a.options.duration === 650).length, 1);
});

test('partie : la confirmation serveur anime la pose sans remplacer la carte animée', async () => {
    const app = game(true); await app.select();
    app.onPlay(() => {
        const card = app.state.me.hand[0];
        app.handlers.GameStateUpdated({ ...app.state, me: { ...app.state.me, hand: [], characters: [card, null, null, null] } });
    });
    await app.elements.playCardButton.click(); await tick();
    const animation = app.animations.find(a => a.node.dataset.instanceId === 'hand-1');
    assert.equal(animation.options.duration, 620);
    assert.equal(animation.node, app.elements.selfCharacters.children[0].children[0]);
    assert.match(animation.frames[0].transform, /translate\(-100px, 300px\)/);
});

test('mort : impact et départ vers le cimetière uniquement pour une carte du terrain', () => {
    const app = game(true);
    const card = { ...app.state.me.hand[0], instanceId: 'dying', currentHealth: 20 };
    const before = { ...app.state, me: { ...app.state.me, characters: [card, null, null, null] } };
    app.handlers.GameStateUpdated(before); app.animations.length = 0;
    app.handlers.GameStateUpdated({ ...before, me: { ...before.me, characters: [null,null,null,null], graveyardCount: 1 } });
    const death = app.animations.find(a => a.options.duration === 1050);
    assert.equal(death.node.dataset.instanceId, 'dying');
    assert.equal(death.node.classList.contains('game-card-dying'), true);
    assert.equal(death.node['aria-hidden'], 'true');
    assert.equal(death.frames.length, 5);
    assert.equal(death.frames.at(-1).opacity, 0);
    assert.match(death.frames.at(-1).transform, /scale\(\.12\)/);
    assert.equal(app.animations.some(a => a.node === app.elements.selfGraveyard && a.options.delay === 750), true);
    app.animations.length = 0;
    app.handlers.GameStateUpdated({ ...before, me: { ...before.me, characters: [null,null,null,null], hand: [] } });
    assert.equal(app.animations.some(a => a.options.duration === 1050), false);
    assert.equal(app.animations.some(a => a.options.duration === 520), true);
});

test('mort : aucun déplacement lorsque les animations sont réduites', () => {
    const app = game(false);
    const before = { ...app.state, me: { ...app.state.me, characters: [app.state.me.hand[0],null,null,null], hand: [] } };
    app.handlers.GameStateUpdated(before);
    app.handlers.GameStateUpdated({ ...before, me: { ...before.me, characters: [null,null,null,null] } });
    assert.equal(app.animations.length, 0);
});

test('partie : une carte sélectionnée peut être posée dans un emplacement précis', async () => {
    const app = game(); await app.select();
    assert.equal(app.elements.playCardButton.hidden, false);
    await app.elements.selfCharacters.children[2].click(); await tick();
    assert.deepEqual(app.calls.find(call => call.name === 'PlayCard').args, ['match-1', 'hand-1', 2]);
    assert.equal(app.elements.playCardButton.hidden, true);
});

test('partie : bouton poser choisit le premier emplacement libre', async () => {
    const app = game(); app.state.me.characters[0] = app.state.me.leader;
    app.handlers.GameStateUpdated(app.state); await app.select();
    await app.elements.playCardButton.click(); await tick();
    assert.equal(app.calls.find(call => call.name === 'PlayCard').args[2], 1);
});

test('partie : refus serveur visible, sélection conservée et nouvelle tentative possible', async () => {
    const app = game(); app.fail('HubException: Cet emplacement est déjà occupé.');
    await app.select(); await app.elements.playCardButton.click(); await tick();
    assert.equal(app.elements.gameActionStatus.textContent, 'Cet emplacement est déjà occupé.');
    assert.equal(app.elements.gameActionStatus.role, 'alert');
    assert.equal(app.elements.playCardButton.hidden, false);
    assert.equal(app.elements.playCardButton.disabled, false);
    app.fail(null); await app.elements.playCardButton.click(); await tick();
    assert.equal(app.calls.filter(call => call.name === 'PlayCard').length, 2);
});

test('partie : hors tour ou énergie insuffisante ne déclenche aucune pose', async () => {
    const app = game(); app.state.me.money = 5; await app.select();
    assert.match(app.elements.gameActionStatus.textContent, /Énergie insuffisante/);
    app.state.me.money = 40; app.state.isMyTurn = false; await app.select();
    assert.match(app.elements.gameActionStatus.textContent, /Attendez votre tour/);
    assert.equal(app.calls.some(call => call.name === 'PlayCard'), false);
});

test('matchmaking : compteurs actualisés puis indisponibles si le serveur échoue', async () => {
    const app = lobby(false, true); await tick();
    assert.equal(app.elements.matchmakingPlayersInGame.textContent, 8);
    assert.equal(app.elements.matchmakingPlayersInQueue.textContent, 3);
    assert.equal(app.timers.at(-1).delay, 5000);
    app.failActivity(true); await app.timers.at(-1).fn();
    assert.equal(app.elements.matchmakingPlayersInGame.textContent, '—');
    assert.equal(app.elements.matchmakingPlayersInQueue.textContent, '—');
});

test('partie : PV, énergie et baisse de coût notifiés sans doublon de rendu', () => {
    const app = game();
    assert.equal(app.elements.gameStatNotifications.children.length, 0);
    const leader = { ...app.state.me.leader, currentHealth: 100, maxHealth: 100 };
    app.handlers.GameStateUpdated({ ...app.state, me: { ...app.state.me, leader } });
    const before = app.elements.gameStatNotifications.children.length;
    const next = { ...app.state, me: { ...app.state.me, money: 60, leader: { ...leader, currentHealth: 75, maxHealth: 120, usageCost: 15 } } };
    app.handlers.GameStateUpdated(next);
    const notices = app.elements.gameStatNotifications.children.slice(before);
    assert.ok(notices.some(n => /énergie : \+20/.test(n.title) && n.textContent === '+20' && n.className.includes('is-gain')));
    assert.ok(notices.some(n => /PV : -25/.test(n.title) && n.textContent === '-25' && n.className.includes('is-loss')));
    assert.ok(notices.some(n => /PV maximum : \+20/.test(n.title)));
    assert.ok(notices.some(n => /coût : -5/.test(n.title) && n.className.includes('is-gain')));
    const count = app.elements.gameStatNotifications.children.length;
    app.handlers.GameStateUpdated(next);
    assert.equal(app.elements.gameStatNotifications.children.length, count);
    assert.equal(app.elements.selfLeader.children[0].children.some(n => n.className === 'game-card-name'), false);
});

test('partie : sélection de personnage sans popup et pose accessible par emplacement', async () => {
    const app = game(); await app.select();
    assert.equal(!!app.elements.gameCardDialog.open, false);
    await app.elements.selfCharacters.children[2].click(); await tick();
    assert.equal(app.calls.find(c => c.name === 'PlayCard').args[2], 2);
    assert.equal(!!app.elements.gameCardDialog.open, false);
    assert.equal(app.elements.selfHand.children[0].children.some(n => n.className === 'game-card-cost'), false);
});

test('partie : compétences ciblées utilisables sans action de blocage manuel', async () => {
    const app = game(); app.state.me.turnsPlayed = 2;
    app.state.opponent.leader.instanceId = 'enemy-leader';
    app.state.me.leader.abilities = [{ id: 7, type: 1, name: 'Attaque', usageCost: 10, targetTypes: [22], cooldown: 0 }];
    app.handlers.GameStateUpdated(app.state); await app.elements.selfLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Utiliser Attaque').click(); await tick();
    assert.equal(app.elements.opponentLeader.children[0].classList.contains('is-targetable'),true);
    assert.equal(app.calls.some(c => c.name === 'UseAbility'),false);
    await app.elements.opponentLeader.children[0].click();
    assert.match(app.elements.gameCardDialogStatus.textContent,/→/);
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Confirmer l’action').click(); await tick();
    assert.deepEqual(app.calls.find(c => c.name === 'UseAbility').args, ['match-1', 'leader', 7, 'enemy-leader']);
    await app.elements.selfLeader.children[0].click();
    assert.equal(app.elements.gameCardDialogActions.children.some(n => n.textContent === 'Passer en blocage'), false);
    app.state.me.characters[0] = { ...app.state.me.leader, type: 2, instanceId: 'own-character' };
    app.handlers.GameStateUpdated(app.state); await app.elements.selfCharacters.children[0].children[0].click();
    assert.equal(app.elements.gameCardDialogActions.children.some(n => n.textContent === 'Passer en blocage'), false);
    assert.equal(app.calls.some(c => c.name === 'Block'), false);
});

test('partie : inspection adverse et compétences épuisées ne proposent pas d’action active', async () => {
    const app = game(); app.state.me.turnsPlayed = 2;
    app.state.me.leader.isExhausted = true;
    app.state.me.leader.abilities = [{ id: 8, type: 1, name: 'Compétence', usageCost: 1, cooldown: 0 }];
    app.handlers.GameStateUpdated(app.state); await app.elements.selfLeader.children[0].click();
    assert.equal(app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Utiliser Compétence').disabled, true);
    app.state.opponent.leader.instanceId = 'opponent-only'; app.handlers.GameStateUpdated(app.state);
    await app.elements.opponentLeader.children[0].click();
    assert.match(app.elements.gameCardDialogStatus.textContent, /consultation uniquement/);
    assert.equal(app.elements.gameCardDialogActions.children.length, 0);
});

test('partie : aucune action pendant l’épuisement et le Leader ne peut pas bloquer', async () => {
    const app = game(); app.state.me.turnsPlayed = 1; app.state.me.leader.isExhausted = true;
    app.state.me.leader.abilities = [
        { id: 9, type: 2, name: 'Talent', usageCost: 1, cooldown: 0 },
        { id: 10, type: 1, name: 'Compétence', usageCost: 1, cooldown: 0 }
    ];
    app.handlers.GameStateUpdated(app.state); await app.elements.selfLeader.children[0].click();
    const actions = app.elements.gameCardDialogActions.children;
    assert.equal(actions.some(n => n.textContent === 'Passer en blocage'), false);
    assert.equal(actions.find(n => n.textContent === 'Utiliser Compétence').disabled, true);
    assert.equal(actions.find(n => n.textContent === 'Utiliser Talent').disabled, true);
    app.state.me.leader.isExhausted = false; app.handlers.GameStateUpdated({ ...app.state });
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Utiliser Talent').click(); await tick();
    assert.deepEqual(app.calls.find(c => c.name === 'UseAbility').args, ['match-1', 'leader', 9, null]);
});

test('partie : la mise à jour du chrono préserve les actions de la popup', async () => {
    const app = game(); await app.elements.selfLeader.children[0].click();
    const action = app.elements.gameCardDialogActions.children[0];
    app.handlers.GameStateUpdated({ ...app.state, me: { ...app.state.me, remainingSeconds: 1199 } });
    assert.equal(app.elements.gameCardDialogActions.children[0], action);
});

test('popup : retour de tour avant la réponse du Hub réactive les capacités sans rouvrir', async () => {
    const app = game(); app.state.me.turnsPlayed = 2;
    app.state.me.leader.abilities = [{ id: 1, type: 2, name: 'Soin', usageCost: 0, cooldownRemaining: 0, targetTypes: [1], targetIds: [] }];
    app.handlers.GameStateUpdated(app.state);
    let release;
    app.onInvoke(name => name === 'EndTurn' ? new Promise(resolve => { release = resolve; }) : undefined);
    const ending = app.elements.endTurnButton.click();
    app.handlers.GameStateUpdated({ ...app.state, turnNumber: 3, isMyTurn: true });
    await app.elements.selfLeader.children[0].click();
    const actions = () => app.elements.gameCardDialogActions.children.filter(node => node.textContent.startsWith('Utiliser'));
    assert.equal(actions()[0].disabled, true);
    assert.match(app.elements.gameCardDialogStatus.textContent, /Action en cours/);
    release(); await ending;
    assert.equal(app.elements.gameCardDialog.open, true);
    assert.equal(actions()[0].disabled, false);
    assert.doesNotMatch(app.elements.gameCardDialogStatus.textContent, /Action en cours/);
});

test('partie : la pose d’une décision transmet la cible choisie dans la popup', async () => {
    const app = game(); app.state.me.hand[0].type = 3; app.state.me.hand[0].playTargetTypes = [22];
    app.state.opponent.leader.instanceId = 'enemy-leader';
    app.handlers.GameStateUpdated(app.state); await app.select();
    assert.equal(!!app.elements.gameCardDialog.open, false);
    assert.equal(app.calls.some(c => c.name === 'PlayCardWithTarget'), false);
    await app.elements.opponentLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Confirmer l’action').click(); await tick();
    assert.deepEqual(app.calls.find(c => c.name === 'PlayCardWithTarget').args, ['match-1', 'hand-1', null, 'enemy-leader']);
});

test('partie : bonus fixe de dégâts visible et variation notifiée près de la carte', async () => {
    const app = game(); app.state.me.leader.damageBonus = 0;
    app.handlers.GameStateUpdated(app.state);
    app.handlers.GameStateUpdated({ ...app.state, me: { ...app.state.me, leader: { ...app.state.me.leader, damageBonus: 40 } } });
    const badges = app.elements.selfLeader.children[0].children.find(n => n.className === 'game-card-states');
    assert.equal(badges.children.some(n => n.textContent === 'Dégâts +40'), true);
    assert.equal(app.elements.gameStatNotifications.children.some(n => n.title.includes('bonus de dégâts : +40')), true);
});

test('partie : chrono actif fluide, recalage serveur, changement de tour et gel à la fin', async () => {
    const app = game(); app.advance(2100);
    assert.equal(app.elements.selfTimer.textContent, '19:58');
    assert.equal(app.elements.opponentTimer.textContent, '20:00');
    await app.select(); assert.equal(app.elements.selfTimer.textContent, '19:58');
    const state = { ...app.state, status: 2, serverTimeUtc: new Date(app.now()).toISOString(), clockAnchorUtc: new Date(app.now() - 20000).toISOString() };
    app.handlers.GameStateUpdated(state); assert.equal(app.elements.selfTimer.textContent, '19:40');
    app.advance(3000); assert.equal(app.elements.selfTimer.textContent, '19:37');
    const opponentTurn = { ...state, isMyTurn: false, serverTimeUtc: new Date(app.now()).toISOString(), clockAnchorUtc: new Date(app.now()).toISOString(), me: { ...state.me, remainingSeconds: 1177 } };
    app.handlers.GameStateUpdated(opponentTurn); app.advance(2000);
    assert.equal(app.elements.selfTimer.textContent, '19:37');
    assert.equal(app.elements.opponentTimer.textContent, '19:58');
    app.handlers.GameFinished({ winnerUserId: 'me', xpReward: 1 }); app.advance(4000);
    assert.equal(app.elements.opponentTimer.textContent, '19:58');
});

test('partie : les états et bonus sont affichés sur la carte et dans sa popup', async () => {
    const app = game(); Object.assign(app.state.me.leader, { isExhausted: true, isBlocking: true, hasShield: true, isStealthed: true, damageMultiplier: 1.5, extraSkillUses: 1 });
    app.handlers.GameStateUpdated(app.state);
    const badges = app.elements.selfLeader.children[0].children.find(n => n.className === 'game-card-states');
    assert.deepEqual(badges.children.map(n => n.textContent), ['Épuisé', 'Provocation', 'Bouclier', 'Furtif', 'Dégâts ×1.5', '+1 attaque(s)']);
    await app.elements.selfLeader.children[0].click();
    assert.match(app.elements.gameCardDialogStatus.textContent, /Épuisé.*Provocation.*Bouclier/);
});

test('ciblage : annulation sans paiement, carte interdite ignorée et retrait de cible', async () => {
    const app = game(); app.state.me.turnsPlayed = 2; app.state.opponent.leader.instanceId = 'enemy';
    app.state.me.leader.abilities = [{ id:7,type:1,name:'Soin',usageCost:1,targetTypes:[22],targetIds:['enemy'] }];
    app.handlers.GameStateUpdated(app.state); await app.elements.selfLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Utiliser Soin').click();
    await app.elements.selfLeader.children[0].click();
    assert.equal(app.elements.gameCardDialog.open,false);
    await app.elements.opponentLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Annuler').click();
    assert.equal(app.calls.some(c => c.name === 'UseAbility'),false);
    assert.equal(app.elements.gameTargetBar.hidden,true);
    await app.elements.selfLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Utiliser Soin').click();
    app.state.me.leader.abilities[0].targetIds=[]; app.handlers.GameStateUpdated({...app.state});
    assert.equal(app.elements.gameTargetBar.hidden,true);
    assert.match(app.elements.gameActionStatus.textContent,/plus disponible/);
});

test('ciblage : actions automatiques ne masquent pas les cibles adverses autorisées par région', async () => {
    for (const automaticTarget of [1,11,40,41,50,51,52]) {
        const app=game(); app.state.me.turnsPlayed=2;app.state.opponent.leader.instanceId='enemy';
        app.state.me.leader.abilities=[{id:7,type:1,name:'Attaque Europe',usageCost:0,targetTypes:[22,automaticTarget],targetIds:['enemy']}];
        app.handlers.GameStateUpdated(app.state);await app.elements.selfLeader.children[0].click();
        const use=app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Utiliser Attaque Europe');
        assert.equal(use.disabled,false,`action automatique ${automaticTarget}`);
        await use.click(); await app.elements.opponentLeader.children[0].click();
        assert.match(app.elements.gameCardDialogStatus.textContent,/Confirmez/);
    }
});

test('coût de main : choix obligatoire, annulation sans appel et confirmation avec les cartes choisies', async () => {
    const app=game(); app.state.me.turnsPlayed=2;
    app.state.me.leader.abilities=[{id:7,type:2,name:'Marchander',usageCost:1,targetTypes:[],handCosts:[{conditionIndex:0,discard:true,count:1,eligibleIds:['hand-1']}]}];
    app.handlers.GameStateUpdated(app.state);
    const open=async()=>{await app.elements.selfLeader.children[0].click();await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Utiliser Marchander').click();};
    await open();
    assert.equal(app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Confirmer les cartes et effectuer l’action').disabled,true);
    await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Annuler').click();
    assert.equal(app.calls.some(c=>c.name==='UseAbilityWithCosts'),false);
    await open();
    const group=app.elements.gameCardDialogActions.children.find(n=>n.className==='game-hand-cost-group');
    await group.children[1].children[0].click();
    await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Confirmer les cartes et effectuer l’action').click();await tick();
    assert.deepEqual(JSON.parse(JSON.stringify(app.calls.find(c=>c.name==='UseAbilityWithCosts').args)),['match-1','leader',7,null,[{conditionIndex:0,cardIds:['hand-1']}]]);
});

test('coût de main : une pose attend son paiement et une révélation adverse utilise Card', async () => {
    const app=game(); app.state.me.hand[0].playHandCosts=[{conditionIndex:0,discard:false,count:1,eligibleIds:['extra']}];
    app.state.me.hand.push({...app.state.me.hand[0],instanceId:'extra',playHandCosts:[]});
    app.handlers.GameStateUpdated(app.state);await app.select();await app.elements.selfCharacters.children[1].click();
    assert.equal(app.calls.some(c=>c.name==='PlayCardWithCosts'),false);
    const group=app.elements.gameCardDialogActions.children.find(n=>n.className==='game-hand-cost-group');await group.children[1].children[0].click();
    await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Confirmer les cartes et effectuer l’action').click();await tick();
    assert.equal(app.calls.find(c=>c.name==='PlayCardWithCosts').args[2],1);
    app.handlers.GameCardsRevealed({userId:'opponent',cards:[{cardId:12,name:'Carte dévoilée'}]});
    assert.equal(app.elements.gameRevealedCards.hidden,false);
    assert.equal(app.elements.gameRevealedCards.children[1].children[0].children[0].dataset.cardComponent,12);
});

test('ciblage : attaque animée après confirmation, refus sans animation', async () => {
    const app=game(true); app.state.me.turnsPlayed=2; app.state.opponent.leader.instanceId='enemy';
    app.state.me.leader.abilities=[{id:7,type:1,name:'Attaque',usageCost:1,targetTypes:[22],targetIds:['enemy'],isAttack:true}];
    app.handlers.GameStateUpdated(app.state);
    async function choose() {
        await app.elements.selfLeader.children[0].click();
        await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Utiliser Attaque').click();
        await app.elements.opponentLeader.children[0].click();
    }
    await choose();
    assert.equal(app.animations.some(a=>a.options.duration===650),false);
    await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Confirmer l’action').click(); await tick();
    assert.equal(app.animations.filter(a=>a.options.duration===650).length,1);
    app.fail('HubException: Condition non remplie : au moins 20 énergie (actuellement 10).');
    await choose(); await app.elements.gameCardDialogActions.children.find(n=>n.textContent==='Confirmer l’action').click(); await tick();
    assert.equal(app.animations.filter(a=>a.options.duration===650).length,1);
    assert.match(app.elements.gameCardDialogStatus.textContent,/Condition non remplie.*20/);
    assert.equal(app.elements.gameCardDialogStatus.classList.contains('is-error'),true);
});


test('fin : victoire et montants personnels réels, puis rechargement de partie terminée', async () => {
    const app = game();
    app.handlers.GameFinished({ winnerUserId: 'me', xpReward: 123, cardBalanceReward: 10, reason: 'LeaderDefeated' });
    assert.equal(app.elements.gameResultDialog.open, true);
    assert.equal(app.elements.gameResultTitle.textContent, 'Victoire !');
    assert.equal(app.elements.gameResultXp.textContent, '+123 XP');
    assert.equal(app.elements.gameResultMoney.textContent, '+10');
    await app.elements.gameResultClose.click();
    app.handlers.GameStateUpdated({ ...app.state, status: 3, winnerUserId: 'me', xpEarned: 123, cardBalanceEarned: 10 });
    assert.equal(app.elements.gameResultDialog.open, false);
    const reload = game();
    reload.handlers.GameStateUpdated({ ...reload.state, status: 3, winnerUserId: 'me', endReason: 1, xpEarned: 123, cardBalanceEarned: 10 });
    assert.equal(reload.elements.gameResultDialog.open, true);
    assert.equal(reload.elements.endTurnButton.disabled, true);
});

test('fin : défaite sans récompense adverse et ancienne partie sans montants inventés', () => {
    const app = game();
    app.handlers.GameFinished({ winnerUserId: 'opponent', xpReward: 100, cardBalanceReward: 10, reason: 'Surrender' });
    assert.equal(app.elements.gameResultTitle.textContent, 'Défaite');
    assert.equal(app.elements.gameResultXp.textContent, '+0 XP');
    assert.equal(app.elements.gameResultMoney.textContent, '+0');
    assert.match(app.elements.gameResultReason.textContent, /Vous avez abandonné/);
    const old = game();
    old.handlers.GameStateUpdated({ ...old.state, status: 3, winnerUserId: 'me' });
    assert.equal(old.elements.gameResultXp.textContent, '—');
});

test('guidage : tour, main adverse et capacité disponible sont recalés sur le serveur', () => {
    const app = game();
    assert.match(app.elements.gamePhase.textContent, /Tour 1/);
    assert.match(app.elements.gameOpponentHand.textContent, /5 carte/);
    app.handlers.GameStateUpdated({ ...app.state, turnNumber: 4, isMyTurn: false });
    assert.match(app.elements.gamePhase.textContent, /Tour 4.*adverse/);
});


test('main : décisions et lieux joués directement, sans popup', async () => {
    for (const type of [3,4]) {
        const app = game(); app.state.me.hand[0].type = type;
        app.handlers.GameStateUpdated(app.state); await app.select(); await tick();
        assert.equal(!!app.elements.gameCardDialog.open, false);
        assert.deepEqual(app.calls.find(c => c.name === 'PlayCard').args, ['match-1', 'hand-1', null]);
    }
});

test('glisser : dépôt dans le bon emplacement et annulation hors terrain sans paiement', async () => {
    const app = game(); app.dragTo(2); await tick();
    assert.deepEqual(app.calls.find(c => c.name === 'PlayCard').args, ['match-1','hand-1',2]);
    assert.equal(!!app.elements.gameCardDialog.open, false);
    const outside = game(); outside.dragTo(null); await tick();
    assert.equal(outside.calls.some(c => c.name === 'PlayCard'), false);
    const occupied = game(); occupied.state.me.characters[0] = occupied.state.me.leader;
    occupied.handlers.GameStateUpdated(occupied.state); occupied.dragTo(0); await tick();
    assert.equal(occupied.calls.some(c => c.name === 'PlayCard'), false);
    const cancel = game(); cancel.dragTo(2, true); await tick();
    assert.equal(cancel.calls.some(c => c.name === 'PlayCard'), false);
});

test('glisser : personnage avec cible attend la sélection et confirmation', async () => {
    const app = game(); app.state.me.hand[0].playTargetTypes = [22];
    app.state.opponent.leader.instanceId = 'enemy-leader';
    app.handlers.GameStateUpdated(app.state); app.dragTo(3); await tick();
    assert.equal(app.calls.some(c => c.name.startsWith('PlayCard')), false);
    assert.equal(!!app.elements.gameCardDialog.open, false);
    await app.elements.opponentLeader.children[0].click();
    await app.elements.gameCardDialogActions.children.find(n => n.textContent === 'Confirmer l’action').click(); await tick();
    assert.deepEqual(app.calls.find(c => c.name === 'PlayCardWithTarget').args,['match-1','hand-1',3,'enemy-leader']);
});

test('deck : focus en grand sans modifier la sélection, contrôles synchronisés et leader distinct', () => {
    const app=deckBuilder();
    app.state.leaders=[{cardId:100,name:'Leader',artType:1}];
    app.state.cardsGrouped.set(1,{name:'Carte',variants:[{cardId:1,ownedQuantity:1,cardNumber:'P01-1'},{cardId:2,ownedQuantity:4}]});
    app.focusCard(1);
    assert.match(app.get('worldsDeckFocusCard').innerHTML,/data-card-component="1"/);
    assert.equal(app.get('worldsDeckFocusName').textContent,'Carte');
    assert.equal(app.state.selectedCards.size,0);
    assert.equal(app.get('worldsDeckFocusAdd').disabled,false);
    assert.equal(app.get('worldsDeckFocusRemove').disabled,true);
    app.increaseVariant(1);
    assert.equal(app.get('worldsDeckFocusAdd').disabled,true);
    assert.equal(app.get('worldsDeckFocusRemove').disabled,false);
    assert.equal(app.get('worldsDeckFocusQuantity').textContent,'1 dans le deck');
    app.decreaseVariant(1);
    app.state.selectedCards.set(2,4);app.updateFocusControls();
    assert.equal(app.get('worldsDeckFocusAdd').disabled,true);
    app.focusCard(100);
    assert.equal(app.get('worldsDeckFocusQuantity').textContent,'Leader');
    assert.equal(app.get('worldsDeckFocusAdd').disabled,true);
    assert.equal(app.get('worldsDeckFocusRemove').disabled,true);
    app.focusCard(999);
    assert.equal(app.state.focusCardId,100);
});

test('deck : survol, clavier et clic inspectent ; ajout et retrait depuis les panneaux', async () => {
    const app=deckBuilder();app.attachEventListeners();
    app.state.cardsGrouped.set(1,{name:'Carte',variants:[{cardId:1,ownedQuantity:4}]});
    const target={closest: selector => selector==='[data-focus-card]' ? {dataset:{focusCard:'1'}} : null};
    for (const event of ['mouseover','focusin','click']) {
        app.state.focusCardId=null;
        app.get('worldsDeckModal').listeners[event]({target});
        assert.equal(app.state.focusCardId,1);
        assert.equal(app.state.selectedCards.size,0);
    }
    await app.get('worldsDeckFocusAdd').click();
    assert.equal(app.state.selectedCards.get(1),1);
    assert.match(app.get('worldsDeckPreviewCards').innerHTML,/data-remove-card="1"/);
    const remove={closest: selector => selector==='[data-remove-card]' ? {dataset:{removeCard:'1'}} : null};
    app.get('worldsDeckModal').listeners.click({target:remove});
    assert.equal(app.state.selectedCards.size,0);
});

function gameFocus() {
    const elements=new Map(['gamePage','gameCardFocus','gameCardFocusVisual','gameCardFocusStatus'].map(id=>[id,new Element()]));
    const docEvents={}, windowEvents={}, timers=new Map();let sequence=0, observer, dialogOpen=false, hydrated=0;
    const popup=elements.get('gameCardFocus');popup.hidden=true;popup.offsetWidth=340;popup.offsetHeight=480;
    const context={document:{getElementById:id=>elements.get(id),createElement:()=>new Element(),querySelector:()=>dialogOpen?{}:null,addEventListener:(type,fn)=>docEvents[type]=fn},
        window:{innerWidth:1000,innerHeight:800,worldsCardComponents:{hydrate(){hydrated++;}},setTimeout(fn){timers.set(++sequence,fn);return sequence;},clearTimeout(id){timers.delete(id);},addEventListener:(type,fn)=>windowEvents[type]=fn},
        MutationObserver:class {constructor(fn){observer=fn;}observe(){}}};
    vm.runInNewContext(script('game-card-focus.js'),context);
    const card=new Element();card.contains=other=>other===card;card.getBoundingClientRect=()=>({left:800,right:900,top:650});
    card.querySelector=selector=>selector==='[data-card-component]'?{dataset:{cardComponent:'42'}}:selector==='.game-card-hp'?{textContent:'30/40'}:null;
    const target={closest:()=>card};
    return {popup,card,target,elements,docEvents,windowEvents,get hydrated(){return hydrated;},events:elements.get('gamePage').listeners,flush(){for(const [id,fn]of timers){timers.delete(id);fn();}},mutate(){observer();},setDialog(value){dialogOpen=value;}};
}
test('jeu : focus agrandi au survol et au clavier, position dans la fenêtre, sans sélection',()=>{
    const app=gameFocus();app.events.pointerover({target:app.target,type:'pointerover',pointerType:'mouse',buttons:0});
    assert.equal(app.popup.hidden,true);app.flush();assert.equal(app.popup.hidden,false);
    assert.equal(app.elements.get('gameCardFocusVisual').children[0].dataset.cardComponent,'42');
    assert.equal(app.elements.get('gameCardFocusStatus').textContent,'30/40');
    assert.equal(app.popup.style.left,'444px');assert.equal(app.popup.style.top,'312px');
    app.events.pointerout({relatedTarget:app.card});assert.equal(app.popup.hidden,false);
    app.events.pointerout({relatedTarget:null});assert.equal(app.popup.hidden,true);
    app.events.focusin({target:app.target,type:'focusin'});app.flush();assert.equal(app.popup.hidden,false);
    app.events.click();assert.equal(app.popup.hidden,true);
});
test('jeu : focus annulé pendant déplacement, popup ouverte, touche Échap et carte retirée',()=>{
    const app=gameFocus();const hover=()=>app.events.pointerover({target:app.target,type:'pointerover',pointerType:'mouse',buttons:0});
    hover();app.events.pointerdown();app.flush();assert.equal(app.popup.hidden,true);
    app.events.pointerover({target:app.target,pointerType:'touch'});app.flush();assert.equal(app.hydrated,0);
    hover();app.setDialog(true);app.flush();assert.equal(app.popup.hidden,true);
    app.setDialog(false);hover();app.flush();app.docEvents.keydown({key:'Escape'});assert.equal(app.popup.hidden,true);
    hover();app.flush();app.card.isConnected=false;app.mutate();assert.equal(app.popup.hidden,true);
});
