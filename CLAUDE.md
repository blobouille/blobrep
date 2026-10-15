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
2. Boucle de jeu : départ, arrivée (fin de niveau), échec / recommencer
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
- Logique de simulation et pilotage physique dans `FixedUpdate`, jamais dans `Update`
  (dépendrait du framerate) ; utiliser `Time.fixedDeltaTime`
- Ordre de création des corps/joints stable et explicite (ne pas dépendre de l'ordre
  d'exécution des scripts) ; ordre d'itération stable (pas de dépendance aux Dictionary/HashSet)
- Signaler tout risque de non-déterminisme plutôt que de l'ignorer
- Recommencer = `LevelRestart.Restart()` (R, plus tard la mort) : détruit toute la physique, `PhysicsWorld.Reset()`
  sur le monde vide (sinon l'historique interne de Box2D fait diverger les essais), puis recrée dans l'ordre de la hiérarchie.
  Le départ du niveau passe aussi par là (au chargement de la scène) : chaque essai est identique au bit près (~1 ms)
- Tout état à remettre à zéro hors physique (caméra, timer, musique…) s'abonne à `LevelRestart.Restarted`

## Level design
- `level1` : premier niveau prototype, sous l'objet `Level` (sections A–F : tuto gauche, tuto droit, combinaison,
  descente, relais gauche→droite, tube final + bassin d'arrivée). Départ du blob en (-1.2, 2.3)

## Construction des niveaux : modèle CurvePath
Tout ce qui sert à créer un niveau suit le modèle existant (CurvePath2D / PinballFlipper) :
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur
- Aperçu visible dans la Scene view AVANT de lancer le jeu : `OnDrawGizmos` (+ Editor custom
  avec handles si besoin), avec la même géométrie que celle envoyée à la physique
- La physique n'est créée qu'en Play (`Application.isPlaying`), dans `OnEnable`, et détruite dans `OnDisable`.
  Le composant implémente `ILevelPhysics` : `LevelRestart` le désactive / réactive pour recommencer le niveau
  (tout corps créé hors d'un `ILevelPhysics` empêche la remise à zéro du monde)
- Une seule source de vérité géométrique pour aperçu et collision

Scripts existants :
- `CurvePath2D` : courbe de Bézier cubique par morceaux, épaisseur uniforme (center/left/right),
  échantillonnage adaptatif (`resolution`, `straightTolerance`, `miterLimit`),
  `BuildCollisionChains()` pour la collision
- `CurvePath2DEditor` : édition dans la Scene view (points, tangentes, modes Free/Aligned/Vector/Auto, Undo)
- `CurveCollider2D` : crée la collision statique du chemin, uniquement en chaînes Box2D (`PhysicsChain`).
  Les quads (polygones jointifs) ont été abandonnés : collisions fantômes aux jointures → rebonds parasites.
  Le léger « accroc » des chaînes à très haute vitesse est accepté (atténué par le pas à 120 Hz).
  Pour un mur épais solide des deux côtés : `collisionMode = Outline`
- `PinballFlipper` : flipper (2 cercles + quad), charnière à moteur (`useTorque`) ou kinematic piloté en vitesse
- `BLOB` : la balle à amener à la fin du niveau. Détecte seul les obstacles (contacts de ses capsules, dans `FixedUpdate`,
  ordre fixe capsules → contacts). Tout obstacle touché casse TOUS ses joints ; ensuite chaque capsule libre qui touche
  un obstacle le lui signale (`OnCapsuleTouched`). Événements statiques `Broken` / `Respawned`
- Obstacles (`BlobHazard`, abstrait) : corps statique par défaut, formes marquées par `userData.objectValue = this`,
  géométrie unique `BuildGeometry` (polygones convexes + cercles) pour aperçu et collision
  - `CircularSaw` : scie dentée kinematic en rotation (`rotationSpeed` en degrés/s, modifiable en Play)
    (`CircularSawEditor` : poignées diamètre / dents)
  - `SpikeRow` : rangée de piques (nombre, taille globale, taille par pique) ; les capsules qui la touchent s'empalent
    (joint fixe souple vers un point enfoncé de `impaleDepth`) (`SpikeRowEditor` : poignée par pointe, taille globale, nombre)
  - Nouvel obstacle = sous-classe de `BlobHazard` ; menu GameObject > 2D Object pour créer
- `CameraFollow` : caméra qui suit un GameObject (lissage, décalage, zone morte) ; rendu seulement, dans `LateUpdate`.
  Les Transform de BLOB/Ball2D ne bougent pas : elles exposent leur position physique via `ICameraTarget`.
  À la casse du blob suivi : focus + zoom sur l'obstacle, ralenti optionnel via `monde_settings.SetTimeScaleMultiplier`

Scripts de test (pas des éléments de jeu, mais doivent respecter les règles) :
- `Ball2D` : balle rigide simple, pour comparer son comportement à celui du blob
- `LevelShape` : bordures simples (rectangle, cercle…) pour les tests physiques ; ancien, à ne pas étendre
- `monde_settings` : réglages globaux du monde (vitesse, gravité, sous-pas, collision continue, `drawOptions`) ;
  seul endroit qui configure le `PhysicsWorld` et `Time.timeScale` (ralenti : `SetTimeScaleMultiplier`, ne change pas la simulation)
- `LowLevelRaycastGrabber` (`mouse.cs`) : attraper un corps à la souris, debug uniquement
- `RespawnOnKey` : touche R = `LevelRestart.Restart()`, dans `FixedUpdate` avec un ordre d'exécution prioritaire
  (le pas qui suit un redémarrage est identique au premier pas du niveau)

## Rendu
- Rendu debug de la physique uniquement (`drawOptions`, gizmos)
- AUCUN mesh, sprite 
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
