// Isolated containers only. No real SMTP, Discord webhook, DNS change or certificate request.
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { prepareBundle } from '../.github/scripts/prepare-deploy.mjs';
const dir=mkdtempSync(join(tmpdir(),'worlds-monitoring-check-'));
const project=`worlds-monitoring-check-${process.pid}`;
const docker=(args)=>execFileSync('docker',args,{encoding:'utf8',maxBuffer:8*1024*1024,stdio:['ignore','pipe','pipe']});
const file=join(dir,'test.json');
const compose=(...args)=>docker(['compose','-p',project,'-f',file,...args]);
const image=process.env.TEST_APP_IMAGE || 'worlds-tcg:monitoring-check';
async function eventually(fn, label, attempts=60) {
  let error;
  for(let i=0;i<attempts;i++) {try {return await fn();}catch(e){error=e;await delay(1000);}}
  throw new Error(`${label}: ${error?.message}`);
}
function port(service,target) {return Number(compose('port',service,String(target)).trim().split(':').at(-1));}
async function get(url,options={}) {const response=await fetch(url,{...options,signal:AbortSignal.timeout(5000)});assert.ok(response.ok,`${url}: HTTP ${response.status}`);return response;}
try {
  prepareBundle({APP_IMAGE:'ghcr.io/test/app:'+'a'.repeat(40),APP_DOMAIN:'worlds-tcg.test',ACME_EMAIL:'admin@example.test',POSTGRES_DB:'monitoring',POSTGRES_USER:'checks',POSTGRES_PASSWORD:'test-password',SMTP_HOST:'smtp.example.test',SMTP_USERNAME:'test',SMTP_PASSWORD:'test',MAIL_FROM:'no-reply@example.test',MONITORING_ENABLED:'true',GRAFANA_ADMIN_PASSWORD:'test-password-long',POSTGRES_MONITOR_PASSWORD:"monitor'$long-password",ALERT_EMAIL:'admin@example.test',MONITORING_DISCORD_WEBHOOK_URL:'https://discord.com/api/webhooks/123/test-token'},dir);
  const mon=join(dir,'monitoring');
  // Native parsers validate the production configurations before test-only overrides.
  docker(['run','--rm','-v',`${join(mon,'prometheus')}:/etc/prometheus:ro`,'--entrypoint','promtool','prom/prometheus:v3.16.0','check','config','/etc/prometheus/prometheus.yml']);
  docker(['run','--rm','-v',`${join(mon,'alertmanager')}:/etc/alertmanager:ro`,'--entrypoint','amtool','prom/alertmanager:v0.35.0','check-config','/etc/alertmanager/config.yml']);
  docker(['run','--rm','-v',`${mon}:/config:ro`,'grafana/alloy:v1.21.3','validate','/config/config.alloy']);
  docker(['run','--rm','-v',`${mon}:/config:ro`,'grafana/loki:3.7.0','-config.file=/config/loki.yml','-verify-config=true']);
  docker(['run','--rm','-e','APP_DOMAIN=worlds-tcg.test','-e','GRAFANA_DOMAIN=admin.grafana.worlds-tcg.test','-e','ACME_EMAIL=admin@example.test','-v',`${join(dir,'Caddyfile')}:/etc/caddy/Caddyfile:ro`,'-v',`${join(mon,'caddy')}:/etc/caddy/monitoring:ro`,'caddy:2-alpine','caddy','adapt','--config','/etc/caddy/Caddyfile','--validate']);
  console.log('Configurations Prometheus, alertes, Alloy, Loki et Caddy valides.');
  const config=JSON.parse(docker(['compose','--env-file',join(dir,'runtime.env'),'-f',join(dir,'docker-compose.prod.yml'),'-f',join(dir,'docker-compose.monitoring.yml'),'config','--format','json']));
  // Compose config escapes dollars for subsequent interpolation; preserve them in this new file.
  for(const [name,network] of Object.entries(config.networks)) {delete network.name;delete network.ipam;}
  for(const volume of Object.values(config.volumes)) delete volume.name;
  for(const service of Object.values(config.services)) {
    for(const network of Object.values(service.networks || {})) if(network) delete network.ipv4_address;
    delete service.restart;
  }
  delete config.services.caddy; // No host ports 80/443, ACME or real domain on test machine.
  delete config.services.smtp;
  // Host rootfs propagation is a Linux-host feature; exercise it in the Linux CI.
  if(process.platform==='win32') delete config.services['node-exporter'];
  delete config.services['web-tcg'].depends_on.smtp;
  config.services['web-tcg'].image=image;
  config.services['uploads-init'].image=image;
  config.services['web-tcg'].ports=['127.0.0.1::8080','127.0.0.1::9091'];
  config.services.grafana.ports=['127.0.0.1::3000'];
  config.services.prometheus.ports=['127.0.0.1::9090'];
  // Disable real receivers after validating their schema above.
  writeFileSync(join(mon,'alertmanager/config.yml'),JSON.stringify({route:{receiver:'sink'},receivers:[{name:'sink'}]}));
  writeFileSync(join(mon,'config.alloy'),readFileSync(join(mon,'config.alloy'),'utf8').replace('com.docker.compose.project=worlds-tcg',`com.docker.compose.project=${project}`));
  writeFileSync(file,JSON.stringify(config));
  compose('up','-d','--wait','--wait-timeout','420');
  const app=`http://127.0.0.1:${port('web-tcg',8080)}`;
  const metrics=`http://127.0.0.1:${port('web-tcg',9091)}/metrics`;
  await get(`${app}/health`);
  const publicMetrics=await fetch(`${app}/metrics`,{redirect:'manual'});
  assert.notEqual(publicMetrics.status,200,'le port public ne sert pas les métriques');
  const text=await eventually(async()=>{const value=await (await get(metrics)).text();assert.match(value,/worlds_tcg_business_metrics_last_success_timestamp_seconds [1-9]/);return value;},'métriques métier');
  assert.match(text,/http_requests_received_total/);
  assert.match(text,/worlds_tcg_db_commands_total/);
  assert.ok(!text.includes('test-password'));
  console.log('Application : port de métriques privé, HTTP et requêtes EF instrumentés.');
  const grafana=`http://127.0.0.1:${port('grafana',3000)}`;
  const auth={Authorization:`Basic ${Buffer.from('admin:test-password-long').toString('base64')}`};
  const dashboards=await eventually(async()=>{const value=await (await get(`${grafana}/api/search`,{headers:auth})).json();assert.equal(value.filter(x=>x.type==='dash-db').length,5);return value;},'5 tableaux de bord provisionnés');
  assert.ok(dashboards.some(x=>x.uid==='worlds-logs'));
  assert.equal((await fetch(`${grafana}/api/search`)).status,401,'Grafana nécessite une authentification');
  console.log('Grafana : authentification et cinq tableaux de bord vérifiés.');
  const prom=`http://127.0.0.1:${port('prometheus',9090)}`;
  async function query(expr) {return (await (await get(`${prom}/api/v1/query?query=${encodeURIComponent(expr)}`)).json()).data.result;}
  for(const dashboard of readdirSync(join(mon,'grafana/dashboards'))) {
    const definition=JSON.parse(readFileSync(join(mon,'grafana/dashboards',dashboard),'utf8'));
    for(const panel of definition.panels) if(panel.datasource?.uid==='prometheus') for(const target of panel.targets) await query(target.expr);
  }
  await eventually(async()=>{const value=await query('pg_up');assert.equal(value[0]?.value[1],'1');},'exporteur PostgreSQL',90);
  await eventually(async()=>{const value=await query('up{job="application"}');assert.equal(value[0]?.value[1],'1');},'scrape application');
  await eventually(async()=>{const value=await query('container_memory_working_set_bytes{container_label_com_docker_compose_service!=""}');assert.ok(value.length>0);},'métriques par conteneur',90);
  console.log('Prometheus : métriques application, PostgreSQL et conteneurs collectées.');
  const role=compose('exec','-T','postgres','psql','-U','checks','-d','monitoring','-Atc',"SELECT rolsuper::text||','||rolcreatedb::text||','||rolcreaterole::text FROM pg_roles WHERE rolname='worlds_monitor'").trim();
  assert.equal(role,'false,false,false');
  // Verify the password update path and idempotency on an existing database.
  compose('run','--rm','postgres-monitor-init');
  const socketStatus=(path,...args)=>docker(['run','--rm','--network',`${project}_docker-observer`,'--entrypoint','curl',image,'--silent','--output','/dev/null','--write-out','%{http_code}',...args,`http://docker-socket-proxy:2375/${path}`]).trim();
  assert.equal(socketStatus('containers/json'),'200');
  assert.equal(socketStatus('containers/create','--request','POST','--data','{}','--header','Content-Type: application/json'),'403');
  // Query Loki through its Grafana datasource proxy: validates ingestion, not just startup.
  await eventually(async()=>{const value=await (await get(`${grafana}/api/datasources/proxy/uid/loki/loki/api/v1/query_range?query=${encodeURIComponent('{job="worlds-tcg",service="web-tcg"}')}&limit=1`,{headers:auth})).json();assert.ok(value.data.result.length>0);},'journaux collectés',90);
  const grafanaId=compose('ps','-q','grafana').trim();
  const grafanaState=JSON.parse(docker(['inspect','--format','{{json .State}}',grafanaId]));
  assert.equal(grafanaState.Health.Status,'healthy');
  assert.equal(grafanaState.OOMKilled,false);
  assert.equal(docker(['inspect','--format','{{.RestartCount}}',grafanaId]).trim(),'0','Grafana doit rester stable pendant la collecte');
  console.log('Supervision : métriques privées, PostgreSQL sans superutilisateur, 5 dashboards authentifiés, logs reçus et API Docker en lecture seule vérifiés.');
} catch(error) {
  try {console.error(compose('logs','--tail','50','cadvisor').split('\n').filter(x=>!x.includes('Machine:')&&!x.includes('Version:')&&!x.includes('Filesystem partitions:')).join('\n'));console.error(compose('logs','--tail','5','alloy','postgres-exporter','grafana','web-tcg'));}catch{}
  throw error;
} finally {
  try {compose('down','-v','--remove-orphans');}catch{}
  rmSync(dir,{recursive:true,force:true});
}
