# Projet

JEU DE SLIMES (décision du 2026-10-21) : jeu mobile en écran VERTICAL, autour de slimes mous qu'on prend plaisir à voir
tomber, s'écraser, trembler et fusionner. Point de départ du gameplay : un « Suika » (jeu de la pastèque) de slimes : on lâche
des slimes dans un réservoir, deux slimes de même couleur qui se touchent fusionnent en un plus gros (« division cellulaire à
l'envers »), la partie s'arrête quand ça déborde. Très simple, très addictif, très satisfaisant. Le cœur de jeu définitif
n'est PAS encore décidé : on prototype d'abord la sensation.

Avant : un jeu de momentum avec un BLOB (balle souple) poussé par des flippers, puis un skateur (abandonnés). Tout cet état
(code, scènes, ancien CLAUDE.md complet avec toutes les mesures) est conservé par les tags git `blob-et-debut-skate` et
`avant-slime`. Le code reste dans le projet (voir « Code existant »).

## Piliers
- Satisfaction d'abord : déformations, tremblements, fusions, réactions en chaîne. La mollesse doit être une mécanique, pas
  un décor
- Ultra simple : un doigt, compris en une seconde en regardant quelqu'un jouer ; parties courtes (« encore une »)
- Résultat TOUJOURS déterministe : mêmes entrées = même partie, au bit près (défi du jour à suite fixe, replays, classements)
- Performance cruciale, mobile d'abord : beaucoup de slimes à l'écran sans ralentir

## Idées retenues (À VALIDER en jeu, rien n'est décidé)
- Tenir avant de lâcher : le slime se comprime, puis rebondit ou s'étale (compétence sans complexité)
- Secouer le réservoir : ressource rare (2–3 par partie, rechargée par les grosses fusions), toute la pile tremble et se
  débloque ; entrée enregistrée comme les autres (déterministe)
- Défi du jour : même suite de slimes pour tous (graine fixe), classement, replay du meilleur rejoué par la simulation
- Slimes spéciaux rares (3–4 max) : collant, lourd, arc-en-ciel (fusionne avec tout), acide (mange un petit voisin)
- Collection : slimes rares / shiny gagnés en fin de partie. Attention : en PERDRE crée de la rancœur ; pistes : il part en
  balade et revient, ou un seul « compagnon » à la fois
- Bocal-jouet : écran d'accueil où la collection se laisse toucher, écraser, secouer (attachement, aucune contrainte)
- Deuxième piste de cœur de jeu, en réserve : « chute molle » (un slime tombe dans un puits ; tenu = mou, il se faufile ;
  relâché = dur, il rebondit et casse). Écartées : remplir un moule (incompréhensible sans démonstration), slime qui se
  déplace tout seul (perte de contrôle)
- Piste technique : physique de jeu SIMPLE par slime (cercle ou petit anneau de 8–12 points) + gélatine VISUELLE (ressort
  par slime calculé seulement à l'affichage, jamais dans la simulation). Le blob actuel (anneau de 32 capsules + joints) est
  trop lourd pour 30 slimes à l'écran : à mesurer (étape 1)

## Roadmap (étape actuelle en gras)
1. **Strict minimum (scène `Slime/reservoir`)** : un réservoir (rectangle ouvert en haut), on lâche des blobs d'en haut
   (`BlobDropper`), rien d'autre. But : voir comment ils s'empilent et combien on peut en mettre avant de ralentir.
   Fait (2026-10-21) : réservoir 8 × 14 m (courbe ouverte en U), `Lâcher` (flèches = viser, Espace = lâcher, tenu = rafale,
   R = vider), `Blob modèle (désactivé)` = réglages des blobs lâchés (défauts : 16 capsules, Ø 1 m, joints 48 Hz), gravité −15.
   Mesuré (PC, éditeur, blobs empilés au repos) : 10 blobs 0,24 ms par pas, 30 : 0,9, 60 : 2,3, 90 : 3,5, 120 : 5,1 ms (réservoir
   presque plein) ; budget 8,3 ms par pas à 120 pas/s, mobile 2 à 4 × plus lent → ≈ 40–60 blobs max sur mobile en l'état.
   Moins de la moitié est Box2D (120 blobs : 2,0 ms) : le reste = chaque blob relit TOUS les événements de contact du monde
   (`BLOB.RefreshContacts`, coût ∝ blobs × événements) → première optimisation évidente. Rejeu exact au bit près (d'un bloc
   et pas à pas). Corrigé au passage : tous les blobs avaient le même groupe de collision (−1) et se traversaient ; un groupe
   négatif par blob maintenant (`BLOB._groupCounter`)
   Fusion (2026-10-21, `BlobFusion` sur `Lâcher`) : deux blobs de même taille qui se touchent → un blob ×2 en diamètre
   (`growth`), capsules doublées jusqu'à 32, couleur par niveau, `maxLevel` = 2 par défaut (sans limite : blobs de 8 puis 16 m
   dans le réservoir de 8 m, éjectés à 200 m/s). Mitose à l'envers : nouvel anneau posé sur une cacahuète (union adoucie des
   deux cercles, col `waist`) TENUE par l'anneau (`SetShape` : angles de repos des joints = angles de la cacahuète − 2π/m,
   capsules allongées à son périmètre), puis ramenée au cercle en `mergeTime` selon `mergeCurve` ; capsules qui s'épaississent.
   Avant : laissée s'arrondir seule = quasi instantané, et avec `growth` < 2 les joints contractaient le périmètre d'un coup. Écarté : recoudre les capsules réelles des deux anneaux (un blob écrasé par sa chute se replie,
   l'anneau recousu se croise en 8 et y reste bloqué). Rejeu exact au bit près (d'un bloc et pas à pas, 33 fusions).
   À revoir : les gros blobs (32 capsules, mêmes joints) sont bien plus mous, ceux du bas de la pile s'aplatissent ; surface (et
   masse) du blob fusionné = 2 × celle des deux blobs réunis (conserver la matière = diamètre ×1,41)
