# Vérifications decks, PVP et composants Card

Depuis la racine du projet :

```powershell
node --test tests/frontend-regressions.test.cjs
dotnet run --project tests/RegressionChecks/RegressionChecks.csproj
dotnet build web-tcg/web-tcg.csproj --no-restore
```

Les tests JavaScript vérifient les plafonds de quantité et de possession, les variantes, les brouillons, les requêtes d'enregistrement/suppression, les messages d'erreur et les reprises PVP, ainsi que le cache du composant Card. Le programme .NET teste la file d'attente, l'annulation, les anciennes connexions et 400 arrivées concurrentes.

Les vérifications EF reproduisent aussi le suivi erroné d'une nouvelle DeckCard avec un GUID prédéfini, puis contrôlent son insertion explicite dans un deck existant ou nouveau, sans ouvrir de connexion PostgreSQL.

Elles reproduisent l'erreur ReadOnlySpan des tableaux avec C# 14, puis vérifient la traduction SQL PostgreSQL des trois requêtes de présentation des cartes avec des listes, sans ouvrir de connexion à la base.

Pour contrôler les vues Razor avec des données fictives, sans connexion à la base :

```powershell
dotnet run --project tests/RegressionChecks/RegressionChecks.csproj -- --preview
```

Ouvrir `http://127.0.0.1:5299/`, `/Deck/Manage`, `/Pvp/Play`, ou `/Game/11111111-1111-1111-1111-111111111111`. La prévisualisation utilise les vrais composants et scripts, avec des API fictives ; elle ne valide pas une partie réelle ni la persistance PostgreSQL. Arrêter avec Ctrl+C.

L'initialisation du bot corrige son deck incomplet et lui attribue les exemplaires nécessaires uniquement dans son inventaire. Elle nécessite un Leader et au moins dix cartes principales distinctes. Les brouillons des joueurs restent enregistrables, et les parties exigent toujours exactement 40 cartes hors Leader.

Les vérifications de partie couvrent la pose par emplacement ou par bouton, les refus affichés sans perte de sélection, l'énergie insuffisante et la conservation de l'animation après confirmation serveur. Le moteur est contrôlé pour les emplacements manquants, invalides ou occupés et les lieux déjà posés : un refus conserve la main et l'énergie. La prévisualisation expose également `/Admin/CreateCard` avec le vrai formulaire et un Hub de jeu fictif acceptant la pose des personnages.

Le matchmaking affiche les joueurs humains distincts dans les parties en cours (hors bots) et la file FIFO, avec actualisation toutes les cinq secondes. Les tests JS contrôlent l'actualisation, l'indisponibilité du serveur et les notifications de variations de PV, d'énergie et de coût sans doublon. La prévisualisation génère 120 messages pour vérifier le défilement indépendant du chat et de l'historique, et expose des compteurs fictifs.

Le clic sur une carte ouvre maintenant un dialogue natif avec le composant Card agrandi : pose par emplacement, blocage, compétences/talents et choix de cible. Les refus courants désactivent les boutons avec leur raison ; les validations finales restent côté serveur. Les tests couvrent les cibles du Hub, le premier tour, l'épuisement, les talents et la préservation des actions lors d'une mise à jour du chrono. Les variations sont affichées pendant deux secondes près du compteur ou de la carte concernée, sans grand message global.

Le plateau s'adapte à la hauteur disponible sans défilement de page ni commande de zoom. Le Leader apparaît à côté des personnages ; les chronos, l'énergie, la pioche et le cimetière sont regroupés au centre. La main ajuste son chevauchement pour rester dans la fenêtre. Le chat et l'historique conservent leur défilement interne. Les chronos utilisent la date de référence du serveur et décomptent localement entre deux états : les tests couvrent une référence ancienne, les changements de tour, les mises à jour locales et l'arrêt en fin de partie. Les cartes et leur dialogue affichent les états actifs (épuisement, blocage, bouclier, furtivité et bonus d'attaque).

L'éditeur de capacités configure désormais les actions, déclenchements, cibles, valeurs, durées, conditions et recharges. Les effets et conditions sont chargés en modification et remplacés avec le suivi EF requis. Les tests couvrent le refus du blocage d'un Leader, l'interdiction de tout talent pendant l'épuisement, les dégâts multipliés, les attaques supplémentaires, les décisions ciblées et conditionnelles, la résurrection à 50 % et les bonus passifs de groupe, y compris sur les nouvelles cartes et après destruction du lieu. Les actions instantanées (dégâts, soins, ressources) ne prennent pas de durée ; les états et multiplicateurs peuvent durer les prochains tours du propriétaire. Les passifs restent liés à la présence de leur source. Second souffle est limité à une résurrection par exemplaire.

Les capacités proposent des sélections multiples de régions et de pays, pour les conditions, les bénéficiaires et le comptage de personnages. Plusieurs régions sont combinées avec OU ; régions et pays sont combinés avec ET. Bonus de PV augmente les PV actuels et maximaux ; Bonus de dégâts augmente les compétences avant application du multiplicateur. Le calcul par personnage peut compter le terrain allié, adverse ou les deux. Les tests contrôlent l'enregistrement des paramètres, les filtres, le comptage sans Leaders ni personnages morts, les recalculs passifs, l'expiration, le retrait des auras et le SQL de migration JSONB, sans accès à PostgreSQL.

Le ciblage se fait directement sur les cartes du plateau : cibles autorisées calculées avec le moteur côté serveur, surbrillance, confirmation source/cible, changement de cible ou annulation. Les attaques acceptées animent le déplacement aller-retour de la carte ; les soins et refus ne déclenchent pas cette animation. Les contrôles couvrent les bloqueurs, le camouflage, les origines, les conditions de PV, l'invalidation de la sélection et les erreurs détaillées. Les captures dans tests/artifacts utilisent des données fictives.

Le plateau affiche désormais le tour, les capacités disponibles, le nombre de cartes de la main adverse et la dernière action. Les tours de l’IA sont diffusés action par action, avec des pauses de présentation. Les tests contrôlent l’ordre et l’absence de doublons dans l’historique.

L’écran de fin présente victoire/défaite, la cause de fin et les récompenses personnelles. Les montants sont enregistrés dans le JSON de la partie lors du crédit du compte, dans la même transaction : aucune migration supplémentaire n’est nécessaire. Les défaites rapportent actuellement 0 XP et 0 ¤, conformément aux règles de récompense existantes. Les anciennes parties sans montants enregistrés affichent « — ». Dans l’aperçu fictif, « Terminer le tour » déclenche une victoire de démonstration et « Abandonner » une défaite.
