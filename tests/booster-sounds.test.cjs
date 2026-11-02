const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const script=name=>fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/js',name),'utf8');
const tick=()=>new Promise(resolve=>setImmediate(resolve));
function setup({muted=false,fail=false,decodePending=false}={}) {
    const requests=[],played=[],sources=[],stored={},contexts=[];let release;
    const context={fetch:async url=>{requests.push(url);return {ok:!fail,arrayBuffer:async()=>({url,slice(){return this;}})};},
        window:{localStorage:{getItem:()=>String(muted),setItem:(key,value)=>stored[key]=value},AudioContext:class {
            constructor(){this.state='suspended';contexts.push(this);}
            async resume(){this.state='running';}
            async close(){this.state='closed';}
            async decodeAudioData(data){if(decodePending)await new Promise(resolve=>release=resolve);return data;}
            createGain(){return {gain:{},connect(){},disconnect(){}};}
            createBufferSource(){const source={connect(){},disconnect(){},start(){played.push(this.buffer.url);},stop(){this.stopped=true;this.onended?.();}};sources.push(source);return source;}
        }}};
    vm.runInNewContext(script('booster-sounds.js'),context);
    return {audio:context.window.worldsBoosterSounds.create('/sons/'),requests,played,sources,stored,contexts,release:()=>release?.()};
}
test('sons : chaque rareté utilise le WAV correspondant, chaque fichier est chargé une seule fois',async()=>{
    const app=setup();
    for(const [rarity,file] of Object.entries({C:'common',R:'rare',SR:'super-rare',UR:'ultra-rare',ALT:'alternate-art',FULL_ART:'full-art',LEADER:'rare',SECRET:'full-art'})) {
        await app.audio.reveal(rarity);assert.equal(app.played.at(-1),`/sons/reveal-${file}.wav`);
    }
    await app.audio.reveal('inconnue');assert.equal(app.played.at(-1),'/sons/reveal-common.wav');
    assert.equal(app.requests.length,6);assert.equal(app.sources.slice(0,-1).every(source=>source.stopped),true);
});
test('sons : ouverture, préférence muette et reprise après activation dans le geste utilisateur',async()=>{
    const app=setup({muted:true});await app.audio.open();assert.equal(app.played.length,0);
    app.audio.setMuted(false);assert.equal(app.contexts.length,1);assert.equal(app.contexts[0].state,'running');
    await app.audio.open();assert.equal(app.played[0],'/sons/reveal-common.wav');
    app.audio.setMuted(true);assert.equal(app.sources[0].stopped,true);await app.audio.reveal('UR');assert.equal(app.played.length,1);
    assert.equal(app.stored['worlds-booster-muted'],'true');
});
test('sons : passer ou quitter annule aussi un son dont le décodage est encore en attente',async()=>{
    const app=setup({decodePending:true});const pending=app.audio.reveal('UR');await tick();app.audio.stop();app.release();await pending;
    assert.equal(app.played.length,0);app.audio.dispose();assert.equal(app.contexts[0].state,'closed');
});
test('sons : un fichier absent ne bloque pas la découverte et les six ressources sont bien des WAV',async()=>{
    const app=setup({fail:true});await app.audio.open();await app.audio.reveal('ALT');assert.equal(app.played.length,0);
    for(const file of fs.readdirSync(path.join(__dirname,'../web-tcg/wwwroot/sons'))) {
        const data=fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/sons',file));
        assert.equal(data.toString('ascii',0,4),'RIFF');assert.equal(data.toString('ascii',8,12),'WAVE');
    }
});

function opening() {
    const elements=new Map(),timers=[],calls=[];
    function node() {const classes=new Set();return {dataset:{},style:{setProperty(){}},listeners:{},classList:{add(...names){names.forEach(n=>classes.add(n));},remove(...names){names.forEach(n=>classes.delete(n));},contains:name=>classes.has(name)},
        offsetWidth:320,offsetHeight:447,querySelector(){return node();},addEventListener(type,fn){this.listeners[type]=fn;},setAttribute(){},getBoundingClientRect(){return {left:0,top:0,width:1200,height:800};},appendChild(){},remove(){}};}
    const get=selector=>{if(!elements.has(selector))elements.set(selector,node());return elements.get(selector);};
    get('.booster-opening').dataset={soundBaseUrl:'/sons/'};
    const slots=[{rarity:'Common',artType:'Normal'},{rarity:'Rare',artType:'FullArt'},{rarity:'SuperRare',artType:'AlternateArt'}].map(data=>Object.assign(node(),{dataset:data}));
    const audio={muted:false,open(){calls.push('open');},reveal(rarity){calls.push(rarity);},stop(){calls.push('stop');},dispose(){},setMuted(value){this.muted=value;}};
    const context={document:{querySelector:get,querySelectorAll:selector=>selector==='.booster-card-slot'?slots:[],getElementById:id=>get('#'+id),addEventListener(type,fn){if(type==='DOMContentLoaded')fn();},createElement:node},
        window:{innerWidth:1200,addEventListener(){},setTimeout(fn){timers.push(fn);},worldsBoosterSounds:{create:()=>audio}},
        gsap:{set(){},killTweensOf(){},fromTo(){},to(){},timeline(){const chain={to(){return chain;},set(){return chain;},call(){return chain;}};return chain;}},console:{log(){},error(){}},setTimeout(){},clearTimeout(){}};
    const source=script('booster-opening.js').replace('function startOpening() {','globalThis.openingTest={showCard,playRevealSound,getAnimationRarity,skipOpening}; function startOpening() {');
    vm.runInNewContext(source,context);
    return {calls,slots,get,timers,api:context.openingTest};
}
test('booster : clic initial déverrouille le son, variantes prioritaires et récapitulatif silencieux',()=>{
    const app=opening();assert.deepEqual(app.calls,[]);
    app.get('#booster-start-opening').listeners.click();assert.deepEqual(app.calls,['open']);app.timers[0]();
    assert.deepEqual(app.calls,['open','C']);app.api.showCard(0);assert.deepEqual(app.calls,['open','C']);
    app.api.showCard(1);assert.equal(app.calls.at(-1),'FULL_ART');app.api.playRevealSound(app.slots[2]);assert.equal(app.calls.at(-1),'ALT');
    app.api.skipOpening();assert.equal(app.calls.at(-1),'stop');app.api.playRevealSound(app.slots[2]);assert.equal(app.calls.at(-1),'stop');
});
test('booster : passer dès le début annule l’introduction retardée sans jouer toutes les cartes',()=>{
    const app=opening();app.get('#booster-start-opening').listeners.click();app.api.skipOpening();app.timers[0]();assert.deepEqual(app.calls,['open','stop']);
});
