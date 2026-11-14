import { test } from 'node:test';
import assert from 'node:assert/strict';
import { configuration, prepareBundle } from './prepare-deploy.mjs';
import { mkdtempSync, readFileSync, existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
const valid = { APP_IMAGE:'ghcr.io/owner/app:'+'a'.repeat(40), APP_DOMAIN:'worlds-tcg.com', ACME_EMAIL:'admin@example.com',POSTGRES_DB:'worlds',POSTGRES_USER:'worlds',POSTGRES_PASSWORD:'fake', SMTP_HOST:'smtp-relay.brevo.com',SMTP_USERNAME:'fake',SMTP_PASSWORD:'fake',MAIL_FROM:'no-reply@worlds-tcg.com' };
test('configuration avec domaine expéditeur et port STARTTLS', () => {const s=configuration(valid);assert.match(s,/MAIL_DOMAIN='worlds-tcg.com'/);assert.match(s,/SMTP_PORT='587'/);});
test('secrets avec dollars et apostrophes préservés sans interpolation', () => {assert.ok(configuration({...valid,POSTGRES_PASSWORD:"a'$b${x}#;\\c"}).includes("POSTGRES_PASSWORD='a\\'$b${x}#;\\c'"));});
test('secrets absents rejetés sans révéler les valeurs',()=>assert.throws(()=>configuration({...valid,SMTP_PASSWORD:''}),/SMTP_PASSWORD/));
test('injection de lignes rejetée',()=>assert.throws(()=>configuration({...valid,POSTGRES_PASSWORD:'secret\nNEW=value'}),/POSTGRES_PASSWORD/));
test('URL et références arbitraires refusées',()=>{assert.throws(()=>configuration({...valid,APP_DOMAIN:'https://worlds-tcg.com'}));assert.throws(()=>configuration({...valid,APP_IMAGE:'app:latest'}));assert.throws(()=>configuration({...valid,SMTP_PORT:'465'}));});
const monitoring={...valid,MONITORING_ENABLED:'true',GRAFANA_ADMIN_PASSWORD:"test'$long-password",POSTGRES_MONITOR_PASSWORD:'test-password-long',ALERT_EMAIL:'admin@example.test',MONITORING_DISCORD_WEBHOOK_URL:'https://discord.com/api/webhooks/123/test-token'};
test('supervision facultative, validation stricte des secrets activés',()=>{
  assert.match(configuration(valid),/MONITORING_ENABLED='false'/);
  assert.match(configuration(monitoring),/GRAFANA_DOMAIN='admin.grafana.worlds-tcg.com'/);
  for (const key of ['GRAFANA_ADMIN_PASSWORD','POSTGRES_MONITOR_PASSWORD','ALERT_EMAIL','MONITORING_DISCORD_WEBHOOK_URL']) assert.throws(()=>configuration({...monitoring,[key]:''}),new RegExp(key));
  assert.throws(()=>configuration({...monitoring,MONITORING_DISCORD_WEBHOOK_URL:'https://example.com/hook'}),/WEBHOOK/);
  assert.throws(()=>configuration({...monitoring,GRAFANA_DOMAIN:valid.APP_DOMAIN}),/DOMAIN/);
  assert.throws(()=>configuration({...monitoring,GRAFANA_ADMIN_PASSWORD:'short'}),/16/);
});
test('bundle complet, paramètres Alertmanager privés et désactivation sans fichiers résiduels',()=>{
  const dir=mkdtempSync(join(tmpdir(),'worlds-monitoring-'));
  try {
    prepareBundle(monitoring,dir);
    const alert=JSON.parse(readFileSync(join(dir,'monitoring/alertmanager/config.yml'),'utf8'));
    assert.equal(alert.receivers[0].email_configs[0].to,monitoring.ALERT_EMAIL);
    assert.equal(alert.receivers[0].discord_configs[0].webhook_url,monitoring.MONITORING_DISCORD_WEBHOOK_URL);
    assert.equal(alert.global.smtp_smarthost,'smtp:587');
    assert.ok(existsSync(join(dir,'monitoring/caddy/grafana.caddy')));
    assert.match(readFileSync(join(dir,'monitoring/prometheus/prometheus.yml'),'utf8'),/https:\/\/admin.grafana.worlds-tcg.com\/api\/health/);
    assert.ok(existsSync(join(dir,'monitoring/grafana/dashboards/worlds-overview.json')));
    prepareBundle(valid,dir);
    assert.ok(!existsSync(join(dir,'monitoring/caddy/grafana.caddy')));
    assert.ok(!existsSync(join(dir,'monitoring/alertmanager/config.yml')));
  } finally {rmSync(dir,{recursive:true,force:true});}
});
