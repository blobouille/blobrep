# Projet

Jeu 2D de type « Geometry Dash » à mécaniques de flipper. Le but de chaque niveau : amener
le BLOB (balle souple physique, anneau de capsules reliées par des joints) jusqu'à la fin
du niveau. Le joueur NE contrôle PAS le blob directement : il l'influence via des éléments
du niveau (leviers de flipper aujourd'hui, d'autres éléments plus tard). Niveaux construits
avec des courbes de Bézier épaisses (murs/rails) et ces éléments. Plusieurs niveaux prévus.

## Piliers (ne changent pas)
- Gameplay d'abord : esthétique et sons viendront plus tard
- Résultat TOUJOURS déterministe : mêmes entrées = même simulation à chaque lancement
  (indispensable pour un jeu de niveaux à rejouer / mémoriser)
- Le joueur agit sur le niveau, jamais directement sur le blob

## Roadmap (étape actuelle en gras)
1. **Éléments de level design** : outils/composants pour construire des niveaux (courbes, flippers, …)
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

## Échelle (standard du projet)
- 1 unité Unity = 1 m pour Box2D (`lengthUnitsPerMeter = 1`). Ses tolérances sont en absolu
  (`linearSlop` 0,005, `bounceThreshold` 1 m/s, `contactSpeed` 3 m/s…) : la taille n'est PAS relative
