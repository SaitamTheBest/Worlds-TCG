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

function bulkSelection(approved=false) {
    const nodes=new Map();const node=id=>{if(!nodes.has(id))nodes.set(id,{listeners:{},hidden:false,disabled:false,children:[],textContent:'',classList:{toggle(){}},setAttribute(){},addEventListener(type,fn){this.listeners[type]=fn;},replaceChildren(){this.children=[];},appendChild(child){this.children.push(child);}});return nodes.get(id);};
    const choices=[2,5].map((quantity,index)=>{const article=node('article'+index),label=node('label'+index);const choice={checked:false,value:String(index+11),dataset:{quantity:String(quantity),name:'Carte '+index},addEventListener(type,fn){this.change=fn;},closest:selector=>selector==='label'?label:article};article.querySelector=()=>choice;return choice;});
    node('inventorySelectionGrid').querySelectorAll=()=>choices;const messages=[];
    const document={getElementById:node,createElement:()=>({}),addEventListener(type,fn){fn();}};
    const window={worldsConfirmation:{confirmForm(event,message){messages.push(message);if(!approved)event.preventDefault();return approved;}},addEventListener(){}};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/js/inventory-selection.js'),'utf8'),{document,window});
    return {node,choices,messages,submit(){const event={cancelled:false,preventDefault(){this.cancelled=true;}};node('inventoryBulkDiscard').listeners.submit(event);return event;},toggle(){node('inventorySelectionToggle').listeners.click();}};
}
test('Sélection : activation, tout sélectionner, quantités complètes et annulation de la confirmation',()=>{
    const app=bulkSelection();app.toggle();
    assert.equal(app.node('inventorySelectionDiscard').disabled,true);
    app.node('inventorySelectionAll').listeners.click();
    assert.equal(app.node('inventorySelectionDiscard').disabled,false);
    assert.match(app.node('inventorySelectionCount').textContent,/2 carte.*7 exemplaire.*7 ¤/);
    assert.equal(app.submit().cancelled,true);
    assert.equal(app.node('inventorySelectionDiscard').disabled,false);
    assert.match(app.messages[0],/Carte 0 ×2/);assert.match(app.messages[0],/Carte 1 ×5/);
    assert.deepEqual(app.node('inventorySelectionInputs').children.map(input=>[input.name,input.value]),[['items[0].Id','11'],['items[0].Quantity','2'],['items[1].Id','12'],['items[1].Quantity','5']]);
    app.toggle();assert.equal(app.choices.some(choice=>choice.checked),false);assert.equal(app.node('inventorySelectionDiscard').hidden,true);
});
test('Sélection : clic sur carte, acceptation et refus d’une sélection vide',()=>{
    const app=bulkSelection(true);assert.equal(app.submit().cancelled,true);assert.equal(app.messages.length,0);app.toggle();
    const article=app.choices[0].closest('.inventory-card-item');const link={closest:()=>article};let prevented=false;
    app.node('inventorySelectionGrid').listeners.click({target:{closest:()=>link},preventDefault(){prevented=true;}});
    assert.equal(prevented,true);assert.equal(app.choices[0].checked,true);
    assert.equal(app.submit().cancelled,false);assert.equal(app.node('inventorySelectionDiscard').disabled,true);
    assert.equal(app.node('inventorySelectionInputs').children.length,2);
});
