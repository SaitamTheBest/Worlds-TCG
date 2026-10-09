const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

function element(value = '') {
    return { value, hidden: false, disabled: false, textContent: '', listeners: {}, options: [],
        add(option) { this.options.push(option); },
        addEventListener(event, callback) { this.listeners[event] = callback; },
        fire(event) { this.listeners[event]?.(); } };
}
function side() {
    const controls = Object.fromEntries(['search', 'extension', 'previous', 'next', 'page', 'empty'].map(key => [`[data-trade-${key}]`, element()]));
    controls['[data-selected-count]'] = element();
    const cards = Array.from({ length: 14 }, (_, index) => {
        const quantity = element('0');
        return { dataset: { extension: index < 8 ? 'P02' : 'P01', search: `${index === 0 ? 'Élisabeth' : 'Carte'} P02-${index + 1}` }, quantity,
            querySelector(selector) { return selector === '.tcg-card' ? {} : quantity; } };
    });
    return { cards, controls, querySelector: selector => controls[selector], querySelectorAll: () => cards,
        visible() { return cards.filter(card => !card.hidden); } };
}
function setup() {
    const sides = [side(), side()];
    const context = { window: {}, Option: function(text, value) { this.text = text; this.value = value; } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../web-tcg/wwwroot/js/friends-trade-pagination.js'), 'utf8'), context);
    context.window.worldsTradePagination.init({ querySelectorAll: () => sides });
    return sides;
}

test('Six cartes par page, navigation indépendante et quantités conservées', () => {
    const [given, received] = setup();
    assert.equal(given.visible().length, 6);
    given.cards[0].quantity.value = '3';
    given.cards[0].quantity.fire('input');
    given.controls['[data-trade-next]'].fire('click');
    assert.equal(given.visible()[0], given.cards[6]);
    assert.equal(received.visible()[0], received.cards[0]);
    assert.equal(given.controls['[data-selected-count]'].textContent, '3 exemplaire(s) choisi(s)');
    given.controls['[data-trade-previous]'].fire('click');
    assert.equal(given.cards[0].quantity.value, '3');
});

test('Filtre extension et recherche par nom sans accent ou numéro', () => {
    const [given] = setup();
    const extension = given.controls['[data-trade-extension]'];
    assert.deepEqual(extension.options.map(option => option.value), ['P01', 'P02']);
    extension.value = 'P01'; extension.fire('change');
    assert.equal(given.visible().length, 6);
    assert.ok(given.visible().every(card => card.dataset.extension === 'P01'));
    extension.value = ''; extension.fire('change');
    const search = given.controls['[data-trade-search]'];
    search.value = 'elisabeth'; search.fire('input');
    assert.equal(given.visible()[0], given.cards[0]);
    assert.equal(given.visible().length, 1);
    search.value = 'P02-14'; search.fire('input');
    assert.equal(given.visible()[0], given.cards[13]);
    search.value = 'introuvable'; search.fire('input');
    assert.equal(given.visible().length, 0);
    assert.equal(given.controls['[data-trade-empty]'].hidden, false);
});

test('Une quantité invalide sur une page masquée redevient visible pour la validation', () => {
    const [given] = setup();
    given.cards[13].quantity.value = '999999';
    given.cards[13].quantity.fire('invalid');
    assert.ok(given.visible().includes(given.cards[13]));
    assert.equal(given.cards[13].quantity.value, '999999');
});