2. Choisir la représentation des slimes selon les mesures (blob actuel allégé / anneau simple / cercle + gélatine visuelle)
3. Fusion des slimes de même couleur (faite en version brute, voir étape 1), ligne de débordement, score : premier vrai prototype jouable
4. Sensation (gélatine, sons, effets), puis idées retenues une par une

Garder cette section à jour quand une étape avance ou que la vision du jeu se précise.

## Technique
- Unity 6000.6, physique 2D bas niveau `Unity.U2D.Physics` (Box2D v3 : PhysicsWorld,
  PhysicsBody, PhysicsShape, PhysicsChain, joints)
- PAS de Physics2D / Rigidbody2D / Collider2D classiques
- Un seul monde : `PhysicsWorld.defaultWorld` (pas fixe : 120 Hz, `fixedDeltaTime` ≈ 0,0083)
- Les `NativeList` doivent être détruits/libérés dans `OnDestroy` (sinon fuite mémoire)
- Utiliser `XxxDefinition.defaultDefinition` pour les définitions (corps, formes, joints), puis modifier les champs
- Unités des rotations (mesuré, Unity 6000.6) : `PhysicsBody.angularVelocity` et les angles des charnières (`angle`,
  `springTargetAngle`, limites, `motorSpeed`) sont en degrés ; `PhysicsBody.GetBatchVelocity` rend `angularVelocity` en rad/s mais
  `SetBatchVelocity` l'attend en °/s : convertir avant de réécrire, sinon chaque écriture écrase presque la rotation des corps
- Charnière (`PhysicsHingeJoint`) : `angle` = angle du corps B − angle du corps A quand les ancres (`PhysicsTransform`) n'ont pas
  de rotation ; le ressort (`enableSpring`, `springFrequency` en Hz, normalisé par la masse) vise `springTargetAngle`
- Pivoter un corps autour d'un point de contact (et pas de son centre de masse) injecte de la quantité de mouvement = propulsion
  parasite : toute rotation imposée doit conserver l'élan (piège rencontré sur le skateur)
