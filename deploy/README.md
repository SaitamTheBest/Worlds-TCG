# Déploiement OVH — Debian 12, worlds-tcg.com

Chaque push sur `master` lance les tests, construit une image GHCR portant le SHA du commit, puis déploie via SSH. Les PR et `main` sont vérifiés sans déployer. Le lancement manuel du workflow sur `master` permet de relancer. Les déploiements sont sérialisés ; en cas de plusieurs pushes rapides, GitHub peut remplacer un déploiement encore en attente par le plus récent.

## 1. Préparer le VPS

Sur votre ordinateur, créez une clé dédiée (sans passphrase, destinée uniquement à Actions) :

```sh
ssh-keygen -t ed25519 -f worlds-tcg-deploy -C github-actions-worlds-tcg
```

Connectez-vous au VPS avec le compte administrateur fourni par OVH. Transférez `setup-vps.sh`, puis exécutez `sudo bash setup-vps.sh`. Le script conserve Docker préinstallé et vérifie que Docker Compose est disponible. Sinon, installez le plugin Compose avec le dépôt officiel : https://docs.docker.com/engine/install/debian/ . Ajoutez le contenu de `worlds-tcg-deploy.pub` dans `/home/deploy/.ssh/authorized_keys`, puis :

```sh
sudo chown deploy:deploy /home/deploy/.ssh/authorized_keys
sudo chmod 600 /home/deploy/.ssh/authorized_keys
```

Le compte `deploy` appartient au groupe Docker, qui donne des droits équivalents à root. Réservez-le à cette clé. Testez une nouvelle connexion `ssh -i worlds-tcg-deploy deploy@IP_DU_VPS` avant de fermer la session administrateur.

Ports entrants nécessaires : SSH TCP 22 (ou votre port configuré), HTTP TCP 80, HTTPS TCP 443. Aucune publication de PostgreSQL, SMTP ou du port applicatif 8080. Sur le VPS, après avoir confirmé le port SSH :

```sh
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

Remplacez 22 si SSH utilise un autre port AVANT d'activer UFW. Autorisez les mêmes ports dans tout pare-feu réseau OVH activé. Les publications Docker peuvent contourner UFW : conservez seulement 80 et 443 dans Compose. Sorties nécessaires : DNS, HTTPS pour les images/certificats et SMTP TCP 587 vers Brevo. Sources : https://docs.docker.com/engine/network/packet-filtering-firewalls/ . Les sous-réseaux Docker sont `172.30.40.0/24` (backend) et `172.30.42.0/24` (proxy). Vérifiez qu'ils ne chevauchent aucun réseau/VPN existant. Caddy possède `172.30.42.2` ; les adresses automatiques du réseau proxy utilisent uniquement `172.30.42.128/25`. Le backend existant est conservé lors de la mise à jour.

## 2. Domaine et HTTPS

Dès la livraison de `worlds-tcg.com`, dans OVH → zone DNS, créez/remplacez l'enregistrement **A**, sous-domaine vide (`@`), vers l'IPv4 du VPS. Supprimez un ancien AAAA s'il ne correspond pas à une IPv6 opérationnelle du VPS. Vérifiez avec `nslookup worlds-tcg.com`. Caddy délivre et renouvelle automatiquement le certificat ; le premier déploiement attend que le DNS et les ports soient opérationnels. `www` n'est pas configuré dans cette version : utilisez `https://worlds-tcg.com`.

Guide OVH : https://docs.ovhcloud.com/fr/guides/web-cloud/domains/dns-zone-a-record-creation . Caddy et SignalR/WebSocket fonctionnent via `reverse_proxy`, avec une seule adresse proxy approuvée par ASP.NET.

## 3. Service mail

Créez un compte Brevo, activez l'envoi transactionnel, ajoutez `worlds-tcg.com` comme domaine expéditeur et `no-reply@worlds-tcg.com` comme expéditeur. Recopiez dans OVH les enregistrements de validation, DKIM et DMARC indiqués par Brevo. Ne devinez pas les valeurs DNS et ne créez pas deux enregistrements SPF distincts. Attendez la validation du domaine et l'activation du compte transactionnel.

Dans **SMTP et API → SMTP**, récupérez le login SMTP affiché et générez une **clé SMTP**, pas une clé API. Serveur `smtp-relay.brevo.com`, port **587**. Le relais Postfix privé dans Docker transmet les mails à Brevo avec STARTTLS et vérification du certificat ; aucun port SMTP n'est ouvert au public. Ce service permet l'envoi, pas la réception de mails dans une boîte `no-reply`.

Sources : https://help.brevo.com/hc/en-us/articles/7924908994450-Send-transactional-emails-using-Brevo-SMTP et https://help.brevo.com/hc/en-us/articles/7959631848850-Create-and-manage-your-SMTP-keys .

## 4. Secrets GitHub

Dans le dépôt → **Settings → Environments**, créez `production` et autorisez seulement `master`. Ajoutez les **Environment secrets** ci-dessous. Sans approbateur obligatoire, le déploiement est automatique.

