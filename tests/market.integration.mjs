import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';

// Isolated test application and database only; never point at a production container.
const base=process.env.MARKET_TEST_URL || 'http://127.0.0.1:18096';
const container=process.env.MARKET_TEST_CONTAINER || 'tcg-market-checks';
assert.match(container,/^tcg-market-checks(?:-[a-z0-9-]+)?$/);
const sql=text=>execFileSync('docker',['exec','-i',container,'psql','-U','checks','-d','market_checks','-v','ON_ERROR_STOP=1','-tA'],{input:text,encoding:'utf8'}).trim();
function client() {
  const cookies=new Map();
  return async (path,options={})=>{
    const response=await fetch(base+path,{...options,redirect:'manual',headers:{Cookie:[...cookies].map(([k,v])=>`${k}=${v}`).join('; '),...options.headers}});
    for(const cookie of response.headers.getSetCookie()) { const entry=cookie.split(';')[0], index=entry.indexOf('='); cookies.set(entry.slice(0,index),entry.slice(index+1)); }
    return response;
  };
}
const token=html=>html.match(/name="__RequestVerificationToken"[^>]*value="([^"]+)"/)?.[1];
const name='market'+Date.now();
const seller=client(), buyer=client();
async function signup(request,username) {
  const html=await (await request('/Auth')).text();
  const response=await request('/Auth/Register',{method:'POST',headers:{'Content-Type':'application/json',RequestVerificationToken:token(html)},body:JSON.stringify({Username:username,Email:username+'@example.test',Password:'test123',ConfirmPassword:'test123'})});
  assert.equal(response.status,200); assert.equal((await response.json()).success,true);
}
assert.equal((await client()('/Market')).status,302,'marché inaccessible sans authentification');
await signup(seller,name); await signup(buyer,name+'buyer');
sql(`UPDATE "AspNetUsers" SET "CardBalance"=100 WHERE "UserName"='${name}buyer';
INSERT INTO "UserCardCollections" ("UserId","CardId","Quantity","AcquiredDate","Condition","IsForSale")
SELECT "Id",(SELECT MIN("Id") FROM "Cards"),2,NOW(),'Mint',FALSE FROM "AspNetUsers" WHERE "UserName"='${name}';`);
for(const path of ['/Market','/Market/MySales','/Market/History']) {
  const response=await seller(path); assert.equal(response.status,200,path); assert.match(await response.text(),/data-market-list/);
}
assert.equal((await buyer('/Inventory/AddCard',{method:'POST'})).status,302,'ajout arbitraire de cartes interdit à un joueur');
const create=await (await seller('/Market/Create')).text();
assert.match(create,/tcg-card/,'sélection avec le composant Card');
const cardId=Number(create.match(/name="Input.CardId" value="(\d+)"/)?.[1]); assert.ok(cardId);
const fields={'Input.CardId':cardId,'Input.StartingPrice':'10','Input.DurationHours':'1'};
assert.equal((await seller('/Market/Create',{method:'POST',body:new URLSearchParams(fields)})).status,400,'création protégée contre le CSRF');
const published=await seller('/Market/Create',{method:'POST',body:new URLSearchParams({...fields,__RequestVerificationToken:token(create)})});
if(published.status!==302) console.log((await published.text()).match(/<div[^>]*validation-summary[^>]*>[\s\S]*?<\/div>/)?.[0]);
assert.equal(published.status,302,'vente créée par le vrai formulaire MVC');
const path=published.headers.get('location'); assert.match(path,/^\/Market\/Auction\/[a-f0-9-]+$/);
const id=path.split('/').at(-1);
const detail=await (await buyer(path)).text(); assert.match(detail,/tcg-card/); assert.match(detail,/>10<\/strong>/); assert.match(detail,/tcg-market-currency/); assert.match(detail,/tcg-card-balance-unit/);
const buyerToken=token(detail);
const negotiate=await buyer('/hubs/market/negotiate?negotiateVersion=1',{method:'POST'});
assert.equal(negotiate.status,200);
const {connectionToken}=await negotiate.json(); assert.ok(connectionToken);
const abort=new AbortController();
const stream=await buyer('/hubs/market?id='+encodeURIComponent(connectionToken),{headers:{Accept:'text/event-stream'},signal:abort.signal});
assert.equal(stream.status,200);
const reader=stream.body.getReader(); let events='';
const consume=(async()=>{try {while(true){const {value,done}=await reader.read();if(done)break;events+=new TextDecoder().decode(value);}}catch(error){if(error.name!=='AbortError')throw error;}})();
await buyer('/hubs/market?id='+encodeURIComponent(connectionToken),{method:'POST',headers:{'Content-Type':'text/plain'},body:'{"protocol":"json","version":1}\u001e'});
const bid=await buyer(path+'/Bid',{method:'POST',headers:{'Content-Type':'application/json',RequestVerificationToken:buyerToken},body:JSON.stringify({amount:20})});
assert.equal(bid.status,200,'enchère enregistrée');
const updated=await (await buyer(path+'/State')).text(); assert.match(updated,/data-minimum="21"/); assert.match(updated,/>80<\/strong>/); assert.doesNotMatch(updated,/id="userCardBalance"|id="cardBalanceWidget"/,'la vue marché ne duplique pas les identifiants de la topbar');
for(let i=0;i<30&&!events.includes('MarketChanged');i++)await new Promise(resolve=>setTimeout(resolve,100));
assert.ok(events.includes('MarketChanged'),'mise à jour reçue sur une vraie connexion SignalR');
sql(`UPDATE "MarketAuctions" SET "EndsAt"=NOW()-INTERVAL '1 second' WHERE "Id"='${id}';`);
let closed='';
for(let i=0;i<30;i++){closed=await (await seller(path+'/State')).text();if(closed.includes('Vente termin'))break;await new Promise(resolve=>setTimeout(resolve,200));}
assert.match(closed,/Vente termin/,'clôture effectuée par le service en arrière-plan');
assert.match(await (await seller('/Market/History')).text(),/Vendue/,'vente visible dans l’historique');
assert.equal(Number(sql(`SELECT "CardBalance" FROM "AspNetUsers" WHERE "UserName"='${name}';`)),20,'vendeur payé');
assert.equal(sql(`SELECT "Quantity" FROM "UserCardCollections" WHERE "CardId"=${cardId} AND "UserId"=(SELECT "Id" FROM "AspNetUsers" WHERE "UserName"='${name}buyer');`),'1','carte transférée au gagnant');
abort.abort(); await consume;
console.log('Marché HTTP/SignalR : pages, composant Card, formulaires, CSRF, enchère, événement en direct, clôture et transfert vérifiés.');
