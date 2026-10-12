const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const tick = () => new Promise(resolve => setImmediate(resolve));

function setup() {
    function element() { return { open:false, textContent:'', isConnected:true, listeners:{}, focused:0,
        addEventListener(name, callback) { this.listeners[name]=callback; },
        fire(name, event={}) { this.listeners[name]?.(event); },
        focus() { this.focused++; }, showModal() { this.open=true; }, close() { this.open=false; this.fire('close'); } }; }
    const elements = Object.fromEntries(['popupConfirmation','popupConfirmationTitle','popupConfirmationMessage','popupConfirmationCancel','popupConfirmationAccept'].map(id=>[id,element()]));
    const opener=element(), documentEvents={};
    const document={ activeElement:opener, getElementById:id=>elements[id], addEventListener:(name,handler)=>{documentEvents[name]=handler;} };
    const window={ confirm() { throw new Error('La confirmation native ne doit jamais être utilisée.'); } };
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/js/popup-confirmation.js'),'utf8'),{document,window});
    return { api:window.worldsConfirmation,elements,opener,documentEvents };
}
function event(form,submitter) { return { target:form,submitter,cancelled:false,stopped:false,
    preventDefault() { this.cancelled=true; }, stopImmediatePropagation() { this.stopped=true; } }; }

test('Popup : texte sécurisé, choix explicite et restauration du focus',async()=>{
    const app=setup();
    const pending=app.api.ask('<img src=x onerror=alert(1)>',{title:'Défausser',confirmText:'Défausser'});
    assert.equal(app.elements.popupConfirmation.open,true);
    assert.equal(app.elements.popupConfirmationMessage.textContent,'<img src=x onerror=alert(1)>');
    assert.equal(app.elements.popupConfirmationAccept.textContent,'Défausser');
    assert.equal(app.elements.popupConfirmationCancel.focused,1);
    app.elements.popupConfirmationAccept.fire('click');
    assert.equal(await pending,true); assert.equal(app.api.isOpen,false); assert.equal(app.opener.focused,1);
});
test('Popup : Annuler, Échap et clic sur le fond ne confirment aucune action',async()=>{
    for(const action of ['cancel','escape','backdrop']) {
        const app=setup(), pending=app.api.ask('Supprimer ?');
        if(action==='cancel') app.elements.popupConfirmationCancel.fire('click');
        if(action==='escape') app.elements.popupConfirmation.fire('cancel',{preventDefault(){}});
        if(action==='backdrop') app.elements.popupConfirmation.fire('click',{target:app.elements.popupConfirmation});
        assert.equal(await pending,false);
    }
});
test('Formulaire : aucune soumission avant acceptation, puis une seule avec le bouton original',async()=>{
    const app=setup(), submitter={name:'action',value:'delete'};
    let submitted=0;
    const form={isConnected:true, requestSubmit(button) { assert.equal(button,submitter); const resumed=event(form,button);
        if(app.api.confirmForm(resumed,'Supprimer ?')) submitted++; } };
    const first=event(form,submitter);
    assert.equal(app.api.confirmForm(first,'Supprimer ?'),false);
    app.api.confirmForm(event(form,submitter),'Supprimer ?');
    assert.equal(first.cancelled,true);assert.equal(first.stopped,true);assert.equal(submitted,0);
    app.elements.popupConfirmationAccept.fire('click');await tick();
    assert.equal(submitted,1);
});
test('Formulaire : refus, formulaire retiré et validation échouée ne soumettent rien',async()=>{
    const app=setup();let requests=0;
    const form={isConnected:true,requestSubmit(){requests++;}};
    app.api.confirmForm(event(form),'Supprimer ?');app.elements.popupConfirmationCancel.fire('click');await tick();
    assert.equal(requests,0);
    app.api.confirmForm(event(form),'Supprimer ?');form.isConnected=false;app.elements.popupConfirmationAccept.fire('click');await tick();
    assert.equal(requests,0);
    form.isConnected=true;
    // Native validation can reject requestSubmit before it emits another submit event.
    app.api.confirmForm(event(form),'Supprimer ?');app.elements.popupConfirmationAccept.fire('click');await tick();
    assert.equal(requests,1);
    app.api.confirmForm(event(form),'Supprimer ?');assert.equal(app.api.isOpen,true);
    app.elements.popupConfirmationCancel.fire('click');await tick();
});
test('Popup : fermeture ancienne et raccourcis du jeu ne ferment pas une nouvelle confirmation',async()=>{
    const app=setup();const first=app.api.ask('Première');app.elements.popupConfirmationAccept.fire('click');await first;
    const second=app.api.ask('Deuxième');app.elements.popupConfirmation.fire('close');assert.equal(app.api.isOpen,true);
    let stopped=false;app.elements.popupConfirmation.fire('keydown',{stopPropagation(){stopped=true;}});assert.equal(stopped,true);
    app.elements.popupConfirmationCancel.fire('click');assert.equal(await second,false);
});
test('Toutes les confirmations applicatives utilisent le composant commun',()=>{
    const root=path.join(__dirname,'../web-tcg');
    const files=[];
    function scan(directory) { for(const entry of fs.readdirSync(directory,{withFileTypes:true})) {
        const file=path.join(directory,entry.name);
        if(entry.isDirectory()) scan(file); else if(/\.(js|cshtml)$/.test(entry.name)) files.push(file);
    } }
    scan(path.join(root,'Views'));scan(path.join(root,'wwwroot/js'));
    for(const file of files) assert.doesNotMatch(fs.readFileSync(file,'utf8'),/\b(?:window\.)?confirm\s*\(/,file);
    assert.match(fs.readFileSync(path.join(root,'Views/Shared/_Layout.cshtml'),'utf8'),/Component.InvokeAsync\("PopupConfirmation"\)/);
});
