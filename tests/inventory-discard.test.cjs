const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

function setup(approved = true) {
    const input = { value: '1', invalid: false, reportValidity() { this.invalid = true; } };
    const button = { disabled: false };
    const listeners = {}, messages = [];
    const form = { dataset: { owned: '3', cardName: 'Carte variante' },
        querySelector: selector => selector.includes('quantity') ? input : button,
        addEventListener: (name, callback) => { listeners[name] = callback; } };
    const pageHandlers = {};
    const document = {
        querySelectorAll: () => [form],
        addEventListener: (name, callback) => { if (name === 'DOMContentLoaded') callback(); }
    };
    const window = {
        worldsConfirmation: { confirmForm(event, message) { messages.push(message); if (!approved) event.preventDefault(); return approved; } },
        addEventListener: (name, callback) => { pageHandlers[name] = callback; }
    };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../web-tcg/wwwroot/js/inventory-discard.js'), 'utf8'), { document, window });
    return { input, button, messages, submit() { const event={ cancelled:false, preventDefault() { this.cancelled=true; } }; listeners.submit(event); return event; } };
}

test('Défausse : annuler la confirmation ne soumet pas le formulaire', () => {
    const app = setup(false);
    assert.equal(app.submit().cancelled, true);
    assert.equal(app.button.disabled, false);
    assert.match(app.messages[0], /Carte variante.*1 ¤/);
});
test('Défausse : confirmation de la quantité et avertissement pour la dernière copie', () => {
    const app = setup(); app.input.value = '3';
    assert.equal(app.submit().cancelled, false);
    assert.equal(app.button.disabled, true);
    assert.match(app.messages[0], /3 ¤/);
    assert.match(app.messages[0], /tous vos exemplaires.*deck/);
});
test('Défausse : quantités hors possession ou décimales refusées avant confirmation', () => {
    for (const value of ['0', '-1', '4', '1.5', 'abc']) {
        const app = setup(); app.input.value = value;
        assert.equal(app.submit().cancelled, true);
        assert.equal(app.messages.length, 0);
        assert.equal(app.input.invalid, true);
    }
});