- Limites de Box2D mesurées (2026-10-19), à garder en tête pour tout élément :
  - Rotation plafonnée à 45° par pas (`PhysicsWorld.bodyMaxRotation`, lecture seule) : 5400 °/s à 120 Hz. Une petite roue qui
    roule vraiment plafonne vite (10 cm : ≈ 17 km/h) → roues soudées et sans frottement (elles glissent, leur rotation est dessinée)
  - Contact vu seulement à moins de 0,02 m (`speculativeContactDistance`) : un corps kinematic qui avance de plus par pas
    (bumper : 0,12 m) entre dans ce qu'il frappe, et le dégagement « mou » qui suit fait dériver / tourner, ou le traverse (bumper à 200 km/h =
    0,46 m par pas > capsule du blob de 0,33 m : bumper coincé dans l'anneau). La collision continue (`fastCollisions`, sur le
    corps frappé ou sur le kinematic) n'y change rien. Solution de `PinballBumper` : choc donné d'avance (voir plus bas).
    Abandonné : s'arrêter au contact avant de pousser (le blob, 32 corps, n'était plus propulsé)
  - Peu d'itérations de résolution : deux contacts d'un même corps se partagent mal une grosse impulsion (bumper, réception à plat
    sur les deux roues) → rotation parasite de 5 à 15 °/s dont le SENS dépend de l'ordre interne des contacts. Ni les sous-pas ni
    la raideur des contacts n'y changent rien. Les deux roues sont indépendantes quand l'inertie vaut masse × (empattement / 2)²
    (`Skateboard.truckInertia`, mesuré : 0,000 °/s)
  - Formes collées sur un même corps (cercle + polygone) : le coin d'un polygone posé sur un cercle est vu comme un vrai coin
    (normale tournée vers l'arrière) → une roue qui passe dessus est freinée et projetée (flipper : 62 → 60 km/h, 373 °/s). Tout
    coin interne doit être enfoncé d'au moins 0,025 m dans l'autre forme (`PinballFlipper`), ou ne pas exister (polygone arrondi)
  - Chaîne sur un corps mobile : surface parfaitement lisse, mais d'un seul côté : un flipper qui se relève fait passer la roue
    à l'intérieur (planche coincée). Abandonné pour les éléments mobiles
  - Sommets d'un polygone à moins de 0,02 m : soudés sans prévenir (trapèze → triangle : masse et forme changent) ou forme
    effacée (plateau de 2 cm de la planche à l'échelle 0,5). Toute forme qui change de taille doit garder ses côtés au-dessus
  - Collision continue (`monde_settings.continuousAllowed`) : à grande vitesse, un corps peut être arrêté pendant le pas sans
    qu'aucun contact ne soit signalé à la fin de ce pas (le contact n'apparaît qu'au pas suivant). Ne pas compter sur les seuls
    contacts pour détecter un choc (`Skater.airImpactSpeed`). Ne pas la couper : sans elle, une planche rapide traverse les courbes
  - `PhysicsQuery.ShapeTimeOfImpact` (Unity 6000.6) : seule la `fraction` du résultat est juste ; état, point et normale reviennent
    décalés en mémoire (bug de l'interface Unity). Recalculer point et normale par `ShapeDistance` à l'instant de l'impact.
    `CastGeometry` et `ShapeDistance` rendent des valeurs correctes

## Échelle (standard du projet)
- 1 unité Unity = 1 m pour Box2D (`lengthUnitsPerMeter = 1`). Ses tolérances sont en absolu
  (`linearSlop` 0,005, `bounceThreshold` 1 m/s, `contactSpeed` 3 m/s…) : la taille n'est PAS relative
- Référence : un blob de diamètre 1 m (tailles de balles et de slimes toujours en diamètre). Objets mobiles entre 0,1 et 10
- Échelle des Transform des éléments toujours à (1, 1, 1) : la taille se règle par les paramètres, sinon épaisseurs et cercles
  se déforment
- Régler la sensation de vitesse par la gravité / vitesse du jeu, pas par la taille
- Épaisseur des courbes : visuelle seulement (la collision est une chaîne fine)

## Code existant (hérité du jeu de blob / skate)
Détail complet (réglages, mesures, pièges) dans le CLAUDE.md du tag `avant-slime`. À relire avant de modifier un de ces scripts.
- `Scripts/Core/` : moteur. `LevelSimulation` (seul moteur pas à pas : entrées → `ILevelTick` dans l'ordre de la hiérarchie →
  `PhysicsWorld.Simulate`, rejeu accéléré `FastForward`), `LevelInput` (entrées = un bit par `FlipperKey`, enregistrées),
  `LevelRestart` (R : détruit et recrée toute la physique `ILevelPhysics`), `monde_settings` (seul endroit qui règle le monde :
  gravité, sous-pas, `simulationWorkers` = 1), `GizmoPickArea`, `SpeedKmhAttribute`
- `Scripts/Legacy/Blob/` : `BLOB` (anneau de capsules reliées par des joints fixes ; lest flèche bas ; contacts par événements ;
  `SetShape` pour l'étirer), `BlobJump` (saut Impulse / Stretch). Joints à fréquence 0 = rigides mais l'anneau est
  précontraint (ils visent un angle relatif nul : seules les différences d'angle comptent)
- `Scripts/Level/` : courbes `CurvePath2D` + `CurveCollider2D` (chaînes Box2D), flippers, bumpers, obstacles (`BlobHazard`,
  piques, scies), `MovingPath`, `BoostZone`, gadgets (grappin), vitre destructible `GlassPane`
- `Scripts/Game/` : `CameraFollow`, `SpeedMeter`, `TrainingMode` (points de sauvegarde exacts, S / Tab / R)
- `Scripts/Skater/` : skateur (abandonné)
- `Scripts/Slime/` : le nouveau jeu. `BlobDropper` (lâcher, possède la liste des blobs et leur pas), `BlobFusion` (fusion).
  Les capsules de blob portent leur `BLOB` en `userData.objectValue` (`BLOB.FromShape`)
- Scènes : `Assets/Scenes/Slime/` (nouveau jeu), le reste = bancs de test hérités

## Organisation des scripts
- `Scripts/Slime/` : tout le nouveau jeu (slimes, réservoir, lâcher). Code utilisable en build (pas de `UnityEditor`)
- `Scripts/Core/` : moteur ; `Scripts/Cosmetics/` : effets purement visuels (ils LISENT l'état, jamais d'écriture, jamais
  dans `LevelTick`, dans `LateUpdate`) ; `Editor/` : inspecteurs et poignées (pas inclus dans le jeu)
- Déplacer / renommer un script uniquement via Unity (`AssetDatabase.MoveAsset`) : le `.meta` suit, les scènes gardent leurs références

## Construction des éléments
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur ; défauts du script = réglages officiels
- Aperçu dans la Scene view AVANT de lancer le jeu (`OnDrawGizmos`), même géométrie que celle envoyée à la physique
- La physique n'est créée qu'en Play, dans `OnEnable`, et détruite dans `OnDisable` ; le composant implémente `ILevelPhysics`
  (tout corps créé hors d'un `ILevelPhysics` empêche la remise à zéro du monde)

## Rendu
- Pour l'instant : rendu debug de la physique (`drawOptions`) et `PhysicsWorld.Draw…`. L'esthétique viendra après la sensation
- AUCUN mesh, sprite (exceptions : `GizmoPickArea` éditeur ; `ParticleSystem` des effets de `Cosmetics/`)

## Déterminisme
- Pas de `Random` sans graine fixe explicite
- Logique de simulation et pilotage physique dans `ILevelTick.LevelTick()`, jamais dans `Update` ni `FixedUpdate`
  (un pas doit pouvoir être rejoué en accéléré) ; utiliser `Time.fixedDeltaTime`
- `LevelSimulation` est le seul moteur : à chaque pas fixe, entrées → `LevelTick` de chaque élément (ordre de la hiérarchie)
  → `PhysicsWorld.Simulate` (monde en `SimulationType.Script`, réglé par lui seul). Actions entre deux pas
  (redémarrage, retour à une sauvegarde) : `LevelSimulation.RunBeforeNextStep`
  Pourquoi : avec chaque script dans son propre FixedUpdate, l'ordre dépendait de Unity et un pas ne pouvait pas être
  rejoué hors du temps réel. Un seul moteur = ordre fixe + rejeu accéléré exact (sauvegardes, replays, fantômes)
- Entrées du joueur : uniquement via `LevelInput` (un bit par `FlipperKey` : Espace, flèches…, enregistré à chaque pas), jamais
  le clavier directement dans la simulation. Toute action non enregistrée (ex. souris de debug) casse le rejeu
- Ordre de création des corps/joints stable et explicite (ne pas dépendre de l'ordre
  d'exécution des scripts) ; ordre d'itération stable (pas de dépendance aux Dictionary/HashSet)
- Ne jamais dépendre de l'ordre des événements ni des contacts renvoyés par Box2D (`triggerBeginEvents`, `contactBeginEvents`,
  `GetContacts`, `GetBodies`) : il suit ses identifiants internes, qui changent d'un essai à l'autre (alternés à chaque
  redémarrage, mesuré). La simulation Box2D reste identique, mais un script qui traite « le premier » diverge. Trier par une
  clé stable (ordre de création, index de membre), ou rendre le calcul indépendant de l'ordre (somme en entiers, maximum
  départagé par la position). `GlassPane` et `Skater` le font. Mesuré (audit du 2026-10-18, époque blob) : `GetBodies` change
  bien d'ordre, le `GetContacts` d'un même corps est resté stable ; trier reste une protection gratuite
- Une forme peut être détruite (vitre brisée) avant qu'un autre script lise l'événement de contact : tester `other.isValid`
- Audit du 2026-10-18 (époque blob, scène `Legacy/Blob/poc niveau 1`), pour ne pas le refaire : aucune divergence avec les
  touches appuyées (6 essais, nombreux scénarios). Banc de test réutilisable (`unity command eval_file`, hors dépôt) :
  entrées fixes + hash par pas des bits de tous les corps triés, temps d'un redémarrage. `Temp/` est vidé par Unity au
  redémarrage : garder les bancs dans le scratchpad. Ne pas oublier `TrainingMode._restoring` pendant un test
- Signaler tout risque de non-déterminisme plutôt que de l'ignorer
- Recommencer = `LevelRestart.Restart()` (R, la mort plus tard) : détruit toute la physique, `PhysicsWorld.Reset()`
  sur le monde vide (sinon l'historique interne de Box2D fait diverger les essais), puis recrée dans l'ordre de la hiérarchie.
  Le départ du niveau passe aussi par là (au chargement de la scène) : chaque essai est identique au bit près (~1 ms)
- Tout état à remettre à zéro hors physique (caméra, timer, musique…) s'abonne à `LevelRestart.Restarted`
  (et à `LevelSimulation.Jumped` pour un retour à une sauvegarde ; ignorer les événements pendant `LevelSimulation.FastForwarding`)
- Point de sauvegarde = entrées de chaque pas depuis le départ ; y revenir = redémarrage + rejeu accéléré (exact au bit près)

## Performance
Pourquoi c'est central : revenir à une sauvegarde rejoue TOUS les pas depuis le départ (120 par seconde de jeu). Le coût d'un
pas est donc multiplié par des milliers à chaque Tab, et un point loin dans le niveau coûte plus cher qu'un point proche du départ.
Une microseconde gagnée par pas = 3,6 ms de moins pour un retour à 30 s. Sur mobile, compter 2 à 4× plus lent que sur PC.

Philosophie (tout nouveau script, en particulier tout `LevelTick`) :
- **Ne jamais changer la simulation pour gagner du temps.** Une optimisation doit donner un résultat identique au bit près
  (sinon les sauvegardes, replays et fantômes existants ne retombent plus au même endroit). Changer le comportement physique
  (sous-pas, `aabbMargin`, ordre de création…) est une décision de jeu, pas une optimisation : la proposer, ne pas l'imposer
- **Mesurer avant d'optimiser**, puis après. Pas d'optimisation à l'aveugle : trouver d'abord ce qui coûte (chronométrer chaque
  `LevelTick`, `Simulate`, profil du monde `PhysicsWorld.profile`)
- **Préparer au départ, pas à chaque pas** : tout ce qui ne change pas pendant un essai (géométrie, listes d'enfants, composants,
  décalages, angles) se calcule dans `OnEnable` (chaque redémarrage, donc chaque Tab), pas dans `LevelTick`.
  Une modification d'inspecteur en Play s'applique au redémarrage suivant : c'est accepté
- **Un appel au moteur physique ou à Unity coûte cher** (lire `body.position`, `GetContacts`, `userData`, écrire un `Transform`,
  `GetComponents` : 0,05 à 0,5 µs chacun). Les éviter dans les boucles par pas : écarter d'abord par un test bon marché
  (test de boîte avant les membres, comme la hitbox du `Skater`), lire une fois puis réutiliser, ne pas écrire un Transform
  dont personne n'a besoin en jeu (obstacles sur trajectoire : le corps fait foi)
- **Se faire prévenir plutôt que demander à chaque pas** : événements de Box2D (début / fin de contact via `contactEvents`)
  au lieu d'interroger chaque corps à chaque pas
- **Aucune allocation par pas** (pas de `new`, de LINQ, de `List` créée dans `LevelTick`) : listes réutilisées ou tableaux préparés au départ
- **Sauter le calcul quand rien n'a changé** (ex. pose du skateur recalculée seulement si ses entrées changent)
- **Réglages du monde qui ne changent pas le résultat** : `simulationWorkers` = 1 (Box2D sur 12 threads passait plus de temps à
  coordonner les cœurs qu'à calculer : −33 %, et encore plus utile sur mobile). Résultat identique quel que soit ce nombre
- Le visuel (`LateUpdate`, `Cosmetics/`, dessin debug, dessin du personnage) ne tourne qu'une fois par image, jamais pendant le
  rejeu accéléré : il compte moins que la simulation, mais reste léger pour le mobile

Repères mesurés (PC, 1 thread) : `poc niveau 2` (un blob de 32 capsules, beaucoup d'obstacles) ≈ 55–60 µs par pas ;
ancien `skate reboot` ≈ 13,5 µs. Repères du jeu de slimes : à mesurer (étape 1 de la roadmap).

Vérifier une optimisation (méthode utilisée) :
1. Avec l'ancien code, en Play : redémarrer, rejouer N pas (`LevelSimulation.FastForward` avec des entrées fixes, flèches et
   Espace compris, et un scénario de mort), enregistrer les bits des positions / vitesses de tous les corps
   (`BitConverter.SingleToInt32Bits`), et chronométrer (meilleur de plusieurs essais : la machine varie de ±10 %)
2. Recompiler, refaire exactement pareil : « identique au bit près » obligatoire, puis comparer les temps
3. Pendant ces tests, `LevelRestart.Restart()` efface les points du `TrainingMode` de l'utilisateur : mettre son `_restoring` à vrai
   (réflexion) AVANT CHAQUE `Restart` (le retour au point jaune lancé au début du Play le remet à faux en cours de test), et
   sauvegarder d'abord `UserSettings/TrainingCheckpoints/<scène>.json`. Pour remettre ce fichier : Play arrêté, copier, puis
   relancer Play (l'objet chargé en mémoire hors Play peut sinon réécrire l'ancienne liste vide)

## Conventions de code
- Pas de commentaires, sauf point vraiment non évident
- Paramètres exposés : toujours `[Header]` + `[Tooltip]` (en français, précis sur l'effet et les unités),
  plus `[Min]` / `[Range]` quand pertinent
- Vitesses linéaires exposées : stockées en m/s (unité du code et de Box2D), affichées et saisies en km/h dans l'inspecteur
  grâce à `[SpeedKmh]` (`Core/SpeedKmhAttribute`, dessin `Editor/Core/SpeedKmhDrawer`, float ou Vector2, respecte `[Min]`) ;
  le tooltip parle en km/h. Valeur réécrite seulement si on la modifie : sans effet sur la simulation. Pas pour les rotations
  (°/s) ni les accélérations (m/s²)
- Noms de variables et classes en anglais ; champs privés préfixés `_` (exception : les champs publics français de `Ball2D`/`monde_settings`, gardés pour ne pas perdre les valeurs)
- Un script = une responsabilité ; ne pas renommer les champs publics existants (valeurs d'inspecteur perdues)
- Maths avec `Unity.Mathematics` pour la physique ; dimensions en unités Unity : toujours préciser
  si un paramètre est relatif à une échelle ou absolu

## Façon de travailler
- Modifications ciblées : ne toucher qu'aux fichiers demandés
- Proposer un plan avant les changements importants
- Réponses courtes : dire ce qui a été modifié, sans longs développements
- Mettre à jour ce fichier quand une décision de projet est prise (vision, roadmap, conventions)
- Problème de fond repéré (philosophie du jeu, game design, direction artistique, frustration du joueur), même s'il touche un
  pilier : le signaler en quelques lignes avec une piste, sans bloquer ni retarder la tâche demandée (qui reste faite comme demandé)

## Éditeur Unity (CLI)
- L'Éditeur ouvert est pilotable via le CLI `unity` (`unity status`, `unity list`, `unity command eval '...'`) :
  modifier la scène par ce biais plutôt qu'en éditant les fichiers
- Ne pas sauvegarder la scène (`save_scene`) sans accord
- Chemins de sortie (captures…) : obligatoirement dans le projet, sinon refus ; éviter `Assets/`
- Les gizmos n'apparaissent pas dans `capture_scene_view` : inspecter la géométrie par `eval`
- Git : le dossier appartient à un autre compte Windows → `git -c safe.directory="C:/Users/dev/Documents/blob project" ...` ;
  pas d'identité git configurée : les commits utilisent `-c user.name=Blob -c user.email=Blob@local.invalid` (comme l'historique)

## À ne pas toucher
- Fichiers `.unity`, `.prefab`, `.meta` (modifiés dans l'éditeur Unity)
- Dossiers `Library/`, `Temp/`, `Logs/`, `obj/`
- Ne jamais supprimer de fichiers sans me demander d'abord
