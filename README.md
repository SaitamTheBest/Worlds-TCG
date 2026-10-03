# Worlds TCG

Application ASP.NET Core (.NET 10), PostgreSQL et composants Card.

## Releases et Discord

Chaque tag poussÃ© dÃ©clenche `.github/workflows/release.yml`. Le workflow crÃ©e une **GitHub Release attachÃ©e au tag**, avec les commits depuis le prÃ©cÃ©dent tag, les pull requests et les contributeurs. Les tags `-alpha`, `-beta`, `-rc` et `-preview` deviennent des prÃ©versions.

Le workflow utilise le script de `master` : intÃ©grer les fichiers de cette modification dans cette branche avant de pousser le premier tag. Les permissions `contents: write` sont limitÃ©es au job de publication.

1. Dans Discord, crÃ©er un webhook dans le salon souhaitÃ© via **Modifier le salon â†’ IntÃ©grations â†’ Webhooks**.
2. Dans GitHub, ouvrir **Settings â†’ Secrets and variables â†’ Actions â†’ New repository secret**.
3. Nommer le secret **`DISCORD_WEBHOOK_URL`** et y coller l'URL du webhook. Ne pas mettre cette URL dans Git ou dans le `.env` de l'application.
4. AprÃ¨s avoir commitÃ© et poussÃ© les fichiers sur `master`, publier un tag :

```sh
git tag -a v1.0.0 -m "Version 1.0.0"
git push origin v1.0.0
```

Discord reÃ§oit un embed dorÃ© avec la version, le changelog, l'auteur et les liens vers la release. Les prÃ©versions sont orange. Les notes longues sont raccourcies avec un lien vers la version complÃ¨te et les mentions sont dÃ©sactivÃ©es.

Le secret absent n'empÃªche pas la crÃ©ation de la release. Ajouter le secret puis relancer **Actions â†’ Release et Discord â†’ Run workflow**, avec le tag existant. La release existante est conservÃ©e ; un marqueur invisible dans ses notes Ã©vite de rÃ©annoncer un message dÃ©jÃ  confirmÃ©. Si le message est envoyÃ© mais que l'enregistrement du marqueur GitHub Ã©choue, une reprise peut nÃ©anmoins envoyer un doublon.

Les notes sont dans la release associÃ©e au tag ; le message du tag Git lui-mÃªme n'est pas rÃ©Ã©crit. Source : [API officielle des releases GitHub](https://docs.github.com/en/rest/releases/releases).

## Docker et stockage

CrÃ©er un fichier local Ã  partir du modÃ¨le :

```sh
cp .env.example .env
```

Sous PowerShell : `Copy-Item .env.example .env`. Remplacer `POSTGRES_PASSWORD` par un mot de passe alÃ©atoire, en Ã©vitant les `;` non Ã©chappÃ©s dans la chaÃ®ne de connexion. Le fichier `.env` existant n'est pas remplacÃ© par cette modification.

```sh
docker compose config --quiet
docker compose up --build -d
```

L'application est disponible sur `http://localhost:8080` (modifiable via `WEB_PORT`). PostgreSQL est accessible localement sur le port 5433.

| Volume | Contenu |
| --- | --- |
| `uploads_data` | Images des cartes et avatars, dans `/app/wwwroot/uploads` |
| `postgres_data` | Base PostgreSQL |
| `protection_keys` | ClÃ©s de protection des cookies ASP.NET Core |

Le service d'initialisation donne les droits d'Ã©criture aux volumes avant le dÃ©marrage de l'application. Seul le dossier `uploads` est montÃ©, pour garder les ressources CSS, JavaScript et images du site dans l'image Docker. Le CSS Tailwind est compilÃ© pendant la construction.

Le `.env` sert Ã  l'interpolation Compose ; il n'est ni copiÃ© dans l'image ni montÃ© dans l'application. Seules les variables nÃ©cessaires sont injectÃ©es. [Fonctionnement des variables Compose](https://docs.docker.com/compose/how-tos/environment-variables/envvars-precedence/).

Le mode dÃ©veloppement est explicite :

```sh
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build -d
```

Un `docker compose down` normal conserve les volumes. **`down -v` supprime aussi les images envoyÃ©es et la base** : ne pas l'utiliser pour une simple mise Ã  jour. Les volumes doivent Ãªtre sauvegardÃ©s sÃ©parÃ©ment du dÃ©pÃ´t.

## Fichiers locaux et vÃ©rifications

`.gitignore` exclut `.env`, ses variantes, les uploads, les dÃ©pendances et les sorties de compilation. `.env.example` reste versionnÃ©. `.dockerignore` empÃªche ces fichiers et les mÃ©tadonnÃ©es Git d'entrer dans l'image. Les images statiques du site restent versionnÃ©es.

Le `.env` prÃ©cÃ©demment versionnÃ© reste dans les anciens commits : changer le mot de passe PostgreSQL dÃ©jÃ  enregistrÃ© avant une publication publique. Cette modification retire les fichiers du suivi sans supprimer leurs copies locales et sans rÃ©Ã©crire l'historique.

Le workflow CI vÃ©rifie les scripts de publication, les tests de l'application, Compose et la construction de l'image :

```sh
node --test .github/scripts/release.test.mjs tests/frontend-regressions.test.cjs
dotnet run --project tests/RegressionChecks/RegressionChecks.csproj -p:NuGetAudit=false
docker compose --env-file .env.example -f docker-compose.yml config --quiet
```

Les tests de publication simulent GitHub et Discord : aucun message rÃ©el n'est envoyÃ© pendant les vÃ©rifications locales.

## Production OVH et réinitialisation des mots de passe

Le guide complet Debian 12, worlds-tcg.com, DNS, HTTPS, ports, SSH et Secrets GitHub se trouve dans [deploy/README.md](deploy/README.md). Chaque push sur master est testé puis déployé. Le Docker de développement inclut Mailpit pour vérifier les mails sans envoi réel.
