const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const script=name=>fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/js',name),'utf8');
function setup({muted=false,unsupported=false}={}) {
    const sources=[],stored={},contexts=[],intervals=new Map();
    const param=()=>({value:0,events:[],setValueAtTime(value,time){this.events.push({value,time});},exponentialRampToValueAtTime(value,time){this.events.push({value,time});},setTargetAtTime(value,time){this.events.push({value,time});}});
    const node=()=>({connect(){},disconnect(){}});
    const context={setInterval(fn){const id=intervals.size+1;intervals.set(id,fn);return id;},clearInterval(id){intervals.delete(id);},
        window:{localStorage:{getItem:()=>String(muted),setItem:(key,value)=>stored[key]=value},AudioContext:class {
            constructor(){this.state='suspended';this.currentTime=0;this.sampleRate=100;this.destination=node();contexts.push(this);}
            async resume(){this.state='running';}
            async close(){this.state='closed';}
            createGain(){return {...node(),gain:param()};}
            createConvolver(){return node();}
            createBuffer(channels,length){return {getChannelData:()=>new Float32Array(length)};}
            createStereoPanner(){return {...node(),pan:param()};}
            createBiquadFilter(){return {...node(),Q:param(),frequency:param()};}
            createBufferSource(){const source={...node(),start(time){this.started=time;},stop(time){this.stopTime=time;if(time===undefined){this.stopped=true;this.onended?.();}}};sources.push(source);return source;}
            createOscillator(){const source=this.createBufferSource();source.frequency=param();source.detune=param();return source;}
        }}};
    if(unsupported)delete context.window.AudioContext;
    vm.runInNewContext(script('booster-sounds.js'),context);
    return {audio:context.window.worldsBoosterSounds.create(),sources,stored,contexts,intervals};
}

test('sons : chaque rareté synthétise son accord et réutilise le contexte audio',()=>{
    for(const [rarity,firstNote] of Object.entries({C:659.25,R:659.25,SR:880,UR:523.25,ALT:1174.66,FULL_ART:587.33,LEADER:783.99,SECRET:392,inconnue:659.25})) {
        const app=setup();app.audio.reveal(rarity);
        assert.equal(app.sources[0].frequency.events[0].value,firstNote);
        assert.ok(app.sources.every(source=>Number.isFinite(source.started)&&source.stopTime>source.started));
        const count=app.sources.length;app.audio.reveal(rarity);
        assert.equal(app.contexts.length,1);assert.equal(app.sources.length,count*2);
    }
});

test('sons : préférence muette, activation et arrêt des sons',()=>{
    const app=setup({muted:true});app.audio.open();assert.equal(app.sources.length,0);
    app.audio.setMuted(false);assert.equal(app.contexts.length,1);assert.equal(app.contexts[0].state,'running');
    app.audio.open();assert.equal(app.sources[0].frequency.events[0].value,1046.5);
    const count=app.sources.length;app.audio.setMuted(true);assert.ok(app.sources.every(source=>source.stopped));
    app.audio.reveal('UR');assert.equal(app.sources.length,count);
    assert.equal(app.stored['worlds-booster-muted'],'true');
});

test('sons : passer ou quitter arrête les boucles et tous les sons programmés',()=>{
    const app=setup();app.audio.packRumble();app.audio.reveal('UR');app.audio.warpStart();app.audio.twinkleStart([{rarity:'UR',pan:0}]);
    assert.equal(app.intervals.size,1);app.audio.stop();
    assert.equal(app.intervals.size,0);assert.ok(app.sources.every(source=>source.stopped));
    const count=app.sources.length;app.audio.setWarpSpeed(1);assert.equal(app.sources.length,count);
    app.audio.dispose();assert.equal(app.contexts[0].state,'closed');
});

test('sons : sans Web Audio les effets restent silencieux sans bloquer l’ouverture',()=>{
    const app=setup({unsupported:true});
    for(const method of ['open','packRumble','tear','pop','cardRise','starBurst','starTint','twinkleStart','warpStart','zip','cardFlip','cardSlide','reveal','tensionRise','stopIntro','stop','dispose'])assert.doesNotThrow(()=>app.audio[method]());
    assert.equal(app.sources.length,0);assert.equal(app.intervals.size,0);
});

