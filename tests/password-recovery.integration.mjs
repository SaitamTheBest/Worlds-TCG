import assert from 'node:assert/strict';
const base=process.env.RECOVERY_TEST_URL || 'http://localhost:18085';
const inbox=process.env.MAILPIT_TEST_URL || 'http://localhost:18025';
const cookies=new Map();
async function request(path,options={}) {
  const response=await fetch(base+path,{...options,redirect:'manual',headers:{Cookie:[...cookies].map(([k,v])=>`${k}=${v}`).join('; '),...options.headers}});
  for(const cookie of response.headers.getSetCookie()) {const entry=cookie.split(';')[0];const index=entry.indexOf('=');cookies.set(entry.slice(0,index),entry.slice(index+1));}
  return response;
}
function value(html,name) {
 const input=[...html.matchAll(/<input\b[^>]*>/g)].map(m=>m[0]).find(s=>s.includes(`name="${name}"`));
 assert.ok(input,`champ ${name} présent`);
 return (input.match(/value="([^"]*)"/)?.[1] || '').replace(/&amp;/g,'&').replace(/&#x([0-9a-f]+);/gi,(_,v)=>String.fromCodePoint(parseInt(v,16)));
}
async function form(path,fields) {
 const html=await (await request(path)).text();
 return request(path.split('?')[0],{method:'POST',body:new URLSearchParams({...fields,__RequestVerificationToken:value(html,'__RequestVerificationToken')})});
}
assert.equal((await request('/health')).status,200);
assert.equal((await request('/Auth/ForgotPassword',{method:'POST',body:new URLSearchParams({Email:'missing@example.test'})})).status,400,'CSRF refusé');
const username='recovery'+Date.now();const address=username+'@example.test';
const auth=await (await request('/Auth')).text();
const signup=await request('/Auth/Register',{method:'POST',headers:{'Content-Type':'application/json',RequestVerificationToken:value(auth,'__RequestVerificationToken')},body:JSON.stringify({Username:username,Email:address,Password:'before123',ConfirmPassword:'before123'})});
assert.equal(signup.status,200);assert.ok((await signup.json()).success);
// Repartir sans session : le lien fonctionne pour un utilisateur non connecté.
cookies.clear();
const confirmation=await form('/Auth/ForgotPassword',{Email:address});assert.equal(confirmation.status,200);
const knownBody=await confirmation.text();
let message;
for(let attempt=0;attempt<30;attempt++) {
 const list=await (await fetch(inbox+'/api/v1/messages')).json();
 const match=list.messages?.find(m=>m.To?.some(r=>r.Address===address));
 if(match) {message=await (await fetch(inbox+'/api/v1/message/'+match.ID)).json();break;}
 await new Promise(resolve=>setTimeout(resolve,500));
}
assert.ok(message,'mail reçu via Postfix et Mailpit');
const link=(message.Text || '').match(/https:\/\/worlds-tcg\.com\/Auth\/ResetPassword\?[^\s]+/)?.[0];
assert.ok(link,'lien HTTPS présent dans le mail texte');
assert.ok(message.HTML.includes('Choisir mon mot de passe'),'mail HTML présent');
const url=new URL(link);const path=url.pathname+url.search;
const resetHtml=await (await request(path)).text();
const resetFields={UserId:value(resetHtml,'UserId'),Token:value(resetHtml,'Token'),Password:'after123',ConfirmPassword:'after123'};
const reset=await form(path,resetFields);assert.equal(reset.status,302);assert.equal(reset.headers.get('location'),'/Auth/ResetComplete');
const replay=await form(path,resetFields);assert.equal(replay.status,200);assert.match(await replay.text(),/Ce lien est invalide/);
const unknown=await form('/Auth/ForgotPassword',{Email:'missing@example.test'});assert.equal(unknown.status,200);assert.equal(await unknown.text(),knownBody,'confirmation identique sans révéler le compte');
const throttled=await form('/Auth/ForgotPassword',{Email:address});assert.equal(throttled.status,429);
const loginPage=await (await request('/Auth')).text();
const login=async password=>request('/Auth/Login',{method:'POST',headers:{'Content-Type':'application/json',RequestVerificationToken:value(loginPage,'__RequestVerificationToken')},body:JSON.stringify({Username:username,Password:password})});
assert.equal((await login('before123')).status,401);assert.equal((await login('after123')).status,200);
console.log('HTTP/SMTP : CSRF, mail HTML/texte via Postfix, réinitialisation, lien à usage unique, confidentialité, limite IP et connexion vérifiés.');
