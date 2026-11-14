import { mkdirSync, writeFileSync, copyFileSync, cpSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
export function valuesFor(env) {
  const values = {};
  for (const key of ['APP_IMAGE','APP_DOMAIN','ACME_EMAIL','POSTGRES_DB','POSTGRES_USER','POSTGRES_PASSWORD','SMTP_HOST','SMTP_USERNAME','SMTP_PASSWORD','MAIL_FROM']) {
    const value = env[key];
    if (!value || /[\r\n\0]/.test(value)) throw new Error(`Paramètre manquant ou invalide : ${key}`);
    values[key] = value;
  }
  if (!/^[a-z0-9][a-z0-9.-]*\.[a-z]{2,}$/i.test(values.APP_DOMAIN)) throw new Error('APP_DOMAIN invalide');
  if (!/^ghcr\.io\/[a-z0-9_.\/-]+:[a-f0-9]{40}$/.test(values.APP_IMAGE)) throw new Error('APP_IMAGE invalide');
  if (!/^[a-z0-9.-]+$/i.test(values.SMTP_HOST)) throw new Error('SMTP_HOST invalide');
  if (!/^[^\s@]+@[a-z0-9.-]+\.[a-z]{2,}$/i.test(values.MAIL_FROM)) throw new Error('MAIL_FROM invalide');
  values.MAIL_DOMAIN = values.MAIL_FROM.split('@')[1];
  values.SMTP_PORT = env.SMTP_PORT || '587';
  if (!['587','2525'].includes(values.SMTP_PORT)) throw new Error('Utilisez un port SMTP STARTTLS : 587 ou 2525');
  values.MAIL_FROM_NAME = env.MAIL_FROM_NAME || 'Worlds TCG';
  if (/[\r\n\0]/.test(values.MAIL_FROM_NAME)) throw new Error('MAIL_FROM_NAME invalide');
  values.MONITORING_ENABLED = env.MONITORING_ENABLED || 'false';
  if (!['true','false'].includes(values.MONITORING_ENABLED)) throw new Error('MONITORING_ENABLED invalide');
  if (values.MONITORING_ENABLED === 'true') {
    for (const key of ['GRAFANA_ADMIN_PASSWORD','POSTGRES_MONITOR_PASSWORD','ALERT_EMAIL','MONITORING_DISCORD_WEBHOOK_URL']) {
      if (!env[key] || /[\r\n\0]/.test(env[key])) throw new Error(`Paramètre manquant ou invalide : ${key}`);
      values[key] = env[key];
    }
    for (const key of ['GRAFANA_ADMIN_PASSWORD','POSTGRES_MONITOR_PASSWORD'])
      if (values[key].length < 16) throw new Error(`${key} doit contenir au moins 16 caractères`);
    if (!/^[^\s@<>]+@[a-z0-9.-]+\.[a-z]{2,}$/i.test(values.ALERT_EMAIL)) throw new Error('ALERT_EMAIL invalide');
    if (!/^https:\/\/(?:discord\.com|discordapp\.com)\/api\/webhooks\/\d+\/[a-zA-Z0-9_-]+$/.test(values.MONITORING_DISCORD_WEBHOOK_URL)) throw new Error('MONITORING_DISCORD_WEBHOOK_URL invalide');
    if (!/^[a-zA-Z0-9_-]+$/.test(values.POSTGRES_DB)) throw new Error('POSTGRES_DB invalide pour le collecteur');
    values.GRAFANA_DOMAIN = env.GRAFANA_DOMAIN || `admin.grafana.${values.APP_DOMAIN}`;
    if (!/^[a-z0-9][a-z0-9.-]*\.[a-z]{2,}$/i.test(values.GRAFANA_DOMAIN) || values.GRAFANA_DOMAIN === values.APP_DOMAIN) throw new Error('GRAFANA_DOMAIN invalide');
    values.GRAFANA_ADMIN_USER = env.GRAFANA_ADMIN_USER || 'admin';
    if (!/^[a-zA-Z0-9_.-]{1,64}$/.test(values.GRAFANA_ADMIN_USER)) throw new Error('GRAFANA_ADMIN_USER invalide');
  }
  return values;
}
export function configuration(env) {
  return Object.entries(valuesFor(env)).map(([key,value]) => `${key}='${value.replace(/'/g,"\\'")}'`).join('\n')+'\n';
}
export function prepareBundle(env, directory='deployment-bundle') {
  const values=valuesFor(env);
  mkdirSync(directory,{recursive:true,mode:0o700});
  writeFileSync(join(directory,'runtime.env'),configuration(env),{mode:0o600});
  writeFileSync(join(directory,'registry-token'),env.GITHUB_TOKEN || '',{mode:0o600});
  for (const file of ['docker-compose.prod.yml','docker-compose.monitoring.yml','Caddyfile','remote-deploy.sh']) copyFileSync(`deploy/${file}`,join(directory,file));
  cpSync('deploy/monitoring',join(directory,'monitoring'),{recursive:true});
  // Reusing a bundle must not leave an active host or secret behind when disabled.
  rmSync(join(directory,'monitoring/caddy/grafana.caddy'),{force:true});
  rmSync(join(directory,'monitoring/alertmanager'),{force:true,recursive:true});
  if (values.MONITORING_ENABLED === 'true') {
    copyFileSync('deploy/monitoring/caddy/grafana.caddy.template',join(directory,'monitoring/caddy/grafana.caddy'));
    const config=readFileSync('deploy/monitoring/prometheus/prometheus.yml.template','utf8').replaceAll('APP_DOMAIN_PLACEHOLDER',values.APP_DOMAIN).replaceAll('GRAFANA_DOMAIN_PLACEHOLDER',values.GRAFANA_DOMAIN);
    writeFileSync(join(directory,'monitoring/prometheus/prometheus.yml'),config);
    mkdirSync(join(directory,'monitoring/alertmanager'),{recursive:true});
    // JSON is valid YAML and safely quotes passwords/webhooks/email punctuation.
    writeFileSync(join(directory,'monitoring/alertmanager/config.yml'),JSON.stringify({
      global:{smtp_smarthost:'smtp:587',smtp_from:values.MAIL_FROM,smtp_require_tls:false,resolve_timeout:'5m'},
      route:{receiver:'email-and-discord',group_by:['alertname','job'],group_wait:'30s',group_interval:'5m',repeat_interval:'4h'},
      receivers:[{name:'email-and-discord',email_configs:[{to:values.ALERT_EMAIL,send_resolved:true}],discord_configs:[{webhook_url:values.MONITORING_DISCORD_WEBHOOK_URL,send_resolved:true}]}],
      inhibit_rules:[{source_matchers:['severity="critical"'],target_matchers:['severity="warning"'],equal:['alertname','instance']}]
    },null,2),{mode:0o644});
  }
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  prepareBundle(process.env);
}
