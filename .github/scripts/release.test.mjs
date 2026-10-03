import test from 'node:test';
import assert from 'node:assert/strict';
import { changelog, discordPayload, publish } from './release.mjs';

const sha = 'a'.repeat(40);
const release = { id: 10, tag_name: 'v1.2.0', body: '## Changements\n- Nouvelle carte', html_url: 'https://github.com/demo/tcg/releases/tag/v1.2.0', published_at: '2026-10-03T10:00:00Z', prerelease: false };
const env = { GITHUB_TOKEN: 'test-only', GITHUB_REPOSITORY: 'demo/tcg', RELEASE_TAG: 'v1.2.0', GITHUB_ACTOR: 'demo', DISCORD_WEBHOOK_URL: 'https://discord.com/api/webhooks/123/test-only' };
const git = (...args) => args[0] === 'rev-parse' ? sha : args[0] === 'describe' ? 'v1.1.0' : args[0] === 'log' ? `${sha}\tfeat: Nouvelle carte` : '';
const response = (body, status = 200) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

test('changelog : nouveautés, corrections, liens et plage entre tags', () => {
    const body = changelog([{ sha, subject: 'feat(cards): Nouvelle carte' },{ sha, subject: 'fix: Image [PNG]' }], 'https://github.com/demo/tcg','v1.0.0','v1.1.0');
    assert.match(body,/✨ Nouveautés/); assert.match(body,/🐛 Corrections/);
    assert.match(body,/compare\/v1.0.0\.\.\.v1.1.0/); assert.match(body,/commit\/a{40}/);
    assert.match(body,/\\\[PNG\\\]/);
});

test('changelog : première version et tags sans nouveaux commits', () => {
    assert.match(changelog([], 'https://github.com/demo/tcg',null,'v1'),/Aucun nouveau commit/);
    assert.match(changelog([], 'https://github.com/demo/tcg',null,'v1'),/commits\/v1/);
});

test('embed : notes longues raccourcies, couleurs, liens et mentions désactivées', () => {
    const payload = discordPayload({ ...release,body:'A'.repeat(10000),prerelease:true },'demo/tcg','demo');
    assert.ok(payload.embeds[0].description.length < 4096);
    assert.match(payload.embeds[0].description,/changelog complet/);
    assert.equal(payload.embeds[0].color,0xf39c12);
    assert.deepEqual(payload.allowed_mentions,{parse:[]});
    assert.equal(payload.embeds[0].url,release.html_url);
});

test('publication : release créée puis embed confirmé et marqueur enregistré', async () => {
    const calls=[];
    const request=async (url,options) => {
        const path=String(url); calls.push({ path,options });
        if (path.includes('/releases/tags/')) return response({},404);
        if (path.endsWith('/generate-notes')) return response({body:'Notes GitHub'});
        if (path.endsWith('/releases') && options.method==='POST') return response({...release,body:JSON.parse(options.body).body},201);
        if (path.includes('discord.com')) return response({id:'987654321'});
        return response(release);
    };
    await publish(env,request,git);
    const created=JSON.parse(calls.find(c=>c.path.endsWith('/releases')).options.body);
    assert.match(created.body,/Nouvelle carte/); assert.match(created.body,/Notes GitHub/);
    const discord=calls.find(c=>c.path.includes('discord.com'));
    assert.match(discord.path,/wait=true/);
    assert.equal(discord.options.headers.Authorization,undefined);
    assert.match(calls.at(-1).options.body,/discord-notification: 987654321/);
});

test('reprise : une release annoncée ne produit aucun doublon Discord', async () => {
    let count=0;
    await publish(env,async()=>{count++; return response({...release,body:release.body+'\n<!-- discord-notification: 123 -->'});},git);
    assert.equal(count,1);
});

test('secret absent : release disponible sans appel Discord', async () => {
    let count=0;
    await publish({...env,DISCORD_WEBHOOK_URL:''},async()=>{count++;return response(release);},git);
    assert.equal(count,1);
});

test('refus Discord : erreur exploitable, secret masqué et release non marquée', async () => {
    const calls=[];
    await assert.rejects(publish(env,async(url)=>{calls.push(String(url));return String(url).includes('discord.com')?response({},403):response(release);},git),/Discord : erreur HTTP 403/);
    assert.equal(calls.length,2);
});

test('URL invalide : aucune transmission en dehors de Discord', async () => {
    let count=0;
    await assert.rejects(publish({...env,DISCORD_WEBHOOK_URL:'https://example.com/secret'},async()=>{count++;return response(release);},git),/webhook Discord valide/);
    assert.equal(count,1);
});
