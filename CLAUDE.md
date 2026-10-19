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
   - En cours : rendre le skate plus satisfaisant (voir « Skateur : pistes ») ; adapter au skateur les éléments écrits pour le
     blob (voir « À adapter au skateur »)
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

## Échelle (standard du projet)
- 1 unité Unity = 1 m pour Box2D (`lengthUnitsPerMeter = 1`). Ses tolérances sont en absolu
  (`linearSlop` 0,005, `bounceThreshold` 1 m/s, `contactSpeed` 3 m/s…) : la taille n'est PAS relative
- Référence : le skateur, `Skater.height` = 1,35 m (chevilles → sommet de la tête), planche de `Skateboard` à l'échelle.
  Objets mobiles entre 0,1 et 10. Balle de test (`Ball2D`) : diamètre 1 (tailles de balles toujours en diamètre)
- Échelle des Transform des éléments de niveau toujours à (1, 1, 1) : la taille se règle par les paramètres
  (`size`, `thickness`, points…), sinon épaisseurs et cercles se déforment
- Régler la sensation de vitesse par la gravité / vitesse du jeu, pas par la taille
- Épaisseur des courbes : visuelle seulement (la collision est une chaîne fine)
- Mobile : caméra ortho ≈ 9 sur `skate reboot` (≈ 5–6 à l'époque du blob) ; taille du skateur à l'écran à valider
- Flipper kinematic : pas de collision continue contre ce qu'il frappe → si le bout avance de plus que l'épaisseur de ce
  qu'il frappe par pas, activer `fastCollisions` (`Skater.fastCollisions`, `Ball2D`)

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

Repères mesurés (PC, 1 thread) : `skate reboot` ≈ 13 µs par pas complet, dont ≈ 4,5 µs pour le `Skater`.
Anciens repères (époque blob, `poc niveau 1`, plus gros niveau) : ≈ 50 µs par pas, dont ≈ 40 µs pour Box2D. À remesurer sur
un vrai niveau de skate.

Vérifier une optimisation (méthode utilisée) :
1. Avec l'ancien code, en Play : redémarrer, rejouer N pas (`LevelSimulation.FastForward` avec des entrées fixes, flèches et
   Espace compris, et un scénario de mort), enregistrer les bits des positions / vitesses de tous les corps
   (`BitConverter.SingleToInt32Bits`), et chronométrer (meilleur de plusieurs essais : la machine varie de ±10 %)
2. Recompiler, refaire exactement pareil : « identique au bit près » obligatoire, puis comparer les temps
3. Pendant ces tests, `LevelRestart.Restart()` efface les points du `TrainingMode` de l'utilisateur : mettre son `_restoring` à vrai
   (réflexion) pendant le test, et sauvegarder d'abord `UserSettings/TrainingCheckpoints/<scène>.json`

## Level design
- Règles de conception des niveaux : `LEVEL_DESIGN.md`, à lire AVANT de créer ou modifier un niveau
- `skate reboot` (`Assets/Scenes/`) : piste de test du skateur, réglages du monde de l'ancienne `poc niveau 1` (gravité −20,
  caméra ortho 9), sous `Track` (racine en (0, 40)) : descente, plat (frein), kicker + fosse à piques, flipper (Espace) au bout
  d'une pente, grande réception, vitre posée au sol (la planche la brise), quarter pipe. Départ sur le haut de la descente
  (≈ (2.1, 47.6)). Scène de référence en attendant un premier vrai niveau de skate
- Reliquats (`Assets/Scenes/Legacy/Blob/`) : `poc niveau` (ancienne scène modèle des réglages), `poc niveau 1` (objets de
  test : gadgets, trajectoires, vitres), `level1 by claude`, `level2 by claude`, `reboot`. Niveaux pensés pour le blob :
  utiles comme banc de test des éléments, pas comme référence de design