- Référence : balle (Ball2D / BLOB) de **diamètre 1**. Objets mobiles entre 0,1 et 10
- Tailles de balles toujours en diamètre : `BLOB.diameter` = diamètre extérieur, `Ball2D.diameter` = diamètre ; `BLOB.density` = densité d'un disque plein équivalent
  (même diamètre + même densité qu'une Ball2D = même masse)
- Échelle des Transform des éléments de niveau toujours à (1, 1, 1) : la taille se règle par les paramètres
  (`size`, `thickness`, points…), sinon épaisseurs et cercles se déforment
- Régler la sensation de vitesse par la gravité / vitesse du jeu, pas par la taille
- Épaisseur des courbes : visuelle seulement (la collision est une chaîne fine)
- Mobile : balle ≈ 1/12 à 1/10 de la hauteur d'écran (caméra ortho ≈ 5–6) ; à valider
- Flipper kinematic : pas de collision continue contre la balle → si le bout avance de plus
  d'un rayon de balle par pas, activer `fastCollisions` sur la balle / le blob

## Déterminisme
- Pas de `Random` sans graine fixe explicite
- Logique de simulation et pilotage physique dans `ILevelTick.LevelTick()`, jamais dans `Update` ni `FixedUpdate`
  (un pas doit pouvoir être rejoué en accéléré) ; utiliser `Time.fixedDeltaTime`
- `LevelSimulation` est le seul moteur : à chaque pas fixe, entrées → `LevelTick` de chaque élément (ordre de la hiérarchie)
  → `PhysicsWorld.Simulate` (monde en `SimulationType.Script`, réglé par lui seul). Actions entre deux pas
  (redémarrage, retour à une sauvegarde) : `LevelSimulation.RunBeforeNextStep`
  Pourquoi : avec chaque script dans son propre FixedUpdate, l'ordre dépendait de Unity et un pas ne pouvait pas être
  rejoué hors du temps réel. Un seul moteur = ordre fixe + rejeu accéléré exact (sauvegardes, replays, fantômes)
- Entrées du joueur : uniquement via `LevelInput` (un bit par `FlipperKey`, enregistré à chaque pas), jamais le clavier
  directement dans la simulation. Toute action non enregistrée (ex. souris de debug) casse le rejeu
- Ordre de création des corps/joints stable et explicite (ne pas dépendre de l'ordre
  d'exécution des scripts) ; ordre d'itération stable (pas de dépendance aux Dictionary/HashSet)
- Signaler tout risque de non-déterminisme plutôt que de l'ignorer
- Recommencer = `LevelRestart.Restart()` (R, plus tard la mort) : détruit toute la physique, `PhysicsWorld.Reset()`
  sur le monde vide (sinon l'historique interne de Box2D fait diverger les essais), puis recrée dans l'ordre de la hiérarchie.
  Le départ du niveau passe aussi par là (au chargement de la scène) : chaque essai est identique au bit près (~1 ms)
- Tout état à remettre à zéro hors physique (caméra, timer, musique…) s'abonne à `LevelRestart.Restarted`
  (et à `LevelSimulation.Jumped` pour un retour à une sauvegarde ; ignorer les événements pendant `LevelSimulation.FastForwarding`)
- Point de sauvegarde = entrées de chaque pas depuis le départ ; y revenir = redémarrage + rejeu accéléré
  (exact au bit près, ≈ 0,06 ms par pas rejoué : 40 ms pour 6 s de niveau)

## Level design
- Règles de conception des niveaux : `LEVEL_DESIGN.md`, à lire AVANT de créer ou modifier un niveau
- `level1` : premier niveau prototype, sous l'objet `Level` (sections A–F : tuto gauche, tuto droit, combinaison,
  descente, relais gauche→droite, tube final + bassin d'arrivée). Départ du blob en (-1.2, 2.3)

## Organisation des scripts
- `Scripts/Level/` : tout ce qui se pose dans un niveau (BLOB, courbes, flippers, `Hazards/` obstacles,
  `Neutral/` éléments neutres).
  C'est la palette du futur éditeur de niveaux en jeu : code utilisable en build (pas de `UnityEditor`)
- `Scripts/Core/` : moteur (simulation pas à pas, entrées, redémarrage, réglages du monde)
- `Scripts/Game/` : un exemplaire par scène, hors niveau (caméra, mode entraînement, touche R)
- `Scripts/Test/` : outils de test, pas des éléments de jeu
- `Editor/Level/` : inspecteurs et poignées Scene view des éléments de `Level/`. Le dossier `Editor` n'est PAS inclus
  dans le jeu : le futur éditeur en jeu devra réimplémenter ses poignées, en réutilisant la géométrie des composants
- `Editor/Game/` : outils éditeur des scripts de `Game/` (sélection des fantômes du mode entraînement)
- Déplacer / renommer un script uniquement via Unity (`AssetDatabase.MoveAsset`) : le `.meta` suit, les scènes gardent leurs références

## Construction des niveaux : modèle CurvePath
Tout ce qui sert à créer un niveau suit le modèle existant (CurvePath2D / PinballFlipper) :
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur
- Valeurs par défaut des scripts = réglages officiels, ceux de la scène modèle `poc niveau` (voir `LEVEL_DESIGN.md`).
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
- `PinballFlipper` : flipper (2 cercles + quad), charnière à moteur (`useTorque`) ou kinematic piloté en vitesse
- `BLOB` : la balle à amener à la fin du niveau. Détecte seul les obstacles (contacts de ses capsules, dans `LevelTick`,
  ordre fixe capsules → contacts). Tout obstacle touché casse TOUS ses joints ; ensuite chaque capsule libre qui touche
  un obstacle le lui signale (`OnCapsuleTouched`). Événement statique `Broken`
- Obstacles (`BlobHazard`, abstrait) : corps statique par défaut, formes marquées par `userData.objectValue = this`,
  géométrie unique `BuildGeometry` (polygones convexes + cercles) pour aperçu et collision ; `userData.intValue` = 1 + index du polygone
  - `SpikedHazard` (abstrait) : un pique est le même objet partout, seul son placement change (`SpikeFrame`).
    Taille individuelle par pique (`sizes`), empalement des capsules qui touchent un pique (joint fixe souple vers un point
    enfoncé de `impaleDepth`). Poignée par pointe dans la Scene view (`SpikeHandles`)
  - `CircularSaw` : piques (dents) autour d'un disque, kinematic en rotation (`rotationSpeed` en degrés/s, modifiable en Play) ;
    les capsules empalées tournent avec la scie (`CircularSawEditor` : poignées diamètre / dents)
  - `SpikeRow` : piques en rangée (nombre, taille globale, inclinaison) (`SpikeRowEditor` : taille globale, nombre)
  - Nouvel obstacle = sous-classe de `BlobHazard` ; menu GameObject > 2D Object pour créer
- Éléments neutres (`Neutral/`) : ni bons ni mauvais, ils changent le mouvement du blob sans le casser.
  Ils lisent les blobs via `BLOB.Active` (ordre de création) et agissent sur la vitesse des capsules dans `LevelTick`
  - `BoostZone` : rectangle d'accélération (flèche = +X local), sans corps physique, à la Trackmania : chaque capsule dont
    le centre est dans la zone reçoit `acceleration` (m/s²) dans l'axe, jusqu'au plafond `maxSpeed` ; jamais de freinage,
    le reste du mouvement est conservé. Même entrée = même sortie au bit près ; entrée différente = sortie différente.
    Abandonné : la zone « remise à zéro » (sortie identique quelle que soit l'entrée) ; même avec le blob recréé à la sortie,
    l'historique interne de Box2D (numéros des contacts) faisait diverger la suite au premier contact.
    Variantes prévues : même composant, autres valeurs / couleur. Idée : zone verticale = tube qui transporte le blob
    (`BoostZoneEditor` : poignées longueur / hauteur)
- `CameraFollow` : caméra qui suit un GameObject (lissage, décalage, zone morte) ; rendu seulement, dans `LateUpdate`.
  Les Transform de BLOB/Ball2D ne bougent pas : elles exposent leur position physique via `ICameraTarget`.
  À la casse du blob suivi : focus + zoom sur l'obstacle, ralenti optionnel via `monde_settings.SetTimeScaleMultiplier` ;
  après `slowDuration`, dézoom sur place (toujours centré sur l'impact) jusqu'au redémarrage
- `TrainingMode` : S = point de sauvegarde (fantôme du blob), Tab = dernier point pratiqué ; réappui dans `previousChainDelay`
  (0,5 s) = point précédent ; Shift = plus récent ; R efface tout. 100 points max (le plus ancien saute).
  `discardNewerOnSave` coché : sauvegarder après un retour efface les points suivants (ordre du niveau) ;
  décoché : tout est gardé, dans l'ordre de création.
  Édition du niveau (éditeur uniquement) : points gardés entre deux Play et à la fermeture de Unity
  (`UserSettings/TrainingCheckpoints/<scène>.json`, entrées compressées) ; fantômes visibles dans la Scene view hors Play,
  clic sur un fantôme = point jaune (`TrainingModeGhostPicker`, re-clic = départ normal) ; Play repart du point jaune
  (`startAtCurrentOnPlay`). Si le niveau a été modifié avant le point, le rejeu n'y retombe plus (écart > `divergenceTolerance`) :
  le blob est replacé comme le fantôme (`BLOB.RestoreCapsules`), le reste du niveau garde l'état du rejeu. Pas identique à un
  vrai passage (le niveau publié se fait d'une traite), mais déterministe : même niveau + même point = même résultat

Scripts de test (pas des éléments de jeu, mais doivent respecter les règles) :
- `Ball2D` : balle rigide simple, pour comparer son comportement à celui du blob
- `LevelShape` : bordures simples (rectangle, cercle…) pour les tests physiques ; ancien, à ne pas étendre
- `monde_settings` : réglages globaux du monde (vitesse, gravité, sous-pas, collision continue, `drawOptions`) ;
  seul endroit qui configure le `PhysicsWorld` (sauf `simulationType` : `LevelSimulation`) et `Time.timeScale` (ralenti : `SetTimeScaleMultiplier`, ne change pas la simulation)
- `LowLevelRaycastGrabber` (`mouse.cs`) : attraper un corps à la souris, debug uniquement (non enregistré : casse le rejeu)
- `RespawnOnKey` : touche R = `LevelRestart.Restart()` entre deux pas (efface aussi les points de sauvegarde)

## Rendu
- Rendu debug de la physique uniquement (`drawOptions`, gizmos)
- AUCUN mesh, sprite (seule exception : maillage invisible `GizmoPickArea`, éditeur uniquement, pour la sélection)
- Aucun son pour l'instant

## Conventions de code
- Pas de commentaires, sauf point vraiment non évident
- Paramètres exposés : toujours `[Header]` + `[Tooltip]` (en français, précis sur l'effet et les unités),
  plus `[Min]` / `[Range]` quand pertinent
- Noms de variables et classes en anglais ; champs privés préfixés `_` (exception : les champs publics français de `Ball2D`/`monde_settings`, gardés pour ne pas perdre les valeurs)
- Un script = une responsabilité ; ne pas renommer les champs publics existants (valeurs d'inspecteur perdues)
- Maths avec `Unity.Mathematics` pour la physique ; dimensions en unités Unity : toujours préciser
  si un paramètre est relatif à une échelle ou absolu

## Façon de travailler
- Modifications ciblées : ne toucher qu'aux fichiers demandés
- Proposer un plan avant les changements importants
- Réponses courtes : dire ce qui a été modifié, sans longs développements
- Mettre à jour ce fichier quand une décision de projet est prise (vision, roadmap, conventions)

## Éditeur Unity (CLI)
- L'Éditeur ouvert est pilotable via le CLI `unity` (`unity status`, `unity list`, `unity command eval '...'`) :
  modifier la scène par ce biais plutôt qu'en éditant les fichiers
- Ne pas sauvegarder la scène (`save_scene`) sans accord
- Chemins de sortie (captures…) : obligatoirement dans le projet, sinon refus ; éviter `Assets/`
- Les gizmos n'apparaissent pas dans `capture_scene_view` : inspecter la géométrie par `eval`
- Git : le dossier appartient à un autre compte Windows → `git -c safe.directory="C:/Users/dev/Documents/blob project" ...`

## À ne pas toucher
- Fichiers `.unity`, `.prefab`, `.meta` (modifiés dans l'éditeur Unity)
- Dossiers `Library/`, `Temp/`, `Logs/`, `obj/`
- Ne jamais supprimer de fichiers sans me demander d'abord