| Secret | Valeur à fournir |
| --- | --- |
| `VPS_HOST` | IPv4 du VPS (reste utilisable avant propagation DNS) |
| `VPS_PORT` | `22`, ou votre port SSH |
| `VPS_USER` | `deploy` |
| `VPS_SSH_PRIVATE_KEY` | Contenu entier du fichier privé `worlds-tcg-deploy`, avec BEGIN/END et sauts de ligne |
| `VPS_SSH_KNOWN_HOSTS` | Ligne de clé d'hôte SSH vérifiée, voir ci-dessous |
| `APP_DOMAIN` | `worlds-tcg.com` sans protocole ni chemin |
| `ACME_EMAIL` | Votre adresse de contact pour les certificats |
| `POSTGRES_DB` | `worlds_tcg` |
| `POSTGRES_USER` | `worlds_tcg` |
| `POSTGRES_PASSWORD` | Mot de passe long aléatoire, par exemple généré avec `openssl rand -hex 32` |
| `SMTP_HOST` | `smtp-relay.brevo.com` |
| `SMTP_PORT` | `587` |
| `SMTP_USERNAME` | Login SMTP affiché par Brevo |
| `SMTP_PASSWORD` | Clé SMTP Brevo |
| `MAIL_FROM` | `no-reply@worlds-tcg.com`, expéditeur validé |
| `MAIL_FROM_NAME` | Facultatif : `Worlds TCG` |

`GITHUB_TOKEN` est fourni automatiquement par GitHub : aucun PAT à créer. Le package GHCR doit autoriser ce dépôt dans ses paramètres d'accès Actions si un package existe déjà sans lien avec le dépôt. `DISCORD_WEBHOOK_URL` reste un **Repository secret** pour le workflow de release, indépendant du déploiement.

Pour `VPS_SSH_KNOWN_HOSTS`, obtenez la clé avec `ssh-keyscan -p 22 -t ed25519 IP_DU_VPS`. Comparez son empreinte (`ssh-keygen -lf fichier_known_hosts`) à celle obtenue depuis une console OVH de confiance avec `sudo ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`. Le scan seul n'authentifie pas le serveur. Recopiez ensuite la ligne complète ; pour un port non standard, le nom est `[IP]:PORT`.

Les Secrets remplacent votre `.env` local comme source de configuration production. Le workflow génère sur le VPS un `runtime.env` privé (mode 600) nécessaire à Compose. Il n'est ni versionné ni inclus dans l'image. Les copies précédentes sont également privées : supprimez les anciennes versions selon votre politique de rétention après validation. Ne changez pas `POSTGRES_PASSWORD` seul après l'initialisation : le mot de passe PostgreSQL existant doit aussi être modifié dans la base.

## 5. Premier lancement et contrôles

Après DNS, SMTP et Secrets configurés, poussez sur `master` ou lancez manuellement **Vérifications** sur `master`. Vérifiez l'étape de déploiement puis ouvrez `https://worlds-tcg.com` et `/Auth/ForgotPassword`. Demandez un lien avec un compte existant, vérifiez sa réception et changez le mot de passe ; le lien ne doit plus fonctionner une deuxième fois.

Le lien expire après 30 minutes. Les comptes absents ont la même confirmation. Les demandes sont limitées par IP et protégées contre CSRF. Les jetons Identity utilisent des clés persistantes ; les sessions précédentes sont invalidées lors de leur prochaine validation (au plus cinq minutes). L'URL des mails est configurée, jamais issue du Host du navigateur.

En local : `docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d`, puis consultez les mails capturés dans Mailpit sur `http://localhost:8025` ; ils ne partent pas sur Internet.

## 6. Exploitation et sauvegardes

Sur le VPS :

```sh
cd /opt/worlds-tcg/current
docker compose -p worlds-tcg --env-file runtime.env -f docker-compose.prod.yml ps
docker compose -p worlds-tcg --env-file runtime.env -f docker-compose.prod.yml logs --tail 80 web-tcg smtp caddy
```

Ne publiez pas les logs sans supprimer les données personnelles et ne lancez pas `config` sans `--quiet` : la sortie contient les secrets. Les uploads, PostgreSQL, la file mail, les clés Identity et les certificats résident dans des volumes persistants. Ne faites pas `down -v`.

Avant une mise à jour après le premier déploiement, une sauvegarde SQL est écrite dans `/opt/worlds-tcg/backups`. Planifiez aussi une sauvegarde externe régulière de PostgreSQL, du volume `worlds-tcg_uploads_data` et de `worlds-tcg_protection_keys` (ainsi que vos clés SSH/secrets selon votre politique). Les sauvegardes automatiques locales ne protègent pas d'une perte du VPS. Prévoyez une rétention pour les dumps, releases et images afin de ne pas remplir le disque.

Si le démarrage ou HTTPS échoue, le script tente de rétablir l'image précédente. Les migrations démarrent avec l'application ; une migration incompatible exige une restauration manuelle de la sauvegarde SQL, pas un retour automatique du schéma. Une mise à jour peut interrompre temporairement les connexions et parties en cours. Aucun accès au VPS ni envoi réel n'a été effectué pendant la préparation locale.

## Validation locale du code

```sh
node --test .github/scripts/*.test.mjs tests/frontend-regressions.test.cjs
dotnet run --project tests/RegressionChecks/RegressionChecks.csproj -p:NuGetAudit=false
node tests/deploy-validation.mjs
node tests/proxy-network.integration.mjs
docker build -f web-tcg/Dockerfile -t worlds-tcg:verification .
docker compose -p worlds-tcg-recovery-test -f tests/password-recovery.compose.yml up -d --wait --wait-timeout 240
node tests/password-recovery.integration.mjs
docker compose -p worlds-tcg-recovery-test -f tests/password-recovery.compose.yml down -v
```

Le dernier `down -v` vise exclusivement la pile de tests jetable, jamais le projet production `worlds-tcg`.