## Organisation des scripts
- `Scripts/Skater/` : le personnage principal (`Skater`, `Skateboard`)
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
`Skateboard` = géométrie seule de la planche (longueur, épaisseur, relevés nose/tail on/off + angle, roues, empattement,
trucks ; repère : origine au sol entre les roues, +X = nose ; `KickIndex` = polygone de chaque relevé).
DÉCISION (2026-10-19, après essai au clavier d'un personnage physique à articulations : « mou, pas satisfaisant ») :
ON CONTRÔLE LA PLANCHE, le personnage suit visuellement.

Comportement actuel (`Skater`) :
- Vivant, la planche est le SEUL corps physique : roues = cercles sans frottement (roues libres), elle porte la masse totale
  (`boardMass` + `mass`), centre de gravité bas (stable, colle aux courbes). Le centre de gravité suit horizontalement celui
  du personnage projeté le long de la normale du sol (`weightShift`) : hanches en arrière = poids sur le tail.
- Le personnage (bonhomme bâton, tailles en fractions de `height`) est une POSE recalculée à chaque pas dans le repère de la
  planche (cinématique inverse des jambes, hanches, buste, bras ; recalcul sauté si ses entrées n'ont pas changé), dessinée dans
  `LateUpdate` (`DrawGeometry`) et servant de HITBOX par requêtes de recouvrement : tête / torse contre le décor (catégorie
  `Skater.CategoryBit` exclue, verre jamais mortel), tous les membres contre les obstacles (test de boîte puis par membre).
  Il ne pousse rien. Pendant une bascule voulue il garde le buste droit (`counterTilt`, plafond `maxCounterTilt`) ; sans
  flèche, il suit la planche (son poids revient alors de lui-même vers le milieu).
- Flèches (`LevelInput`) : au sol, la planche pivote sur la roue qui reste au sol (rotation + montée / descente selon la
  normale du sol, jamais de vitesse le long du sol) vers le tail (`StopAngle` + `tailPress` : une fois le tail posé, rotation
  pure qui le presse, le frottement du plateau freine) ou vers le nose ; nose au sol + flèche avant tenue `noseFallTime`
  (0,3 s) = chute. En l'air (après `airDelaySteps` sans contact), rotation autour du centre de gravité commun personnage +
  planche (point balistique mémorisé au décollage, la planche tourne autour), `airSpinSpeed` (540 °/s), freinée au relâchement.
- Mort : hitbox, planche contre un obstacle (événements de contact), nose tenu au sol, ou planche hors sol immobile
  `stuckTime`. Le personnage devient alors un ragdoll (10 corps + charnières libres à frottement `deadJointFriction`) créé à
  la pose actuelle avec la vitesse de la planche en chaque point ; la planche garde sa vitesse et ne porte plus que sa masse.
  Empalement par les piques (`OnCapsuleTouched` des obstacles). `Skater.Died` (focus caméra).
- `ICameraTarget` (torse de la pose), `ITrainingBody` (11 états : planche + membres de la pose, ou du ragdoll ; fantôme en
  bonhomme bâton via `GhostShapeOf`), `IContactFxSource` (étincelles de `ContactParticles`).

Ce qui marche (mesuré, PC, éditeur) : ≈ 4,5 µs par pas ; restart, rejeu (d'un bloc, pas à pas, par paquets) et retour au point du
mode entraînement exacts au bit près. Parcours de `skate reboot` sans touche : vivant jusqu'au quarter ; kicker : tape de 50 à
85 ms pour replaquer, salto arrière = appui de 0,4 à 0,6 s ; frein tenu : 17 → 4 m/s par le seul frottement ; bumper : sous la
planche il l'envoie à ≈ 5,5 m, trop tôt il fait mur ; la planche brise une vitre (23 → 14 m/s).

Abandonné : version « tout physique » à ressorts (trop mou au clavier) ; équilibre actif par rotation imposée et frein assisté
(uncanny : debout au-dessus du vide, frein en tapotant).

Skateur : pistes pour plus de satisfaction (À VALIDER avec l'utilisateur, rien n'est décidé) :
- Réceptions : aujourd'hui seule la hitbox juge une réception. Récompenser une réception propre (planche parallèle au sol :
  vitesse gardée) et pénaliser une réception de travers (perte de vitesse, voire chute) rendrait l'air plus lisible
- Figures = boost (roadmap) : compter les rotations / figures en l'air et rendre de la vitesse à la réception propre
- Pump : gagner de la vitesse en fléchissant dans les creux et en se délestant sur les bosses, comme en vrai skate (aucun moteur,
  cohérent avec le pilier momentum) ; à étudier : poids / accroupissement de la pose qui change la hauteur du centre de gravité
- Ollie (saut sur commande) : absent. Change beaucoup le design (le joueur choisit où décoller) : décision de jeu à prendre
- Courbes concaves serrées : la planche se cabre (accélération centripète v²/R) ; courbes plus larges dans les niveaux ou aide
- Lisibilité de la hitbox et des morts (pourquoi je suis mort ?) : ralenti / focus déjà présents, à juger en jeu
- Sensation de vitesse : caméra (anticipation, dézoom selon la vitesse) à régler pour le skateur
- Limite : le corps traverse une vitre (seule la planche la brise)

## À adapter au skateur (écrits pour le blob, lisent `BLOB.Active`)
Piste proposée : une interface commune (ex. `IRider` : liste statique des actifs dans l'ordre de création, masse, vitesse,
position, corps à accélérer), implémentée par `Skater` (planche) et gardée par `BLOB` pour les anciennes scènes.
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
  échantillonnage adaptatif (`resolution`, `straightTolerance`, `miterLimit`),
  `BuildCollisionChains()` pour la collision
- `CurvePath2DEditor` : édition dans la Scene view (points, tangentes, modes Free/Aligned/Vector/Auto, Undo)
- `CurveCollider2D` : crée la collision statique du chemin, uniquement en chaînes Box2D (`PhysicsChain`).
  Les quads (polygones jointifs) ont été abandonnés : collisions fantômes aux jointures → rebonds parasites.
  Le léger « accroc » des chaînes à très haute vitesse est accepté (atténué par le pas à 120 Hz).
  Pour un mur épais solide des deux côtés : `collisionMode = Outline`
- `PinballFlipper` : flipper (2 cercles + quad), charnière à moteur (`useTorque`) ou kinematic piloté en vitesse ; touche Espace
  (`FlipperKey`). Pour le skateur : levier au bout d'une pente qui le projette, ou tremplin tenu
- `PinballBumper` : levier bumper, capsule de la longueur d'un flipper (deux boules au rayon de la grosse boule du flipper,
  première boule sur l'objet). À l'appui, translation perpendiculaire à la barre de `travel` (m, signe = côté), à `speed`
  (défaut = vitesse du bout d'un flipper, ≈ 14,66 m/s), retour au relâchement. Kinematic piloté en vitesse, même touche
  (`FlipperKey`) ; arrive pile sur la cible (`PinballBumperEditor` : poignée du déplacement)
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
    `wrap` (pivots par lancer de rayon, coin affiné par dichotomie, déroulement au changement de côté), `returnMode`
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
  Les cibles physiques exposent leur position via `ICameraTarget` (`Skater` : torse de la pose).
  À la mort (`Skater.Died`) : focus + zoom sur le point de l'impact, ralenti optionnel via `monde_settings.SetTimeScaleMultiplier` ;
  après `slowDuration`, dézoom sur place jusqu'au redémarrage.
  Anticipation (`lookAhead`, cochée par défaut) : décalage dans le sens de la vitesse moyenne de la cible
  (`ITrainingBody.GetBodyStates`) = (vitesse − `lookAheadDeadSpeed`) × `lookAheadTime`, plafonné (`lookAheadMaxDistance`, 3 m),
  pondéré par axe (`lookAheadAxisWeight`, moins à la verticale). Vitesse lissée : inertie courte quand elle accélère, plus longue
  quand elle ralentit / change de sens (`lookAheadReturnTime`) ; glissement plafonné (`lookAheadMaxShiftSpeed`). Au redémarrage
  et au retour de sauvegarde, décalage et dézoom posés directement à leur valeur (pas de balayage).
  Dézoom selon la vitesse (`speedZoom`, coché par défaut) : taille × 1 à `speedZoomMaxFactor` (1,3) entre `speedZoomStartSpeed`
  (10 m/s) et `speedZoomFullSpeed` (30 m/s), dézoom en `speedZoomOutTime`, retour plus lent en `speedZoomInTime`
- `SpeedMeter` : compteur de vitesse (km/h, case `useMph` pour les mph), coin haut droit. Vitesse = norme de la moyenne
  des vitesses des corps (`ITrainingBody.GetBodyStates`) ; cible vide = cible de la `CameraFollow` principale.
  Affiché en IMGUI (`OnGUI`) : seule UI du projet pour l'instant, aucun asset ni Canvas
- `ContactParticles` (`Cosmetics/`) : étincelles aux contacts, sur le même objet qu'un `IContactFxSource` (`Skater`).
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
  Corps suivis : tout `ITrainingBody` actif (`Skater`, `Ball2D`), dans l'ordre de `LevelRestart.Physics` (hiérarchie) : fantôme
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
