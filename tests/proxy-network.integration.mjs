import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { configuration } from '../.github/scripts/prepare-deploy.mjs';
const directory=mkdtempSync(join(tmpdir(),'worlds-proxy-check-'));
const project=`worlds-proxy-check-${process.pid}`;
const envFile=join(directory,'runtime.env');
const oldFile=join(directory,'old.json');const newFile=join(directory,'new.json');
const docker=(args)=>execFileSync('docker',args,{encoding:'utf8',stdio:['ignore','pipe','pipe']});
const compose=(file,...args)=>docker(['compose','-p',project,'-f',file,...args]);
try {
 writeFileSync(envFile,configuration({APP_IMAGE:'ghcr.io/owner/app:'+'a'.repeat(40),APP_DOMAIN:'worlds-tcg.com',ACME_EMAIL:'test@example.test',POSTGRES_DB:'test',POSTGRES_USER:'test',POSTGRES_PASSWORD:'fake',SMTP_HOST:'smtp-relay.brevo.com',SMTP_USERNAME:'fake',SMTP_PASSWORD:'fake',MAIL_FROM:'no-reply@worlds-tcg.com'}));
 const resolved=JSON.parse(docker(['compose','--env-file',envFile,'-f','deploy/docker-compose.prod.yml','config','--format','json']));
 // Garder la topologie de production, avec des conteneurs inertes : aucun certificat,
 // aucune base ni aucun mail réel ; aucun port ni volume publié.
 const stub=(networks)=>({image:'caddy:2-alpine',entrypoint:['sh','-c','sleep 300'],networks});
 const current={services:{},networks:resolved.networks};
 for(const name of ['postgres','smtp','web-tcg','caddy']) current.services[name]=stub(resolved.services[name].networks);
 for(const network of Object.values(current.networks)) delete network.name;
 const old={services:{postgres:current.services.postgres,smtp:current.services.smtp,'web-tcg':stub({default:current.services['web-tcg'].networks.default}),caddy:stub({default:{ipv4_address:'172.30.40.2'}})},networks:{default:current.networks.default}};
 writeFileSync(oldFile,JSON.stringify(old));writeFileSync(newFile,JSON.stringify(current));
 compose(oldFile,'up','-d','postgres');
 const backend=()=>JSON.parse(docker(['network','inspect',`${project}_default`]))[0];
 const database=Object.values(backend().Containers).find(c=>c.Name===`${project}-postgres-1`);
 assert.equal(database.IPv4Address,'172.30.40.2/24');
 compose(oldFile,'up','-d','smtp','web-tcg');
 assert.throws(()=>compose(oldFile,'up','-d','caddy'),/Address already in use/,'ancien conflit reproduit');
 const databaseId=docker(['inspect','--format','{{.Id}}',`${project}-postgres-1`]).trim();
 compose(newFile,'up','-d');
 assert.equal(docker(['inspect','--format','{{.Id}}',`${project}-postgres-1`]).trim(),databaseId,'conteneur backend conservé');
 const proxy=JSON.parse(docker(['network','inspect',`${project}_proxy`]))[0];
 const caddy=Object.values(proxy.Containers).find(c=>c.Name===`${project}-caddy-1`);
 const app=Object.values(proxy.Containers).find(c=>c.Name===`${project}-web-tcg-1`);
 assert.equal(caddy.IPv4Address,'172.30.42.2/24');
 assert.ok(Number(app.IPv4Address.split('/')[0].split('.').at(-1))>=128);
 assert.ok(!Object.values(backend().Containers).some(c=>c.Name===`${project}-caddy-1`));
 docker(['exec',`${project}-caddy-1`,'ping','-c','1','-W','3','web-tcg']);
 console.log('Docker : ancien conflit reproduit, migration sans recréer PostgreSQL, IP réservée et accès Caddy/application vérifiés.');
} finally {
 try { compose(newFile,'down','--remove-orphans'); } finally { rmSync(directory,{recursive:true,force:true}); }
}
