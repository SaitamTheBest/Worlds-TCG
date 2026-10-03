import { mkdirSync, writeFileSync, copyFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
export function configuration(env) {
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
  return Object.entries(values).map(([key,value]) => `${key}='${value.replace(/'/g,"\\'")}'`).join('\n')+'\n';
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  mkdirSync('deployment-bundle',{recursive:true,mode:0o700});
  writeFileSync('deployment-bundle/runtime.env',configuration(process.env),{mode:0o600});
  writeFileSync('deployment-bundle/registry-token',process.env.GITHUB_TOKEN,{mode:0o600});
  for (const file of ['docker-compose.prod.yml','Caddyfile','remote-deploy.sh']) copyFileSync(`deploy/${file}`,`deployment-bundle/${file}`);
}
