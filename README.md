# Worlds TCG

**Worlds TCG** est un jeu de cartes à collectionner en ligne autour de la politique. Constituez votre collection, construisez un deck avec votre Leader et affrontez un autre joueur ou l’IA dans des parties au tour par tour.

L’application propose une interface noire et dorée, des cartes avec plusieurs variantes visuelles, un marché entre joueurs et un espace communautaire pour discuter, échanger et organiser des duels.

Site : [worlds-tcg.com](https://worlds-tcg.com).

## Fonctionnalités

| Espace | Possibilités |
| --- | --- |
| Collection | Inventaire paginé, recherche, tri, fiche détaillée et défausse individuelle ou multiple. |
| Boosters | Ouverture animée de cinq cartes, probabilités consultables, indicateur **New** et sons adaptés aux raretés et aux variantes. |
| Decks | Création et modification, recherche par nom ou numéro, filtres par extension, pagination de 50 cartes et aperçu agrandi. |
| Parties | PVP avec recherche d’adversaire, parties contre l’IA, pile ou face initial, historique et chat. |
| Marché | Enchères, ventes, historique des achats et des ventes, prix moyen et courbe des ventes d’une carte. |
| Amis | Recherche de joueurs, invitations, messagerie, échanges de cartes et de monnaie marché. |
| Duels entre amis | Invitation notifiée, écran d’attente, choix des decks et lancement automatique de la partie. |
| Profil | Avatar, progression et vitrine de cartes. |
| Administration | Gestion des cartes, extensions, régions, pays, partis politiques et capacités. |

Les notifications signalent notamment les événements du marché, les invitations, les messages, les propositions d’échange et les duels. Les informations de partie sont actualisées avec SignalR.

## Cartes et règles de jeu

### Construire son deck

Un deck jouable contient **un Leader et 40 cartes principales**. La limite est de **quatre exemplaires d’une même carte de gameplay**, toutes variantes confondues. Les cartes doivent être possédées dans l’inventaire ; un deck incomplet peut être enregistré comme brouillon mais ne peut pas lancer une partie.

Les cartes principales comprennent des **Personnages**, des **Lieux** et des **Décisions politiques**. Les cartes SECRET identifiées comme non jouables sont exclues des decks.

Le terrain accueille jusqu’à quatre Personnages et un Lieu par joueur, avec une zone séparée pour le Leader. La défaite du Leader met fin à la partie. L’abandon et les limites de temps sont également gérés.

### Énergie et capacités

- Le joueur qui commence reçoit **60 points d’énergie** à son premier tour.
- Les autres débuts de tour accordent **80 points d’énergie**, dans la limite de **750 points stockés**.
- Une **Compétence** épuise la carte après utilisation, sauf effet accordant des attaques supplémentaires.
- Un **Talent** s’utilise au maximum une fois par tour et respecte sa recharge configurée, sans épuiser la carte. Sa recharge diminue aux tours de son propriétaire.
- Un **Effet** se déclenche automatiquement selon sa configuration : pose, début ou fin de tour, dégâts reçus, mort ou effet passif.

Les capacités peuvent infliger des dégâts, soigner, modifier les PV maximum, accorder des bonus ou malus, affecter l’énergie, faire piocher ou appliquer des états. Les actions **Prison** et **KO** sont également disponibles.

| État | Rôle |
| --- | --- |
| Bouclier | Absorbe la prochaine attaque reçue. |
| Camouflage | Empêche le ciblage adverse. Pour un Personnage, nécessite un autre Personnage allié sur le terrain ; les Leaders suivent leurs conditions explicites. |
| Provocation | Impose une priorité de ciblage aux attaques adverses. Le blocage manuel généralisé n’est pas utilisé. |
| Prison | Empêche la carte d’agir jusqu’à la fin de son prochain tour. |
| KO | Envoie au cimetière un Personnage ou un Lieu ciblé, allié ou adverse selon l’action ; ne supprime pas un Leader. |

Dans Play, les états sont représentés par des icônes avec explications. Les cartes peuvent être agrandies pour lire leurs capacités.

### Conditions des capacités

L’éditeur d’administration distingue les actions, leurs déclenchements, leurs bénéficiaires et les conditions à remplir. Il permet notamment de filtrer par région, pays, parti et orientation politique, de vérifier la présence d’une carte nommée sur le terrain ou de conditionner une action à l’orientation d’un Leader allié ou adverse.

Une capacité peut aussi demander de **dévoiler** ou de **défausser** des cartes choisies dans la main, avec des filtres. Les cartes dévoilées restent en main et sont montrées pendant l’action ; la défausse est payée à la confirmation.

## Collection, monnaies et échanges

Deux soldes distincts sont utilisés, avec des montants entiers :

| Solde | Utilisation |
| --- | --- |
| `Balance` | Achat de boosters. La recharge passive est de 100 toutes les 10 minutes, jusqu’à un plafond de recharge de 1 500. |
| `CardBalance` | Enchères et échanges entre joueurs. La défausse rapporte 1 unité par exemplaire. |

Le plafond de recharge n’efface pas un solde déjà supérieur à 1 500. Les prix et paramètres des extensions sont administrables.

La défausse multiple retire **tous les exemplaires des cartes sélectionnées**, après confirmation. Elle peut rendre un deck incomplet.

Les échanges entre amis peuvent combiner cartes et monnaie marché. La possession des cartes et les soldes sont revérifiés avant le transfert. Une invitation en duel expire après **cinq minutes sans réponse** ; après acceptation, le choix du deck dispose également d’un délai de cinq minutes.

Les extensions non encore publiées sont exclues des boosters disponibles et des statistiques du catalogue et de progression de l’accueil.

## Technologies

- **ASP.NET Core MVC / .NET 10** et vues Razor.
- **Entity Framework Core 9**, fournisseur Npgsql et **PostgreSQL 17**.
- **ASP.NET Core Identity** pour les comptes et les rôles.
- **SignalR** pour les parties, notifications, messages et soldes en temps réel.
- JavaScript, CSS, **Tailwind CSS**, **GSAP** et **Lucide** pour l’interface.
- **Web Audio API** et fichiers WAV dans `web-tcg/wwwroot/sons/` pour les boosters.
- **Docker Compose** pour les environnements locaux et de production.
- **Caddy** pour le HTTPS en production, Mailpit pour les mails locaux et relais SMTP pour la production.

## Installation locale avec Docker

Prérequis : Git, Docker et le plugin Docker Compose. Docker Desktop convient pour un poste Windows avec conteneurs Linux. L’image construit elle-même le CSS et l’application ; le SDK .NET et Node.js sur l’hôte sont nécessaires seulement pour travailler hors conteneur ou lancer les tests locaux.

Depuis la racine du dépôt, pour une première installation :

```powershell
Copy-Item .env.example .env
```

Sous Linux ou macOS : `cp .env.example .env`. Si un fichier `.env` existe déjà, conservez-le. Renseignez `POSTGRES_DB`, `POSTGRES_USER` et un mot de passe PostgreSQL dans ce fichier. `WEB_PORT` vaut 8080 par défaut.

Démarrez la pile de développement, avec les mails capturés par Mailpit :

```sh
docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml config --quiet
docker compose --env-file .env -f docker-compose.yml -f docker-compose.dev.yml up --build -d
```

| Service | Adresse locale |
| --- | --- |
| Application | [http://localhost:8080](http://localhost:8080) |
| Mails de test Mailpit | [http://localhost:8025](http://localhost:8025) |
| PostgreSQL | `127.0.0.1:5433` depuis l’hôte ; `postgres:5432` depuis l’application Docker. |

Au démarrage, l’application applique les migrations, crée le rôle `Admin` et initialise le compte IA `AI_BOT` ainsi que son deck. L’attribution du rôle `Admin` à un compte humain reste à effectuer séparément. Les cartes et extensions du catalogue se configurent dans l’administration.

Les mails envoyés en développement restent dans Mailpit. Pour une installation de production, utilisez le guide de déploiement ci-dessous plutôt que cette pile locale.

### Stockage persistant

| Volume local | Contenu |
| --- | --- |
| `postgres_data` | Base de données PostgreSQL. |
| `uploads_data` | Images envoyées pour les cartes et les avatars. |
| `protection_keys` | Clés ASP.NET Core de protection des cookies et des jetons. |

Un arrêt normal avec `docker compose down` conserve les volumes. **L’option `down -v` les supprime** : elle ne doit pas servir à mettre à jour une installation contenant des données à conserver. Le code, les styles et les sons restent dans l’image Docker ; seul le dossier `uploads` est monté pour les images envoyées.

## Développement et structure

Pour construire hors Docker : installez le **SDK .NET 10** et **Node.js 24**, puis lancez :

```sh
npm --prefix web-tcg ci
npm --prefix web-tcg run build:css
dotnet restore web-tcg/web-tcg.csproj
dotnet build web-tcg/web-tcg.csproj --no-restore
```

La configuration de connexion PostgreSQL et, si nécessaire, celle du SMTP doivent être fournies à l’application pour un lancement hors Docker. Les secrets utilisateur .NET ou les variables d’environnement permettent de les garder hors du dépôt. Le `.env` est lu par Docker Compose, pas automatiquement par `dotnet run`.

```text
web-tcg/
  Controllers/       Routes des espaces joueur et administrateur
  Data/              Entités, énumérations et contexte de base de données
  Game/              Moteur, effets, ciblage, état des parties et boosters
  Migrations/        Évolution du schéma PostgreSQL
  Models/            Modèles de vues et contrats de données
  Services/          Marché, amis, duels, decks, comptes et notifications
  ViewComponents/    Composants partagés : cartes, soldes, confirmation...
  Views/             Vues Razor
  wwwroot/           CSS, JavaScript, images et dossier sons/
tests/               Régressions JavaScript, backend et tests d’intégration
deploy/              Configuration Caddy, Compose de production et scripts VPS
.github/             Vérification, publication et déploiement automatisés
```

Le composant partagé **Card** centralise le rendu des cartes. Pour adapter leur taille à une page, utilisez les conteneurs d’aperçu plutôt que de modifier `web-tcg/wwwroot/css/card.css`.

## Vérifications

Depuis la racine du dépôt :

```sh
node --test .github/scripts/release.test.mjs .github/scripts/prepare-deploy.test.mjs
node --test tests/frontend-regressions.test.cjs tests/friends-pagination.test.cjs tests/inventory-discard.test.cjs tests/popup-confirmation.test.cjs tests/friend-duel.test.cjs tests/booster-sounds.test.cjs
dotnet run --project tests/RegressionChecks/RegressionChecks.csproj -p:NuGetAudit=false
node tests/deploy-validation.mjs
```

Les contrôles couvrent notamment les capacités, conditions, ciblages, recharges, états, interactions de Play, decks, confirmations et sons des boosters.

Les tests d’intégration PostgreSQL nécessitent une **base de test dédiée et jetable**. Les options `--market`, `--currency` et `--inventory` utilisent `TCG_MARKET_TEST_CONNECTION` ; `--friends` et `--duels` utilisent `TCG_FRIENDS_TEST_CONNECTION`. Exécutez chaque option séparément avec le projet `tests/RegressionChecks/RegressionChecks.csproj`. Ces tests appliquent des migrations et créent ou modifient des données : ne les pointez pas vers la production.

La liste complète des contrôles, dont les tests HTTP/SMTP et la construction Docker, se trouve dans [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Déploiement sur un VPS OVH

Le guide détaillé est dans **[deploy/README.md](deploy/README.md)** : préparation de Debian 12, accès SSH, DNS, HTTPS, SMTP, secrets GitHub et sauvegardes.

La configuration de production utilise Caddy sur les ports **80 et 443**, avec des volumes persistants pour ses certificats. Le domaine configuré est `worlds-tcg.com` ; `www` n’est pas configuré par défaut.

Le workflow **Vérifications** contrôle les pushes sur `master` et `main`, ainsi que les pull requests. Après réussite, un push sur `master` publie l’image sur GHCR et déclenche le déploiement SSH. Un lancement manuel sur `master` peut aussi déployer si l’option correspondante est activée.

Sur le VPS, pour consulter l’état et les journaux de Caddy :

```sh
cd /opt/worlds-tcg/current
docker compose --project-name worlds-tcg --env-file runtime.env -f docker-compose.prod.yml ps caddy
docker compose --project-name worlds-tcg --env-file runtime.env -f docker-compose.prod.yml logs --tail=80 caddy
```

Ces commandes supposent un compte autorisé à lire le répertoire et à utiliser Docker. `cd` est une commande du shell : si un accès administrateur est nécessaire, ouvrez d’abord une session avec `sudo -i`, puis changez de dossier.

Les déploiements appliquent les migrations au démarrage et effectuent un contrôle HTTPS. Les sauvegardes locales du déploiement sont à compléter par des sauvegardes externes des données et volumes persistants. Les détails de restauration figurent dans le guide de production.

## Releases et annonces Discord

Le workflow [.github/workflows/release.yml](.github/workflows/release.yml) crée une GitHub Release pour chaque tag poussé, avec les commits, les pull requests et les contributeurs. Les versions `alpha`, `beta`, `rc` et `preview` sont publiées comme préversions.

Pour activer l’annonce dans Discord, créez un webhook de salon et enregistrez son URL dans le secret GitHub Actions **`DISCORD_WEBHOOK_URL`**. Le workflow utilise le script de la branche `master` ; ses modifications doivent être intégrées dans cette branche avant publication.

```sh
git tag -a v1.0.0 -m "Version 1.0.0"
git push origin v1.0.0
```

L’absence du secret Discord ne bloque pas la création de la release. Le workflow peut être relancé manuellement pour un tag existant. Les tests de publication simulent GitHub et Discord et n’envoient pas de véritable annonce.

## Configuration et confidentialité

Les fichiers `.env`, `runtime.env`, les clés privées, mots de passe et webhooks ne doivent pas être versionnés ou partagés. Utilisez [.env.example](.env.example) pour documenter la configuration locale et les secrets GitHub pour la production. Évitez de partager la sortie détaillée de `docker compose config` : elle peut contenir des valeurs sensibles ; utilisez `config --quiet` pour une validation.

La réinitialisation du mot de passe utilise un lien temporaire envoyé par mail. La configuration de son domaine public et du service SMTP est décrite dans le guide de déploiement.

Les fichiers texte du projet sont enregistrés en **UTF-8** pour conserver les accents et les symboles monétaires.

## Licence

Le code est distribué sous [licence MIT](LICENSE). Les droits sur les images, logos et autres médias doivent être vérifiés séparément avant leur réutilisation.
