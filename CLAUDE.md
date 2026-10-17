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
- Performance cruciale, mobile compris : le jeu doit rester fluide et le retour à une sauvegarde (Tab) quasi instantané
  (voir « Performance »)

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
  (exact au bit près, ≈ 0,05 ms par pas rejoué sur `poc niveau 1` : ≈ 180 ms pour 30 s de niveau ; voir « Performance »)

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
  (ex. `BoostZone` : une capsule du blob avant les 32), lire une fois puis réutiliser, ne pas écrire un Transform dont personne
  n'a besoin en jeu (obstacles sur trajectoire : le corps fait foi)
- **Se faire prévenir plutôt que demander à chaque pas** : événements de Box2D (début / fin de contact via `contactEvents`, comme
  le `BLOB` avec les obstacles) au lieu d'interroger chaque corps à chaque pas
- **Aucune allocation par pas** (pas de `new`, de LINQ, de `List` créée dans `LevelTick`) : listes réutilisées ou tableaux préparés au départ
- **Réglages du monde qui ne changent pas le résultat** : `simulationWorkers` = 1 (Box2D sur 12 threads passait plus de temps à
  coordonner les cœurs qu'à calculer : −33 %, et encore plus utile sur mobile). Résultat identique quel que soit ce nombre
- Le visuel (`LateUpdate`, `Cosmetics/`, dessin debug) ne tourne qu'une fois par image, jamais pendant le rejeu accéléré : il compte
  moins que la simulation, mais reste léger pour le mobile

Repères mesurés (`poc niveau 1`, PC, 1 thread) : ≈ 50 µs par pas, dont ≈ 40 µs pour Box2D (`Simulate` : recherche des paires de
contacts ≈ 15, résolution ≈ 25) et ≈ 10 µs pour tous les scripts. Avant optimisation : ≈ 130 µs. Le blob seul : 11 → 0,2 µs.

Vérifier une optimisation (méthode utilisée) :
1. Avec l'ancien code, en Play : redémarrer, rejouer N pas (`LevelSimulation.FastForward` avec des entrées fixes, flippers compris,
   et un scénario où le blob se casse sur un obstacle), enregistrer les bits des positions / vitesses de toutes les capsules et
   corps (`BitConverter.SingleToInt32Bits`) dans `Temp/`, et chronométrer (meilleur de plusieurs essais : la machine varie de ±10 %)
2. Recompiler, refaire exactement pareil : « identique au bit près » obligatoire, puis comparer les temps
3. Pendant ces tests, `LevelRestart.Restart()` efface les points du `TrainingMode` de l'utilisateur : mettre son `_restoring` à vrai
   (réflexion) pendant le test, et sauvegarder d'abord `UserSettings/TrainingCheckpoints/<scène>.json`

## Level design
- Règles de conception des niveaux : `LEVEL_DESIGN.md`, à lire AVANT de créer ou modifier un niveau
- `level1` : premier niveau prototype, sous l'objet `Level` (sections A–F : tuto gauche, tuto droit, combinaison,
  descente, relais gauche→droite, tube final + bassin d'arrivée). Départ du blob en (-1.2, 2.3)
- `level2 by claude` : niveau moyen (≈ 42 s jusqu'au bassin), réglages de la scène `poc niveau 1`, sous `Level` :
  A flipper au bout de la pente, B bumper au-dessus des scies, C mur + berceau-flipper + cheminée, D flipper puis
  flipper-tremplin à tenir, E bosse + longue descente, F flipper final + bassin d'arrivée ; R raccourci (bumper sur la montée
  de E → rail + accélérateur → bassin, ≈ 5 s gagnées). Départ du blob en (-0.6, 1.75)

## Organisation des scripts
- `Scripts/Level/` : tout ce qui se pose dans un niveau (BLOB, courbes, flippers, `Hazards/` obstacles,
  `Neutral/` éléments neutres).
  C'est la palette du futur éditeur de niveaux en jeu : code utilisable en build (pas de `UnityEditor`)
