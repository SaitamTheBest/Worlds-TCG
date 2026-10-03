import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { configuration } from '../.github/scripts/prepare-deploy.mjs';
const directory=mkdtempSync(join(tmpdir(),'worlds-deploy-check-'));
const password="fake'$money${x}#;\\end";
try {
 const file=join(directory,'runtime.env');
 writeFileSync(file,configuration({APP_IMAGE:'ghcr.io/owner/app:'+'a'.repeat(40),APP_DOMAIN:'worlds-tcg.com',ACME_EMAIL:'test@example.test',POSTGRES_DB:'test',POSTGRES_USER:'test',POSTGRES_PASSWORD:password,SMTP_HOST:'smtp-relay.brevo.com',SMTP_USERNAME:'fake',SMTP_PASSWORD:'fake',MAIL_FROM:'no-reply@worlds-tcg.com'}));
 const result=JSON.parse(execFileSync('docker',['compose','--env-file',file,'-f','deploy/docker-compose.prod.yml','config','--format','json'],{encoding:'utf8'}));
 // Compose échappe les dollars dans sa représentation sérialisée ; les conteneurs reçoivent un dollar.
 assert.equal(result.services.postgres.environment.POSTGRES_PASSWORD.replace(/\$\$/g,'$'),password);
 assert.deepEqual(Object.keys(result.services).filter(key=>result.services[key].ports?.length),['caddy']);
 assert.equal(result.services['web-tcg'].environment.ReverseProxy__KnownProxy,result.services.caddy.networks.proxy.ipv4_address);
 assert.equal(result.services.caddy.networks.proxy.ipv4_address,'172.30.42.2');
 assert.equal(result.networks.proxy.ipam.config[0].ip_range,'172.30.42.128/25');
 assert.equal(result.services['uploads-init'].network_mode,'none');
 assert.deepEqual(Object.keys(result.services.caddy.networks),['proxy']);
 assert.deepEqual(Object.keys(result.services.postgres.networks),['default']);
 assert.deepEqual(Object.keys(result.services.smtp.networks),['default']);
 assert.deepEqual(Object.keys(result.services['web-tcg'].networks).sort(),['default','proxy']);
 assert.equal(result.services.smtp.environment.POSTFIX_smtp_tls_security_level,'verify');
 console.log('Compose production : secrets avec caractères spéciaux, ports privés et proxy approuvé vérifiés.');
} finally { rmSync(directory,{recursive:true,force:true}); }