function opening({reducedMotion=false}={}) {
    const elements=new Map(),timers=[],calls=[];
    function node() {const classes=new Set(),children=new Map();return {dataset:{},style:{setProperty(){}},listeners:{},classList:{add(...names){names.forEach(n=>classes.add(n));},remove(...names){names.forEach(n=>classes.delete(n));},contains:name=>classes.has(name)},
        offsetWidth:320,offsetHeight:447,querySelector(selector){if(!children.has(selector))children.set(selector,node());return children.get(selector);},querySelectorAll(){return [];},addEventListener(type,fn){this.listeners[type]=fn;},setAttribute(){},getBoundingClientRect(){return {left:0,top:0,width:1200,height:800};},appendChild(){},remove(){}};}
    const get=selector=>{if(!elements.has(selector))elements.set(selector,node());return elements.get(selector);};
    get('.booster-opening').dataset={soundBaseUrl:'/sons/'};
    const slots=[{rarity:'Common',artType:'Normal'},{rarity:'Rare',artType:'FullArt'},{rarity:'SuperRare',artType:'AlternateArt'}].map(data=>Object.assign(node(),{dataset:data}));
    const audio={muted:false,open(){calls.push('open');},reveal(rarity){calls.push(rarity);},stop(){calls.push('stop');},dispose(){},setMuted(value){this.muted=value;}};
    const introCalls=[];
    for(const method of ['packRumble','tear','pop','cardRise','starBurst','starTint','twinkleStart','twinkleStop','warpStart','setWarpSpeed','warpStop','zip','cardFlip','cardSlide','tensionRise','stopIntro'])audio[method]=(...args)=>introCalls.push({method,args});
    const context={document:{querySelector:get,querySelectorAll:selector=>selector==='.booster-card-slot'?slots:[],getElementById:id=>get('#'+id),addEventListener(type,fn){if(type==='DOMContentLoaded')fn();},createElement:node},
        window:{innerWidth:1200,matchMedia:()=>({matches:reducedMotion}),addEventListener(){},setTimeout(fn){timers.push(fn);},worldsBoosterSounds:{create:()=>audio}},
        gsap:{set(){},killTweensOf(){},fromTo(){return {kill(){}};},to(){return {kill(){}};},timeline(){const chain={to(){return chain;},fromTo(){return chain;},set(){return chain;},call(){return chain;},kill(){}};return chain;}},console:{log(){},error(){}},setTimeout(){},clearTimeout(){}};
    const source=script('booster-opening.js').replace('function startOpening() {','globalThis.openingTest={showCard,playRevealSound,getAnimationRarity,finishInstantly,enterCollection}; function startOpening() {');
    vm.runInNewContext(source,context);
    return {calls,introCalls,slots,get,timers,api:context.openingTest};
}
test('booster : clic initial déverrouille le son, variantes prioritaires et récapitulatif silencieux',()=>{
    const app=opening();assert.deepEqual(app.calls,[]);
    app.get('#booster-pack').listeners.click();assert.deepEqual(app.calls,['open']);
    assert.ok(app.introCalls.some(call=>call.method==='packRumble'));
    assert.ok(app.slots.every(slot=>slot.classList.contains('is-face-down')),'éventail sans recto dévoilé');
    app.api.enterCollection();
    assert.ok(app.introCalls.some(call=>call.method==='stopIntro'));
    assert.deepEqual(app.calls,['open','C']);app.api.showCard(0);assert.deepEqual(app.calls,['open','C']);
    app.api.showCard(1);assert.equal(app.calls.at(-1),'FULL_ART');app.api.playRevealSound(app.slots[2]);assert.equal(app.calls.at(-1),'ALT');
    app.api.finishInstantly();assert.equal(app.calls.at(-1),'stop');app.api.playRevealSound(app.slots[2]);assert.equal(app.calls.at(-1),'stop');
    assert.ok(app.slots.every(slot=>!slot.classList.contains('is-face-down')),'récapitulatif face visible');
});
test('booster : passer dès le début annule l’introduction retardée sans jouer toutes les cartes',()=>{
    const app=opening();app.get('#booster-pack').listeners.click();app.api.finishInstantly();assert.deepEqual(app.calls,['open','stop']);
});

test('booster : passer avant ouverture affiche les cartes sans son de révélation',()=>{
    const app=opening();app.api.finishInstantly();assert.deepEqual(app.calls,['stop']);
    assert.ok(app.slots.every(slot=>!slot.classList.contains('is-face-down')));
});

test('booster : mouvement réduit passe directement à la découverte sans dévoiler les cartes suivantes',()=>{
    const app=opening({reducedMotion:true});app.get('#booster-pack').listeners.click();
    assert.deepEqual(app.calls,['open','C']);
    assert.equal(app.slots[0].classList.contains('is-face-down'),false);
    assert.ok(app.slots.slice(1).every(slot=>slot.classList.contains('is-face-down')));
});
