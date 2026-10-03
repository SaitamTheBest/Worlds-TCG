import { test } from 'node:test';
import assert from 'node:assert/strict';
import { configuration } from './prepare-deploy.mjs';
const valid = { APP_IMAGE:'ghcr.io/owner/app:'+'a'.repeat(40), APP_DOMAIN:'worlds-tcg.com', ACME_EMAIL:'admin@example.com',POSTGRES_DB:'worlds',POSTGRES_USER:'worlds',POSTGRES_PASSWORD:'fake', SMTP_HOST:'smtp-relay.brevo.com',SMTP_USERNAME:'fake',SMTP_PASSWORD:'fake',MAIL_FROM:'no-reply@worlds-tcg.com' };
test('configuration avec domaine expéditeur et port STARTTLS', () => {const s=configuration(valid);assert.match(s,/MAIL_DOMAIN='worlds-tcg.com'/);assert.match(s,/SMTP_PORT='587'/);});
test('secrets avec dollars et apostrophes préservés sans interpolation', () => {assert.ok(configuration({...valid,POSTGRES_PASSWORD:"a'$b${x}#;\\c"}).includes("POSTGRES_PASSWORD='a\\'$b${x}#;\\c'"));});
test('secrets absents rejetés sans révéler les valeurs',()=>assert.throws(()=>configuration({...valid,SMTP_PASSWORD:''}),/SMTP_PASSWORD/));
test('injection de lignes rejetée',()=>assert.throws(()=>configuration({...valid,POSTGRES_PASSWORD:'secret\nNEW=value'}),/POSTGRES_PASSWORD/));
test('URL et références arbitraires refusées',()=>{assert.throws(()=>configuration({...valid,APP_DOMAIN:'https://worlds-tcg.com'}));assert.throws(()=>configuration({...valid,APP_IMAGE:'app:latest'}));assert.throws(()=>configuration({...valid,SMTP_PORT:'465'}));});