- `Scripts/Core/` : moteur (simulation pas à pas, entrées, redémarrage, réglages du monde)
- `Scripts/Game/` : un exemplaire par scène, hors niveau (caméra, mode entraînement avec la touche R)
- `Scripts/Test/` : outils de test, pas des éléments de jeu
- `Scripts/Cosmetics/` : effets purement visuels posés sur une balle ; ils LISENT son état (jamais d'écriture, jamais dans
  `LevelTick`), dans `LateUpdate`. Coupables par balle (case du composant), prévus aussi pour un blob fantôme / d'un ami
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
- `PinballBumper` : levier bumper, capsule de la longueur d'un flipper (deux boules au rayon de la grosse boule du flipper,
  première boule sur l'objet). À l'appui, translation perpendiculaire à la barre de `travel` (m, signe = côté), à `speed`
  (défaut = vitesse du bout d'un flipper, ≈ 14,66 m/s), retour au relâchement. Kinematic piloté en vitesse, même touche
  (`FlipperKey`) ; arrive pile sur la cible (`PinballBumperEditor` : poignée du déplacement)
- `BLOB` : la balle à amener à la fin du niveau. Détecte seul les obstacles (contacts de ses capsules, dans `LevelTick`,
  ordre fixe capsules → contacts). Tout obstacle touché casse TOUS ses joints ; ensuite chaque capsule libre qui touche
  un obstacle le lui signale (`OnCapsuleTouched`). Événement statique `Broken`. Seules les capsules qui touchent un obstacle
  relisent leurs contacts : les formes d'obstacle ont `contactEvents` (début / fin de contact touchant, sans effet sur la
  simulation) et le blob compte par capsule les contacts en cours (55× moins cher qu'interroger les 32 capsules à chaque pas)
- Obstacles (`BlobHazard`, abstrait) : corps statique par défaut, formes marquées par `userData.objectValue = this`,
  géométrie unique `BuildGeometry` (polygones convexes + cercles) pour aperçu et collision ; `userData.intValue` = 1 + index du polygone
  - `SpikedHazard` (abstrait) : un pique est le même objet partout, seul son placement change (`SpikeFrame`).
    Taille individuelle par pique (`sizes`), empalement des capsules qui touchent un pique (joint fixe souple vers un point
    enfoncé de `impaleDepth`). Poignée par pointe dans la Scene view (`SpikeHandles`)
  - `CircularSaw` : piques (dents) autour d'un disque, kinematic en rotation (`rotationSpeed` en degrés/s, modifiable en Play) ;
    les capsules empalées tournent avec la scie (`CircularSawEditor` : poignées diamètre / dents)
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
- `SpeedMeter` : compteur de vitesse (km/h, case `useMph` pour les mph), coin haut droit. Vitesse = norme de la moyenne
  des vitesses des corps (`ITrainingBody.GetBodyStates`, rotation du blob ignorée) ; cible vide = cible de la `CameraFollow`
  principale, sinon premier BLOB. Affiché en IMGUI (`OnGUI`) : seule UI du projet pour l'instant, aucun asset ni Canvas
- `ContactParticles` (`Cosmetics/`) : étincelles aux contacts de la balle, sur le même objet qu'un `IContactFxSource` (`BLOB`).
  Roulement : débit ∝ vitesse le long de la surface × force d'appui ; choc (nouveau contact) : gerbe ∝ vitesse d'impact.
  Un `ParticleSystem` par balle créé au lancement, alimenté par `Emit` (pas d'émission auto), `maxParticles` = plafond du coût ;
  matériau par défaut = `Sprites/Default` + disque flou généré (aucun asset, présent dans toute build). Émission selon les pas
  écoulés (`LevelSimulation.Step`) : rien sans nouveau pas, rien sur un saut (> 8 pas, rejeu accéléré) ; purge sur
  `Restarted` / `Jumped`. Aléa visuel à graine propre. Les particules ne collisionnent pas (traversent les murs)
- `IContactFxSource.GetSurfaceContacts` : contacts de la balle pour les effets (point, normale vers la balle, vitesse du
  centre relative à la surface, impulsion normale, vitesse d'impact). `BLOB` l'implémente en lecture seule (capsules libres)
- `TrainingMode` : S = point de sauvegarde (fantôme de chaque balle), Tab = dernier point pratiqué ; réappui dans `previousChainDelay`
  (0,5 s) = point précédent ; Shift = plus récent ; R = `LevelRestart.Restart()` entre deux pas (recommence le niveau et efface tous les points ; plus de script `RespawnOnKey`). 100 points max (le plus ancien saute).
  Balles suivies : tout `ITrainingBody` actif (`BLOB`, `Ball2D`), dans l'ordre de `LevelRestart.Physics` (hiérarchie) : fantôme
  (`GhostShape`, cercle = capsule de longueur nulle) et état (`BodyState`) de chaque corps. : une Ball2D sert aussi aux tests
  `discardNewerOnSave` coché : sauvegarder après un retour efface les points suivants (ordre du niveau) ;
  décoché : tout est gardé, dans l'ordre de création.
  Édition du niveau (éditeur uniquement) : points gardés entre deux Play et à la fermeture de Unity
  (`UserSettings/TrainingCheckpoints/<scène>.json`, entrées compressées) ; fantômes visibles dans la Scene view hors Play,
  clic sur un fantôme = point jaune (`TrainingModeGhostPicker`, re-clic = départ normal) ; Play repart du point jaune
  (`startAtCurrentOnPlay`). Si le niveau a été modifié avant le point, le rejeu n'y retombe plus (écart > `divergenceTolerance`) :
  la balle est replacée comme le fantôme (`ITrainingBody.RestoreBodyStates`), le reste du niveau garde l'état du rejeu. Pas identique à un
  vrai passage (le niveau publié se fait d'une traite), mais déterministe : même niveau + même point = même résultat

Scripts de test (pas des éléments de jeu, mais doivent respecter les règles) :
- `Ball2D` : balle rigide simple, pour comparer son comportement à celui du blob
- `LevelShape` : bordures simples (rectangle, cercle…) pour les tests physiques ; ancien, à ne pas étendre
- `monde_settings` : réglages globaux du monde (vitesse, gravité, sous-pas, `simulationWorkers` = 1 : le plus rapide pour un niveau,
  identique au bit près quel que soit le nombre, collision continue, `drawOptions`) ;
  seul endroit qui configure le `PhysicsWorld` (sauf `simulationType` : `LevelSimulation`) et `Time.timeScale` (ralenti : `SetTimeScaleMultiplier`, ne change pas la simulation)
- `LowLevelRaycastGrabber` (`mouse.cs`) : attraper un corps à la souris, debug uniquement (non enregistré : casse le rejeu)

## Rendu
- Rendu debug de la physique uniquement (`drawOptions`, gizmos)
- AUCUN mesh, sprite (exceptions : maillage invisible `GizmoPickArea`, éditeur uniquement, pour la sélection ;
  `ParticleSystem` des effets de `Cosmetics/`, choisi comme le moins coûteux sur toutes les machines)
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
