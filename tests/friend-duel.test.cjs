const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const path=require('node:path');
const tick=()=>new Promise(resolve=>setImmediate(resolve));
function setup(status=0) {
    const now=Date.now(),elements={friendDuel:{dataset:{id:'invitation',status:String(status),expires:new Date(now+300000).toISOString()}},friendDuelCountdown:{},friendDuelConnection:{hidden:true}};
    const intervals=[],handlers={},navigations=[],requests=[];let response={status,expiresAt:new Date(now+300000).toISOString(),serverTime:new Date(now).toISOString()},fail=false;
    const context={document:{getElementById:id=>elements[id]},window:{location:{assign:url=>navigations.push(url),reload:()=>navigations.push('reload')},setInterval:(fn,ms)=>{intervals.push({fn,ms});return intervals.length;},clearInterval:id=>intervals[id-1].cleared=true,addEventListener:(name,fn)=>handlers[name]=fn},
        fetch:async(url,options)=>{requests.push({url,options});return {ok:!fail,json:async()=>response};}};
    vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../web-tcg/wwwroot/js/friend-duel.js'),'utf8'),context);
    return {elements,intervals,handlers,navigations,requests,setResponse(value){response=value;},fail(value){fail=value;},poll:()=>intervals.find(i=>i.ms===2000).fn()};
}
test('duel : attente et compte à rebours serveur, lancement automatique de la même partie',async()=>{
    const app=setup();await tick();assert.match(app.elements.friendDuelCountdown.textContent,/^0[45]:/);
    assert.equal(app.requests[0].url,'/Friends/Duel/invitation/Status');assert.equal(app.requests[0].options.cache,'no-store');
    app.setResponse({status:2,matchId:'match-42'});await app.poll();assert.deepEqual(app.navigations,['/Game/Play/match-42']);
    app.handlers.pagehide();assert.equal(app.intervals.every(i=>i.cleared),true);
});
test('duel : acceptation, refus et expiration actualisent l’écran',async()=>{
    for(const status of [1,3,4,5]) {const app=setup();await tick();app.setResponse({status});await app.poll();assert.deepEqual(app.navigations,['reload']);}
});
test('duel : interruption réseau visible, reprise et arrêt après départ de la page',async()=>{
    const app=setup(1);await tick();app.fail(true);await app.poll();assert.equal(app.elements.friendDuelConnection.hidden,false);
    assert.deepEqual(app.navigations,[]);app.fail(false);await app.poll();assert.equal(app.elements.friendDuelConnection.hidden,true);
    app.handlers.pagehide();const count=app.requests.length;await app.poll();assert.equal(app.requests.length,count);
});
