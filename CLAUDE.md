# Projet

Jeu 2D de SKATE à momentum (inspirations : Elasto Mania, Alto's Adventure, Rider, Trials, OlliOlli). Le but de chaque
niveau : amener le SKATEUR (`Skater`) jusqu'à la fin, d'une traite, en GARDANT SA VITESSE. Aucun moteur ne le fait avancer :
la vitesse vient des pentes et des éléments du niveau (flippers, bumpers, accélérateurs…). Le joueur contrôle la rotation
de la planche (flèches : bascule au sol, frein sur le tail, figures en l'air) et active les éléments du niveau (Espace).
Le skateur meurt si sa tête ou son corps se cogne, ou s'il touche un obstacle. Niveaux construits avec des courbes de
Bézier épaisses (sol, rampes, murs) et des éléments posés. Plusieurs niveaux prévus.

Ancienne version : le jeu était centré sur un BLOB (balle souple qu'on ne contrôlait pas, poussée par des leviers de flipper).
Abandonné (2026-10-19) : chaotique, peu prévisible pour un jeu de précision, passif en l'air. Tout l'état de cette époque
(code, scènes, CLAUDE.md, LEVEL_DESIGN.md) est conservé par le tag git `blob-et-debut-skate` ; le script reste en reliquat
(`Scripts/Legacy/Blob/`).

RETOUR AU BLOB (décision du 2026-10-20) : le skateur est abandonné comme personnage principal ; le jeu redevient centré sur
le BLOB, avec une action du joueur en plus de rouler : le lest (flèche bas, `BLOB.heavyKey`). Le skate reviendra plus tard
comme GADGET qui change le gameplay (comme le grappin). Les sections ci-dessous décrivent encore l'époque du skateur : à
réécrire (vision, roadmap, `Legacy/Blob/` à sortir du reliquat).

## Piliers (ne changent pas)
- Gameplay d'abord : la physique et le design des niveaux priment, l'esthétique et les sons viendront plus tard
- Momentum : la satisfaction vient de garder (et gagner) de la vitesse, de prendre les courbes proprement, de réussir ses
  figures et ses réceptions. Pas de bouton « avancer »
- Résultat TOUJOURS déterministe : mêmes entrées = même simulation à chaque lancement, au bit près
  (indispensable pour un jeu de niveaux à rejouer / mémoriser, et pour les sauvegardes, replays et fantômes)
- Le joueur contrôle le skateur (rotation, frein) et active les éléments du niveau ; les gadgets donnés par le niveau
  (ex. grappin) s'ajoutent à ces commandes
- Performance cruciale, mobile compris : le jeu doit rester fluide et le retour à une sauvegarde (Tab) quasi instantané
  (voir « Performance »)

## Roadmap (étape actuelle en gras)
1. **Mécanique du skateur + éléments de level design**
   - Fait : skateur jouable (`Skater`, scène `skate reboot`), éléments de niveau (courbes, flippers, bumpers, obstacles,
     trajectoires, accélérateurs, gadgets + grappin, vitre destructible `GlassPane`), caméra, mode entraînement
   - Fait (2026-10-19) : planche et personnage séparés (voir « Skateur ») : la planche (`Skateboard`) fait seule le mouvement
     (bascules, saut chargé, rotation en l'air), le personnage (`Skater`, optionnel) est une animation pilotée par la physique
     (corps sur ses jambes) avec chutes physiques (choc, réception, perte d'équilibre) et ragdoll
   - En cours : régler au clavier les règles de chute et la posture (tout est en cases à cocher / paramètres) ; rendre le skate
     plus satisfaisant (voir « Skateur : pistes ») ; adapter au skateur les éléments écrits pour le blob (« À adapter au skateur »)
   - Idée (personnage) : s'accroupir / attraper la planche pour tourner plus vite en l'air. Aujourd'hui : `grabSpinFactor`
     (touche de saut tenue en l'air), seule influence du personnage sur la physique de la planche
   - À venir : figures = boost (une figure réussie rend de la vitesse) ; point jaune façon Geometry Dash avec micro-gel visuel ;
     éléments physiques / obstacles neutres activés par des déclencheurs du niveau. Idée de mise en scène : le skateur longe
     une vitre très vite, un marteau au coin de la vitre s'abat et la brise, il passe dans la gerbe
2. Boucle de jeu : départ, arrivée (fin de niveau), échec / recommencer. But : finir le niveau d'une traite (comme Geometry Dash)
   - Mode entraînement (`TrainingMode`) : points de sauvegarde exacts pour s'entraîner sur un passage.
     Fait : S / Tab / Shift / R + fantômes + 2 modes de sauvegarde. À venir (mobile) : toucher un fantôme pour choisir
     le point à pratiquer (`TrainingMode.GoTo`) ; Tab ramène ensuite toujours au dernier point pratiqué
   - Replays et fantômes : un replay = identifiant + version du niveau + entrées de chaque pas (quelques Ko), rejoué par la
     simulation (pas une vidéo). À vérifier avant : déterminisme entre machines différentes (PC / mobile)
3. Plusieurs niveaux
4. Esthétique, sons

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
- Référence : le skateur, `Skater.height` = 1,35 m (chevilles → sommet de la tête). Planche de référence (valeurs étalons de
  l'utilisateur, objet « Skateboard seul » de `skate reboot`, défauts du script) : 1,5 m de long, empattement 0,75 m, roues de
  9,75 cm, trucks 9,6 cm, plateau 1,8 kg + 2 roues de 0,2 kg, `scale` = 1 (multiplie toutes ses longueurs ; 0,6 = taille d'un vrai
  skate pour ce personnage). Gravité de la scène : −9,81.
  Objets mobiles entre 0,1 et 10. Balle de test (`Ball2D`) : diamètre 1 (tailles de balles toujours en diamètre)
- Échelle des Transform des éléments de niveau toujours à (1, 1, 1) : la taille se règle par les paramètres
  (`size`, `thickness`, points…), sinon épaisseurs et cercles se déforment
- Régler la sensation de vitesse par la gravité / vitesse du jeu, pas par la taille
- Épaisseur des courbes : visuelle seulement (la collision est une chaîne fine)
- Mobile : caméra ortho ≈ 9 sur `skate reboot` (≈ 5–6 à l'époque du blob) ; taille du skateur à l'écran à valider
- Flipper / bumper kinematic : pas de collision continue contre ce qu'ils frappent (`fastCollisions` n'y change rien quand c'est
  le levier qui va vite). Le bumper donne d'avance le choc aux corps qu'il va atteindre (anti-traversée) ; le flipper non (inutile
  à ≤ 900 °/s, voir `PinballFlipper`)

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

Repères mesurés (PC, 1 thread, 2026-10-19) : `skate reboot` ≈ 13,5 µs par pas complet en roulant, dont ≈ 8,6 µs pour la planche
seule et le niveau, ≈ 4,9 µs pour le personnage (`Skater`). Vitre qui se brise : +30 à 50 µs par pas pendant 1 à 3 s.
Anciens repères (époque blob, `poc niveau 1`, plus gros niveau) : ≈ 50 µs par pas, dont ≈ 40 µs pour Box2D. À remesurer sur
un vrai niveau de skate.

Vérifier une optimisation (méthode utilisée) :
1. Avec l'ancien code, en Play : redémarrer, rejouer N pas (`LevelSimulation.FastForward` avec des entrées fixes, flèches et
   Espace compris, et un scénario de mort), enregistrer les bits des positions / vitesses de tous les corps
   (`BitConverter.SingleToInt32Bits`), et chronométrer (meilleur de plusieurs essais : la machine varie de ±10 %)
2. Recompiler, refaire exactement pareil : « identique au bit près » obligatoire, puis comparer les temps
3. Pendant ces tests, `LevelRestart.Restart()` efface les points du `TrainingMode` de l'utilisateur : mettre son `_restoring` à vrai
   (réflexion) AVANT CHAQUE `Restart` (le retour au point jaune lancé au début du Play le remet à faux en cours de test), et
   sauvegarder d'abord `UserSettings/TrainingCheckpoints/<scène>.json`. Pour remettre ce fichier : Play arrêté, copier, puis
   relancer Play (l'objet chargé en mémoire hors Play peut sinon réécrire l'ancienne liste vide)

## Level design
- Règles de conception des niveaux : `LEVEL_DESIGN.md`, à lire AVANT de créer ou modifier un niveau
- `skate reboot` (`Assets/Scenes/`) : piste de test du skateur (gravité −9,81, caméra ortho 9), sous `Track` (racine en (0, 40)) :
  descente, plat (frein), kicker + fosse à piques, flipper (Espace) au bout d'une pente, grande réception, vitre posée au sol (la
  planche la brise), quarter pipe. Bumper (Espace) sous le plat en (24, 39.7). Skateur = objet « Skateboard seul » (`Skateboard` +
  `Skater`), posé sur le bumper ; départ classique sur le haut de la descente (≈ (2.1, 47.75), −28,4°). L'ancien objet `Skater`
  (désactivé) est un reliquat. Mesuré sans touche : passe le kicker, tombe à la grande réception (planche arrivée cabrée de 60°) ;
  avec une rotation en l'air pour se recaler, toute la piste passe. Scène de référence en attendant un premier vrai niveau de skate
- Jonction courbe → élément (flipper au bout d'une pente) : une roue de 5 cm sent un décalage de quelques mm. L'élément doit
  être légèrement SOUS la surface d'arrivée (la roue tombe dessus), jamais au-dessus
- Reliquats (`Assets/Scenes/Legacy/Blob/`) : `poc niveau` (ancienne scène modèle des réglages), `poc niveau 1` (objets de
  test : gadgets, trajectoires, vitres), `level1 by claude`, `level2 by claude`, `reboot`. Niveaux pensés pour le blob :
  utiles comme banc de test des éléments, pas comme référence de design

## Organisation des scripts
- `Scripts/Skater/` : le personnage principal (`Skateboard` = planche et mouvement, `Skater` = personnage optionnel)
- `Scripts/Level/` : tout ce qui se pose dans un niveau (courbes, flippers, `Hazards/` obstacles, `Neutral/` éléments neutres,
  `Gadgets/` gadgets et leurs zones / points, `Destructibles/` éléments à briser).
  C'est la palette du futur éditeur de niveaux en jeu : code utilisable en build (pas de `UnityEditor`)
- `Scripts/Core/` : moteur (simulation pas à pas, entrées, redémarrage, réglages du monde)
- `Scripts/Game/` : un exemplaire par scène, hors niveau (caméra, compteur de vitesse, mode entraînement)
- `Scripts/Test/` : outils de test, pas des éléments de jeu (`Ball2D` sert à comparer des comportements)
- `Scripts/Legacy/Blob/` : reliquat de l'ancienne version (`BLOB`), gardé pour les anciennes scènes ; ne plus l'étendre
- `Scripts/Cosmetics/` : effets purement visuels ; ils LISENT l'état d'un corps (jamais d'écriture, jamais dans
  `LevelTick`), dans `LateUpdate`. Prévus aussi pour un fantôme / un ami
- `Editor/Level/` : inspecteurs et poignées Scene view des éléments de `Level/`. Le dossier `Editor` n'est PAS inclus
  dans le jeu : le futur éditeur en jeu devra réimplémenter ses poignées, en réutilisant la géométrie des composants
- `Editor/Game/` : outils éditeur des scripts de `Game/` (sélection des fantômes du mode entraînement) ; `Editor/Core/` : dessins
  d'attributs (`SpeedKmhDrawer`)
- Déplacer / renommer un script uniquement via Unity (`AssetDatabase.MoveAsset`) : le `.meta` suit, les scènes gardent leurs références

## Skateur (`Scripts/Skater/`)
ARCHITECTURE (2026-10-19, choisie après tests parmi : A = planche physique + animation à la main, B = personnage physique
complet à ressorts, C = planche physique + animation procédurale simplifiée) : C, avec une séparation stricte en trois couches.
B avait été essayé (« mou, pas satisfaisant ») ; mesuré : porter le poids d'un humain sur la planche n'améliore rien (60 kg aux
hanches : planche bloquée avant le flipper, freinage 5,2 m au lieu de 3,7 ; à mi-hauteur : retournée au quarter, freinage 7,7 m ;
au ras de la planche seulement : plus stable, vitesses identiques, mais ce n'est pas physique).
1. MOUVEMENT = la planche seule (`Skateboard`), un corps Box2D. Seul ce qu'on lui applique de l'extérieur la dirige : commandes
   (flèches, saut) et éléments du niveau. Le personnage ne la pousse jamais.
2. ANIMATION pilotée par la physique (`Skater`) : le corps sur ses jambes, lu sur la planche, jamais écrit dans la planche.
3. HITBOX simplifiée (requêtes de recouvrement des membres de la posture), puis RAGDOLL Box2D quand il tombe.
Seule exception, en option : `Skater.grabSpinFactor` (planche attrapée en l'air = rotation plus rapide).

`Skateboard` (géométrie, physique, commandes ; défauts = valeurs étalons de l'utilisateur, voir « Échelle ») :
- Géométrie : longueur, épaisseur, relevés nose/tail on/off + angle, roues, empattement, trucks (triangles de l'axe au plateau) ;
  `scale` multiplie toutes les longueurs (propriétés `Length`, `Wheelbase`… : toujours les utiliser, jamais les champs bruts),
  masses inchangées, épaisseur gardée ≥ 2,1 cm (soudure de Box2D) ; repère : origine au sol entre les roues, +X = nose ;
  `KickIndex` = polygone de chaque relevé. Une seule source pour l'aperçu et la collision.
- Corps : plateau, relevés, trucks (frottement `deckFriction`) et roues soudées sans frottement (`wheelFriction` = 0 : elles
  glissent = roulement parfait, rotation des roues seulement dessinée ; abandonné : vraies roues sur charnière, plafonnées par
  Box2D à ≈ 17 km/h). Masses `deckMass` / `wheelMass` par forme ; `truckInertia` (inertie = masse × (empattement / 2)²).
- Au sol (flèches) : la planche pivote sur la roue qui reste au sol vers le tail ou le nose (`StopAngle` + `tailPress` /
  `nosePress` : le bout posé est pressé, le frottement freine), jamais de vitesse le long du sol. Saut (flèche haut,
  `jumpKey`) : tenue = charge, relâchée = vitesse `jumpMinSpeed` → `jumpMaxSpeed` (charge `jumpChargeTime`) selon la normale du
  sol, au centre de gravité. En l'air (après `airDelaySteps`) : rotation autour de son centre de gravité, `airSpinSpeed`, freinée
  au relâchement. Saut tenu en l'air = le personnage attrape la planche (`GrabHeld`).
- Commandes coupées quand le personnage tombe (`Skater.Controllable`) : la planche devient libre.
- Expose son état au personnage : `Grounded`, `InAir`, `Lead` (sens de marche), `Intent`, `EndTouch`, `Tilt`, `JumpCharge`,
  `JumpVelocity`, `TouchingKinematic` (flipper, bumper), `HazardHit` (obstacle touché par la planche).
- `ICameraTarget` : centre de gravité de la planche et sa vitesse (`TryGetCameraVelocity`), qu'une rotation ne change pas
  (le torse, suivi avant, tournait avec elle à 0,9 m du centre, et la vitesse moyenne des membres variait de ±29 km/h : caméra et
  compteur secoués) ; torse une fois en ragdoll. `ITrainingBody` (planche + 10 membres du personnage s'il est là ; fantôme en
  bonhomme bâton), `IContactFxSource` (étincelles : planche sans les roues, puis ragdoll).

`Skater` (personnage, optionnel : composant décoché = planche seule) :
- Corps sur ses jambes : masse ponctuelle (hanches) en monde, gravité + ressort-amortisseur vers la posture voulue, dans l'axe des
  jambes (`legFrequency`, `legDamping`, précharge du poids quand la planche touche) et le long de la planche (`leanFrequency`,
  `leanDamping`). Le long de la planche les pieds ne le retiennent que par frottement (`footGrip` × charge) : trottoir = le corps
  continue. Il suit la ROTATION de la planche avec retard (`bodyRotationFollow`) : un rabat brutal (réception) ne le balaie pas,
  un looping oui. En l'air il se tient à la planche (`airFollow`), mais un écart de la planche à la chute libre est un choc
  (`airImpactSpeed`) même sans contact Box2D (collision continue à grande vitesse : planche arrêtée sans contact ce pas-là).
  Saut : son corps reçoit la même vitesse que la planche (sinon ses jambes s'écrasent).
- Butées : jambes (`maxCompression`), hanches le long de la planche (`maxLean`). Y arriver plus vite que `landingCrashSpeed` /
  `impactCrashSpeed` = chute ; sinon les genoux plient à fond / le buste part (`leanTorsoPitch`). `safeLaunchers` (coché) :
  la poussée d'un élément kinematic (flipper, bumper) plie les jambes sans faire tomber.
- Chutes : choc (butées), hitbox (tête / torse contre le décor, verre exclu ; membres contre les obstacles), planche contre un
  obstacle, planche bloquée hors sol (`stuckTime`), perte d'équilibre. Wheelies : `canFallForward` (défaut coché, nose tenu au
  sol `noseFallTime`) / `canFallBackward` (défaut décoché, tail tenu `tailFallTime`). Perte d'équilibre = transition : il
  bascule d'un bloc autour de son pied (pendule inversé sous la gravité, départ `fallStartSpeed`, bras qui partent vers l'avant)
  puis lâche à `fallDetachAngle` ou s'il se cogne. Ragdoll (10 corps, charnières à frottement `deadJointFriction`) créé à la
  posture actuelle avec la vitesse de SON corps (hanches + rotation suivie, ou bascule), pas celle de la planche ; empalement par
  les piques. `Skater.Died` (focus caméra).
- Posture : voulue (bascule, charge du saut `chargeCrouch`, en l'air `airCrouch`, planche attrapée `grabCrouch` /
  `grabTorsoLean` / `armGrab`, buste droit pendant une bascule `counterTilt`) + décalage du corps sur ses jambes, puis cinématique
  inverse ; bras ballants (`armFrequency`, `armSwing`). Recalcul sauté si rien n'a bougé de plus de 0,1 mm (déterministe).
  Pieds : écart `stance` × empattement, plafonné à `maxStance` × `height` (grande planche : les jambes n'y arriveraient pas).

Mesuré (PC, éditeur, `skate reboot`, réglages par défaut) : personnage ≈ 4,9 µs par pas (planche et niveau ≈ 8,6). Restart, rejeu
d'un bloc et par paquets exacts au bit près, chutes et ragdoll compris. Chute à plat : ≤ 4 m tient (genoux à fond), ≥ 5 m
tombe. Trottoir de 10 cm : ≤ 12 km/h tient (buste qui part de 0,2 m), ≥ 15 km/h projeté vers l'avant. Saut : 0,33 m (appui
bref) à 1,8 m (charge complète), réception tenue. Wheelie avant tenu : bascule 0,3 s après le contact du nose, lâche 0,23 s plus
tard. Frein au tail : 15 km/h → 0 en 3,7 m. Bumper : la planche seule part droite (0,00 °/s) ; personnage à revérifier avec l'anti-traversée.
En l'air, tourner ou non ne change pas la trajectoire du centre de gravité (identique au bit près jusqu'au contact, comme en
physique) ; seul le point qui touche en premier change (nose plus bas = contact plus tôt). Échelles 0,75 à 2 : piste passée.

Skateur : pistes pour plus de satisfaction (À VALIDER avec l'utilisateur, rien n'est décidé) :
- Réceptions : aujourd'hui une réception de travers fait tomber si le choc dépasse les butées (sinon rien). Récompenser une
  réception propre (vitesse gardée) rendrait l'air plus lisible
- Figures = boost (roadmap) : compter les rotations / figures en l'air et rendre de la vitesse à la réception propre
- Pump : gagner de la vitesse en fléchissant dans les creux et en se délestant sur les bosses (le corps sur ses jambes existe
  déjà : piste pour un pump visuel puis physique), cohérent avec le pilier momentum
- Saut (flèche haut) : existe désormais ; à juger en jeu (où décoller, hauteur, « pop » qui lève le nose ?)
- Courbes concaves serrées : la planche se cabre (accélération centripète v²/R) ; courbes plus larges dans les niveaux ou aide
- Lisibilité de la hitbox et des chutes (pourquoi je suis tombé ?) : ralenti / focus déjà présents, à juger en jeu
- Sensation de vitesse : caméra (anticipation, dézoom selon la vitesse) à régler pour le skateur
- Axe de rotation en l'air : aujourd'hui le centre de gravité de la planche (le personnage ne pèse rien). Un vrai skateur tourne
  autour du centre de gravité commun, vers ses hanches (≈ 30 fois plus lourd que la planche) : la planche tournerait autour de lui
  et l'endroit où elle touche le sol dépendrait davantage de la rotation. Piste : point de rotation virtuel réglable en hauteur
- Limite : le corps traverse une vitre (seule la planche la brise)

## À adapter au skateur (écrits pour le blob, lisent `BLOB.Active`)
Piste proposée : une interface commune (ex. `IRider` : liste statique des actifs dans l'ordre de création, masse, vitesse,
position, corps à accélérer), implémentée par `Skateboard` (planche) et gardée par `BLOB` pour les anciennes scènes.
- `BoostZone` : accélère les capsules du blob → accélérer la planche
- `GadgetZone`, `GadgetHolder` (`[RequireComponent(typeof(BLOB))]`), `BlobGadget`, `GrappleGadget` (accroche sur une capsule) :
  à porter sur le skateur (accroche sur la planche ou le centre de gravité)
- `GlassPane` : prédiction de casse avant contact (masse et vitesse moyenne du blob) et morceaux qui ignorent le blob
  (`BLOB.CategoryBit`) → même chose pour le skateur (aujourd'hui seule la planche casse la vitre, par contact)
- `SpeedMeter` (repli sur le premier BLOB), `CameraFollow` (`BLOB.Broken`) : repli / focus sur le skateur
- Nommage : `BlobHazard` (classe de base des obstacles, tue aussi le skateur) et `BlobGadget` portent encore le nom du blob ;
  les renommer (via Unity, la classe garde son fichier) ne fait pas perdre les références des scènes
- Commentaires / tooltips qui citent le BLOB (`ITrainingBody`, `CameraFollow`, `SpeedMeter`, `Ball2D`, `IContactFxSource`)

## Construction des niveaux : modèle CurvePath
Tout ce qui sert à créer un niveau suit le modèle existant (CurvePath2D / PinballFlipper) :
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur
- Valeurs par défaut des scripts = réglages officiels (voir `LEVEL_DESIGN.md`).
  Un nouveau niveau utilise les scripts sans retoucher ces paramètres
- Aperçu visible dans la Scene view AVANT de lancer le jeu : `OnDrawGizmos` (+ Editor custom
  avec handles si besoin), avec la même géométrie que celle envoyée à la physique
- La physique n'est créée qu'en Play (`Application.isPlaying`), dans `OnEnable`, et détruite dans `OnDisable`.
  Le composant implémente `ILevelPhysics` : `LevelRestart` le désactive / réactive pour recommencer le niveau
  (tout corps créé hors d'un `ILevelPhysics` empêche la remise à zéro du monde)
- Une seule source de vérité géométrique pour aperçu et collision
- Confort d'édition dans la Scene view (obligatoire pour tout nouvel élément) :
  - Zone de clic pleine : dans `OnDrawGizmos`, remplir un `GizmoPickArea` avec la même géométrie (polygones, cercles,
    bande d'une courbe) et appeler `Draw` ; le libérer (`Dispose`) dans `OnDisable`. Invisible, éditeur uniquement :
    l'objet se sélectionne en cliquant n'importe où sur sa surface, pas seulement sur le trait
  - Ne jamais réserver les clics en permanence dans `OnSceneGUI` : `HandleUtility.AddDefaultControl` seulement pendant
    une action précise (ex. Ctrl enfoncé pour insérer un point), sinon on ne peut plus sélectionner un autre objet
    ni désélectionner en cliquant dans le vide
  - Aperçu non sélectionné complet (contour fermé, bouts compris) : même forme que l'aperçu sélectionné

Scripts existants :
- `CurvePath2D` : courbe de Bézier cubique par morceaux, épaisseur uniforme (center/left/right),
  `collisionMode = Outline` par défaut (nouvelles courbes solides des deux côtés),
  échantillonnage adaptatif (`resolution`, défaut 0,3 partout : moins de points = plus rapide, `straightTolerance`, `miterLimit`),
  `BuildCollisionChains()` pour la collision
- `CurvePath2DEditor` : édition dans la Scene view (points, tangentes, modes Free/Aligned/Vector/Auto, Undo)
- `CurveCollider2D` : crée la collision statique du chemin, uniquement en chaînes Box2D (`PhysicsChain`).
  Les quads (polygones jointifs) ont été abandonnés : collisions fantômes aux jointures → rebonds parasites.
  Le léger « accroc » des chaînes à très haute vitesse est accepté (atténué par le pas à 120 Hz).
  Pour un mur épais solide des deux côtés : `collisionMode = Outline`
- `PinballFlipper` : flipper, charnière à moteur (`useTorque`) ou kinematic piloté en vitesse ; touche Espace (`FlipperKey`).
  Pour le skateur : levier au bout d'une pente qui le projette, ou tremplin tenu. Collision = cercle de base + UN polygone arrondi
  au rayon du bout (dessus, dessous, bout) dont la base est enfoncée de 0,025 m dans le cercle (`SeamInset`) : l'ancien quad posé
  sur le cercle faisait un coin fantôme (planche freinée et projetée en passant sur la base). Le dessus a donc un creux de ≈ 2 cm
  près de la base. En `useTorque`, masse un peu différente (2 formes au lieu de 3).
  Pas d'anti-traversée (essayé puis retiré le 2026-10-20) : mesuré sur `poc niveau 1`, à 900 °/s (bout à 0,22 m par pas) le
  levier n'entre que de ≤ 7 cm dans le blob et ne traverse rien ; à 2500 °/s (0,61 m par pas) il entre de 30 cm (presque une
  capsule). La version anti-traversée (comme le bumper, en rotation : temps d'impact, choc donné d'avance, corps déjà posés
  laissés à Box2D) ramenait ça à 16 cm, mais coûtait jusqu'à 38 µs par pas (blob dessus) pour rien aux réglages actuels.
  À reprendre si des leviers dépassent ≈ 1500 °/s
- `PinballBumper` : levier bumper, capsule de la longueur d'un flipper (deux boules au rayon de la grosse boule du flipper,
  première boule sur l'objet). À l'appui, translation perpendiculaire à la barre de `travel` (m, signe = côté), à `speed`
  (défaut = vitesse du bout d'un flipper, ≈ 14,66 m/s), retour au relâchement. Kinematic piloté en vitesse, même touche
  (`FlipperKey`) ; arrive pile sur la cible (`PinballBumperEditor` : poignée du déplacement). Anti-traversée : à chaque pas où il
  bouge, il lance sa forme sur son trajet, et chaque corps dynamique qu'il atteindra pendant ce pas reçoit d'avance le choc d'un
  corps de masse infinie (sans rebond ni frottement), une seule impulsion par corps au milieu de ses points de contact extrêmes :
  il part à la vitesse du bumper et reste devant lui. Mesuré (2026-10-20) : planche lancée droite (0,00 °/s à 53 et 200 km/h,
  avant : 5 à 15 °/s) ; blob à 200 km/h jamais coincé, vitesse de sortie continue selon le moment de l'appui (avant : appui
  1 pas trop tôt = bumper dans le blob, 108 au lieu de 195 km/h), enfoncement résiduel ≤ 16 cm au tout premier pas (les joints du
  blob retiennent la capsule poussée). ≈ 2 µs par pas, seulement quand il bouge. Rejeu exact au bit près
- Obstacles (`BlobHazard`, abstrait, nom historique) : tuent le skateur (planche par contact, membres par la hitbox). Corps
  statique par défaut, formes marquées par `userData.objectValue = this` et par la catégorie `CategoryBit` (requêtes ciblées :
  hitbox du skateur, coupe de la corde du grappin ; contacts inchangés), `contactEvents` sur leurs formes (sans effet sur la
  simulation), géométrie unique `BuildGeometry` (polygones convexes + cercles) pour aperçu et collision ;
  `userData.intValue` = 1 + index du polygone. `OnCapsuleTouched` : appelé (ordre fixe) pour chaque membre mort qui le touche.
  Polygone que Box2D refuserait (soudure < 0,02 m, sommet à ≤ 0,01 m de la droite de ses voisins, ex. pique minuscule) : écarté
  avant `PolygonGeometry.Create` (`Box2DAcceptsHull`). Chaque refus coûtait 3 avertissements (≈ 0,2 ms chacun) à chaque
  redémarrage, simulation identique au bit près (le polygone n'existait pas)
  - `SpikedHazard` (abstrait) : un pique est le même objet partout, seul son placement change (`SpikeFrame`).
    Taille individuelle par pique (`sizes`), empalement des corps qui touchent un pique (joint fixe souple vers un point
    enfoncé de `impaleDepth`). Poignée par pointe dans la Scene view (`SpikeHandles`)
  - `CircularSaw` : piques (dents) autour d'un disque, kinematic en rotation (`rotationSpeed` en degrés/s, modifiable en Play) ;
    les corps empalés tournent avec la scie (`CircularSawEditor` : poignées diamètre / dents)
  - `SpikeRow` : piques en rangée (nombre, taille globale, inclinaison) (`SpikeRowEditor` : taille globale, nombre)
  - Nouvel obstacle = sous-classe de `BlobHazard` ; menu GameObject > 2D Object pour créer
- `MovingPath` : trajectoire. Sur le même objet qu'un `CurvePath2D` (forme éditée comme une courbe, sans `CurveCollider2D`) ;
  chaque enfant actif = un élément (scies, piques, boosts… mélangés), tous à la même `speed` (m/s), `reverse` pour le sens.
  Fermée = boucle ; ouverte = aller-retour (`PingPong`) ou `Teleport`. `alignToPath` (décoché par défaut) : orientation selon la courbe.
  `evenSpacing` + `phase`, sinon `positions` (fraction d'un cycle). Position = distance cumulée remise à zéro dans `OnEnable`
  (restart / rejeu exacts). Au départ elle pose le Transform des enfants (avant que leur `OnEnable` crée leur corps : ordre de la
  hiérarchie) ; ensuite un enfant avec corps implémente `IPathRider` (`BlobHazard` : corps kinematic piloté en vitesse vers la pose
  du pas suivant, Transform laissé au départ en jeu), les autres suivent par leur Transform (`BoostZone`).
  Hors Play, enfants posés à leur position de départ ; un enfant déplacé à la main est ramené sur la trajectoire (sa position suit).
  `PathShapes` (build) : cercle, triangle, carré, polygone régulier, échelle, rotation. `MovingPathEditor` : nombre d'enfants
  (duplique / retire le dernier), boutons + Scie / + Piques / + Boost, formes
- Éléments neutres (`Neutral/`) : ni bons ni mauvais, ils changent le mouvement sans tuer. Agissent dans `LevelTick`
  - `BoostZone` : rectangle d'accélération (flèche = +X local), sans corps physique, à la Trackmania : chaque corps dont
    le centre est dans la zone reçoit `acceleration` (m/s²) dans l'axe, jusqu'au plafond `maxSpeed` ; jamais de freinage,
    le reste du mouvement est conservé. Même entrée = même sortie au bit près ; entrée différente = sortie différente.
    Ne lit encore que les blobs (voir « À adapter au skateur »).
    Abandonné : la zone « remise à zéro » (sortie identique quelle que soit l'entrée) : l'historique interne de Box2D
    (numéros des contacts) faisait diverger la suite au premier contact.
    Variantes prévues : même composant, autres valeurs / couleur. Idée : zone verticale = tube qui transporte
    (`BoostZoneEditor` : poignées longueur / hauteur)
- Gadgets (`Gadgets/`) : capacités actives que le niveau donne au personnage, déclenchées par une touche (`FlipperKey`, donc
  enregistrée par `LevelInput`). Réglages sur un composant de gadget posé sur le personnage (défauts du script = valeurs
  officielles) ; `GadgetHolder` (ajouté avec) gère les emplacements (`maxGadgets` = 1 : un nouveau gadget remplace le plus
  ancien), remis à zéro à chaque redémarrage. `equippedAtStart` pour tester sans zone. Aucun corps créé tant qu'un gadget ne
  sert pas : porter un gadget sans s'en servir simule au bit près comme sans. Écrits pour le blob : à porter sur le skateur
  - `GadgetZone` : cercle sans corps (contour, + = don, × = retrait), test géométrique dans `LevelTick` comme `BoostZone`
  - `GrapplePoint` : rond rouge sans collision, `GrapplePoint.Active` ; position lue au départ (pas encore sur `MovingPath`)
  - `GrappleGadget` (grappin) : à l'appui, vise le point le plus proche dans la portée (`range`), en vue (`CastRay` : tout corps
    non dynamique cache), dans le cadre de l'écran (`screenOnly`, `screenHalfSize` : cadre FIXE centré sur le personnage, jamais
    lu sur la caméra, sinon la simulation dépendrait du format d'écran) et dans `viewAngle` autour de la vitesse ; vérifié au
    tir seulement. Filament qui voyage (`shotSpeed`), accroché tant que la touche est tenue. Options à tester : `attach` (corps
    le plus proche via joint de distance Box2D vers un corps statique d'ancrage / centre = correction de vitesse répartie),
    `ropeMode` (rigide = limite max, retenu / élastique = ressort qui ne pousse jamais, pas aimé), `reelInSpeed`,
    `wrap` (pivots par lancer de rayon, coin affiné par dichotomie, déroulement au changement de côté),
    `centerDeforms` (mode Center, décoché par défaut : traction uniforme = le blob ne se déforme pas, comme sous la gravité ;
    coché : même élan total, réparti 1 ± `centerDeformAmount` × cos selon que la capsule fait face au point ou non : le blob
    s'étire vers le point ; mesuré en l'air : 0 cm uniforme, 21 cm à 0,6, 34 cm à 1), `returnMode`
    (PassThrough = particules Verlet du script / Collide = chaîne de petits corps Box2D, bout tiré en kinematic).
    Corde accrochée de longueur fixe, simulée à chaque pas (dans la simulation : elle sert à la coupe et au retour) : Verlet +
    passes FABRIK à maillons de longueur exacte (`ropeSegmentLength`, `ropeIterations`), l'excédent ploie et pend ;
    tendue = ligne droite sans calcul, plus une vibration amortie quand elle se tend (plafonnée à `twangAmplitude`,
    oscillateur en additions/multiplications : `twangFrequency`, `twangDamping`). Elle ne heurte pas le décor (seul le
    segment droit ancre → personnage s'enroule). Dessin lissé (Chaikin) et recalé sur le corps accroché (la corde est
    calculée avant le pas). `ropeVisual` : Soft (corde simulée) / Straight (trait droit) = rendu seulement, même physique.
    Joints du grappin sans dessin debug (`worldDrawing`).
    `spikesCut` : un `SpikedHazard` (pique, scie) qui touche la corde la tranche (test de boîte puis lancer de rayon par
    maillon) ; le bout côté point reste pendu (décor, animé dans `LateUpdate` par relaxation des deux bouts de chaque maillon :
    un simple « suivi du premier » ajoutait de l'énergie et le faisait tournoyer ; effacé si on se raccroche au point), le reste
    revient. Retour = rembobinage qui accélère (`reelSpeed`, `reelAcceleration`), maillons avalés au centre ; nouveau tir
    possible une fois rentré. Coût mesuré du `LevelTick` (sur le blob) : ≈ 0 rentré, 3,4 µs accroché, +2 µs avec `wrap`.
    Restart et retour de sauvegarde exacts au bit près dans tous les modes, coupe comprise
    (`GadgetZoneEditor`, `GrapplePointEditor` : poignée du rayon ; menu GameObject > 2D Object)
- Destructibles (`Destructibles/`) : éléments à briser
  - `GlassPane` : vitre (rectangle vu de profil, `length` × `thickness`, défaut 9,25 × 0,36), un seul corps tant qu'elle est intacte :
    fixe (statique) ou libre (`freeBody` : dynamique, elle tombe, se cogne et se brise par son propre élan et sa masse).
    TOUT corps peut la casser (la planche du skateur la brise par contact). Énergie du choc = ½ × masse réduite × (vitesse
    relative dans l'axe du choc)² × fragilité au point (profil de flexion, milieu = 1, bouts = `edgeFragility`) ; contre un
    corps fixe, masse réduite = masse de celui qui bouge.
    - Prédiction AVANT contact dans `LevelTick` (masse totale, vitesse moyenne, `detectionMargin`) : écrite pour le blob
      (`BLOB.Active`), à porter sur le skateur.
    - Autres corps : lus sur les contacts du dernier pas (seulement quand la vitre est touchée : `contactEvents`). Un point est
      un choc si sa vitesse d'approche d'avant le pas (`normalVelocity`) ≥ 0,5 m/s, sinon un appui (Box2D crée parfois le
      contact un pas avant : « nouveau / installé » ne suffit pas). Points spéculatifs ignorés. Si la vitre cède, ce qu'elle a
      fait pendant ce pas est repris : vitesse normale d'approche rendue au corps en choc, impulsion reprise pour un appui,
      vitre libre remise à sa vitesse et rotation d'avant le pas (`totalNormalImpulse` ne convient pas sur un contact qui
      dure : il injectait des rotations folles).
    Casse : l'impacteur paie l'énergie de rupture (`energyAbsorbed`) et une part `momentumCost` de l'élan des éclats emportés
    (ceux sur sa trajectoire, largeur + `burstRadius`). 1 = conservation exacte : une vitre épaisse et lourde l'arrête presque.
    En dessous du seuil : rebond, fissures dessinées et affaiblissement (`crackThreshold`, `fatigue`). Charge (posé, roulement) :
    force d'appui, `loadStrength` + fluage (`creepRate`) ; vitre libre : le sol qui la porte ne compte pas.
    Motif (`GlassFracture`, instance partagée, pur et sans allocation) : radiales ondulées (`waviness`) qui se ramifient
    (`branching`), anneaux courbes (`curvature`) et discontinus en quinconce (`ringContinuity`), écart plafonné (`maxRingGap`) ;
    plus fins et plus nombreux quand l'énergie double. Chaque morceau est un polygone quelconque, découpé en parties convexes
    ≤ 8 sommets (oreilles + fusion de Hertel-Mehlhorn) puis par le rectangle : un corps à plusieurs formes.
    Dessin par la vitre dans `LateUpdate` (contour du morceau sans les coutures internes, remplissage `fillAlpha`).
    Parties trop fines pour Box2D (soudure 0,02 m, épaisseur 0,012 m) écartées AVANT `PolygonGeometry.Create` (chaque refus =
    un avertissement Unity de ≈ 0,2 ms). Éclats sous `minFragmentSize` = poussière.
    `crackSpeed` > 0 : propagation visible, `supportedEnds` (vitre fixe) : grands morceaux tenus par le cadre (`crackedStrength`).
    Morceaux = corps dynamiques (`maxFragments`) qui dorment ; `blobInteraction` Collide / Ignore / Pulverize (catégorie
    `LooseFragmentBit`) ; un morceau peut casser une autre vitre. `removeBelow` (30 m sous la vitre) : morceaux tombés dans le
    vide retirés ; `cleanupDelay` (0 = jamais). Préréglages (`ApplyPreset`) : Window (défauts), Tempered, Sugar, Armored, Cinematic.
    Ordre indépendant de Box2D : points de contact triés par position, choix départagés (voir Déterminisme).
    Événements statiques `Dust` (poussière) et `Shattered` pour les effets.
    Mesuré (PC) : vitre intacte ≈ 0,15 µs par pas (fixe), ≈ 0,2 µs (libre endormie) ; casse ≈ 0,5 à 1 ms (un pas) ; ensuite
    +30 à 50 µs par pas pendant 1 à 3 s, jusqu'à ce que les morceaux dorment. Restart et rejeu exacts au bit près.
    Anciens repères (blob standard, à remesurer avec le skateur) : couchée, chute de ≥ 2,5 m = traverse ; debout, de face :
    6 m/s fissure, 9 m/s traverse (`GlassPaneEditor` : poignées longueur / épaisseur, préréglages ;
    menu GameObject > 2D Object > Glass Pane, avec `GlassDust`)
- `CameraFollow` : caméra qui suit un GameObject (lissage, décalage, zone morte) ; rendu seulement, dans `LateUpdate`.
  Les cibles physiques exposent leur position et, si besoin, leur vitesse via `ICameraTarget` (`Skateboard` : centre de gravité de
  la planche, que les rotations ne déplacent pas ; torse du ragdoll après une chute).
  À la mort (`Skater.Died`) : focus + zoom sur le point de l'impact, ralenti optionnel via `monde_settings.SetTimeScaleMultiplier` ;
  après `slowDuration`, dézoom sur place jusqu'au redémarrage.
  Anticipation (`lookAhead`, cochée par défaut) : décalage dans le sens de la vitesse de la cible (`ICameraTarget.TryGetCameraVelocity`,
  sinon moyenne des corps de `ITrainingBody.GetBodyStates`) = (vitesse − `lookAheadDeadSpeed`) × `lookAheadTime`, plafonné (`lookAheadMaxDistance`, 3 m),
  pondéré par axe (`lookAheadAxisWeight`, moins à la verticale). Vitesse lissée : inertie courte quand elle accélère, plus longue
  quand elle ralentit / change de sens (`lookAheadReturnTime`) ; glissement plafonné (`lookAheadMaxShiftSpeed`). Au redémarrage
  et au retour de sauvegarde, décalage et dézoom posés directement à leur valeur (pas de balayage).
  Dézoom selon la vitesse (`speedZoom`, coché par défaut) : taille × 1 à `speedZoomMaxFactor` (1,3) entre `speedZoomStartSpeed`
  (10 m/s) et `speedZoomFullSpeed` (30 m/s), dézoom en `speedZoomOutTime`, retour plus lent en `speedZoomInTime`
- `SpeedMeter` : compteur de vitesse (km/h, case `useMph` pour les mph), coin haut droit. Vitesse = celle de
  `ICameraTarget.TryGetCameraVelocity` (planche du skateur), sinon norme de la moyenne des vitesses des corps
  (`ITrainingBody.GetBodyStates`) ; cible vide = cible de la `CameraFollow` principale.
  Affiché en IMGUI (`OnGUI`) : seule UI du projet pour l'instant, aucun asset ni Canvas
- `ContactParticles` (`Cosmetics/`) : étincelles aux contacts, sur le même objet qu'un `IContactFxSource` (`Skateboard`).
  Roulement : débit ∝ vitesse le long de la surface × force d'appui (frein sur le tail) ; choc : gerbe ∝ vitesse d'impact.
  Un `ParticleSystem` créé au lancement, alimenté par `Emit`, `maxParticles` = plafond du coût ; matériau par défaut =
  `Sprites/Default` + disque flou généré (aucun asset). Émission selon les pas écoulés (`LevelSimulation.Step`) : rien sans
  nouveau pas, rien sur un saut (> 8 pas, rejeu accéléré) ; purge sur `Restarted` / `Jumped`. Aléa visuel à graine propre
- `GlassDust` (`Cosmetics/`) : poussière et éclats fins d'une vitre, sur l'objet d'une `GlassPane`. Placement, quantité et vitesse
  décidés par la physique (événements `GlassPane.Dust`, `Shattered`), mis en file pendant le pas et émis dans `LateUpdate` ;
  rien pendant le rejeu accéléré. Freinage dans l'air (`drag`), traînées (`stretch`)
- `IContactFxSource.GetSurfaceContacts` : contacts pour les effets (point, normale, vitesse relative à la surface, impulsion
  normale, vitesse d'impact), en lecture seule
- `TrainingMode` : S = point de sauvegarde (fantôme), Tab = dernier point pratiqué ; réappui dans `previousChainDelay`
  (0,5 s) = point précédent ; Shift = plus récent ; R = `LevelRestart.Restart()` entre deux pas (recommence le niveau et efface
  tous les points). 100 points max (le plus ancien saute).
  Corps suivis : tout `ITrainingBody` actif (`Skateboard` : planche + personnage, `Ball2D`), dans l'ordre de `LevelRestart.Physics` (hiérarchie) : fantôme
  (`GhostShape`, `GhostShapeOf(i)` quand les corps diffèrent, ex. bonhomme bâton du skateur) et état (`BodyState`) de chaque corps.
  `discardNewerOnSave` coché : sauvegarder après un retour efface les points suivants (ordre du niveau) ;
  décoché : tout est gardé, dans l'ordre de création.
  Édition du niveau (éditeur uniquement) : points gardés entre deux Play et à la fermeture de Unity
  (`UserSettings/TrainingCheckpoints/<scène>.json`, entrées compressées) ; fantômes visibles dans la Scene view hors Play,
  clic sur un fantôme = point jaune (`TrainingModeGhostPicker`, re-clic = départ normal) ; Play repart du point jaune
  (`startAtCurrentOnPlay`). Si le niveau a été modifié avant le point, le rejeu n'y retombe plus (écart > `divergenceTolerance`) :
  le personnage est replacé comme le fantôme (`ITrainingBody.RestoreBodyStates`), le reste du niveau garde l'état du rejeu.
  Pas identique à un vrai passage (le niveau publié se fait d'une traite), mais déterministe : même niveau + même point = même résultat

Reliquat : `BLOB` (`Scripts/Legacy/Blob/`), balle souple (anneau de 32 capsules reliées par des joints) cassée par tout obstacle.
Gardé pour les scènes de `Scenes/Legacy/Blob/` ; détail dans le CLAUDE.md du tag `blob-et-debut-skate`.
Lest (2026-10-20) : touche tenue (`heavyKey`, flèche bas) = gravité des capsules × `heavyGravity` (`gravityScale` de Box2D,
réécrit seulement quand il change : aucune déformation, coût nul sans appui), montée `heavyRampTime`, retour
`heavyReleaseTime`, plafond optionnel de vitesse de chute `heavyMaxFallSpeed`. Coupé quand le blob est cassé.
Mesuré (`poc niveau 1`, × 3) : 191 → 783 km/h en 5 s sur la grande descente ; tenu dans une cuvette = il y reste (freine
en montée). Sans appui : simulation identique au bit près ; rejeu exact
Contacts : les capsules ont `contactEvents` (sans effet sur la simulation, vérifié au bit près) ; `IsTouching(i)` /
`TouchingCapsules` = compteurs entiers lus une fois par pas. Le compteur d'obstacles ne compte que les formes qui ont elles-mêmes
`contactEvents` (obstacles, vitres) : comportement inchangé. `Joint(j)`, `RestRingRadius`, `Generation`, `SetShape(stretch, bend)`
(allongement par capsule, masse inchangée + angle de repos par joint, en une écriture ; tout à 0 = blob d'origine) pour les
actions du blob.
`BlobJump` (2026-10-20, même objet que le `BLOB`, flèche haut) : saut, deux modes au choix (`mode`), plus on tient plus il monte.
Appui = contact à ≤ 1 cm (`GroundTolerance` : les contacts spéculatifs d'une pente voisine, jusqu'à 6 cm, faussaient la
normale de 2,6°) dont la normale est à moins de `maxGroundAngle` de la verticale (défaut 180 = toute surface : tête en bas au
plafond / dans un looping, il saute vers le bas ; mesuré au plafond : 80° = pas de saut, 180° = parti vers le bas dans les deux
modes) ; meilleur appui par capsule, puis moyenne (indépendant de l'ordre de Box2D) ; `coyoteTime` (sol relu un pas sur 4 tant qu'il touche : ≈ 3 µs la lecture), `bufferTime`,
pas de second saut pendant 0,1 s.
- Impulse (jeu vidéo) : vitesse `impulseSpeed` au départ (la vitesse qui allait dans le sol est d'abord annulée), puis poussée
  `holdAcceleration` × (1 − t / `holdTime`)^`holdCurve` tant qu'on tient ; `releaseKeep` (coupe la montée au relâchement) ;
  direction = normale du sol mélangée à la verticale (`upBlend`). Mesuré (`poc niveau 2`, plat, g −15) : 0,50 m (tape) → 2,62 m
  (tenu ≥ 0,25 s), progressif ; même hauteur au millimètre quel que soit le pas de l'appui, à l'arrêt comme en roulant à 6 m/s
- Stretch (physique ; 3e version du 2026-10-20, voulue par l'utilisateur : « simple et cohérent ») : tant que la touche est
  tenue, le blob s'allonge le long de l'axe ; contre le sol, le bout du bas ne peut pas reculer, il monte ; en l'air, ça ne
  donne rien ; relâché, il arrête net et ses joints le ramènent rond. Rien d'autre : ni vitesse visée ni poussée ajoutée.
  Abandonnés : charge écrasée puis détente (« désagréable »), vitesse visée + poussée continuée en l'air (« pas logique »).
  Axe = normale du sol (perpendiculaire au déplacement : au sol, il longe le sol), suivie tant qu'il touche, figée en l'air.
  « Muscle » (`DriveExtension`) : chaque pas, la part « étirement » du mouvement relatif (vitesse relative ∝ position le long de
  l'axe) est mise à `stretchSpeed` entre les deux bouts : somme nulle, rotation laissée. Mise en route en 0,06 s, arrêt à
  `maxStretch` (allongement réel ≈ 85 % de la vitesse : le sol et les joints en mangent un peu). Les joints prennent la forme de
  la fin du pas (`BLOB.SetShape`) : capsules des côtés allongées (sin² de leur angle à l'axe, largeur conservée) + angles de
  repos rond → gélule sur 0,4 m ; ils accompagnent sans tirer.
  Pièges mesurés : allonger les côtés d'un anneau rond le gonfle comme un ballon (il s'élargit au lieu de s'allonger) ; piloter
  la forme par les joints seuls plafonne (≈ 0,27 m) et suit avec retard ; asservir la longueur rattrape par à-coups et le blob
  décolle un instant de lui-même (hauteur qui plafonne au lieu de suivre l'appui) ; prendre la vitesse pour l'axe le fait
  basculer à l'horizontale dès qu'il monte. Sur place seulement (immobile à l'appui) : vitesse de côté et rotation d'ensemble
  gardées nulles pendant l'étirement (`HoldInPlace`, sinon 4 cm de dérive et 30 °/s).
  Mesuré (`poc niveau 2`, x = −57,2, g −15, 8 m/s / 2 m) : 0,02 m (0,025 s), 0,17 (0,05), 0,56 (0,083), 0,81 (0,125), 0,89 (0,167),
  0,99 (0,21), 1,12 (0,25), 1,40 m (0,33 s) ; à 4 m/s : 0,05 → 0,70 m ; retombe à ≤ 0,8 mm de son point de départ ; même
  hauteur quel que soit le pas de l'appui, à l'arrêt (0,0 cm) et en roulant à 6 m/s (0,1 cm) ; au plafond il pousse vers le bas.
  Coût : ≈ +55 µs par pas pendant l'étirement (Box2D : formes et joints qui changent ; formes de capsule réécrites par pas de
  1 cm seulement, changer une forme reconstruit ses contacts : à chaque pas, +125 µs), 0 sinon
- Forme : angle de repos de chaque joint ((2π / N) × amp × cos 2ψ, ψ = position du joint par rapport à l'axe) : somme nulle,
  l'anneau reste fermé ; amp > 0 = allongé le long de l'axe. Les joints du blob visent un angle relatif nul (anneau
  précontraint) : seules les différences d'angle comptent. Tout à 0 = blob d'origine exactement. Impulse : `impulseStretch`
  (visuel, commence un pas après le départ). Retour au rond (Impulse) : `relaxShape` décoché (défaut) = lâché une fois que plus
  rien ne touche (au plus tard après `relaxTime`) ; coché = retour piloté en `relaxTime`
  Restart, rejeu d'un bloc et pas à pas identiques au bit près dans les deux modes

Scripts de test (pas des éléments de jeu, mais doivent respecter les règles) :
- `Ball2D` : balle rigide simple, pour comparer des comportements (importante, à garder)
- `LevelShape` : bordures simples (rectangle, cercle…) pour les tests physiques ; ancien, à ne pas étendre
- `monde_settings` : réglages globaux du monde (vitesse, gravité, sous-pas, `simulationWorkers` = 1 : le plus rapide pour un niveau,
  identique au bit près quel que soit le nombre, collision continue, `drawOptions`) ;
  seul endroit qui configure le `PhysicsWorld` (sauf `simulationType` : `LevelSimulation`) et `Time.timeScale` (ralenti : `SetTimeScaleMultiplier`, ne change pas la simulation)
- `LowLevelRaycastGrabber` (`mouse.cs`) : attraper un corps à la souris, debug uniquement (non enregistré : casse le rejeu)

## Rendu
- Visuel secondaire : on se concentre sur la physique et le design. Rendu debug de la physique (`drawOptions`, gizmos) et dessin
  du personnage par `DrawGeometry`
- AUCUN mesh, sprite (exceptions : maillage invisible `GizmoPickArea`, éditeur uniquement, pour la sélection ;
  `ParticleSystem` des effets de `Cosmetics/`, choisi comme le moins coûteux sur toutes les machines)
- Aucun son pour l'instant

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
