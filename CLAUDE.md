# Projet

Jeu de flipper (pinball) 2D. La "balle" est un blob souple physique (anneau de capsules
reliées par des joints). Niveau construit avec des courbes de Bézier épaisses (murs/rails)
et des flippers.

## Objectifs actuels
- Gameplay d'abord : esthétique et sons viendront plus tard
- Résultat TOUJOURS déterministe : mêmes entrées = même simulation à chaque lancement

## Technique
- Unity 6000.6, physique 2D bas niveau `Unity.U2D.Physics` (Box2D v3 : PhysicsWorld,
  PhysicsBody, PhysicsShape, PhysicsChain, joints)
- PAS de Physics2D / Rigidbody2D / Collider2D classiques 
- Un seul monde : `PhysicsWorld.defaultWorld`
- Les `NativeList` doivent être détruits/libérés dans `OnDestroy` (sinon fuite mémoire)
- Utiliser `XxxDefinition.defaultDefinition` pour les définitions (corps, formes, joints), puis modifier les champs

## Déterminisme
- Pas de `Random` sans graine fixe explicite
- Logique de simulation et pilotage physique dans `FixedUpdate`, jamais dans `Update`
  (dépendrait du framerate) ; utiliser `Time.fixedDeltaTime`
- Ordre de création des corps/joints stable et explicite (ne pas dépendre de l'ordre
  d'exécution des scripts) ; ordre d'itération stable (pas de dépendance aux Dictionary/HashSet)
- Signaler tout risque de non-déterminisme plutôt que de l'ignorer

## Construction des niveaux : modèle CurvePath
Tout ce qui sert à créer un niveau suit le modèle existant (CurvePath2D / PinballFlipper) :
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur
- Aperçu visible dans la Scene view AVANT de lancer le jeu : `OnDrawGizmos` (+ Editor custom
  avec handles si besoin), avec la même géométrie que celle envoyée à la physique
- La physique n'est créée qu'en Play (`Application.isPlaying`), dans `OnEnable`, et détruite dans `OnDisable`
- Une seule source de vérité géométrique pour aperçu et collision

Scripts existants :
- `CurvePath2D` : courbe de Bézier cubique par morceaux, épaisseur uniforme (center/left/right),
  échantillonnage adaptatif (`resolution`, `straightTolerance`, `miterLimit`),
  `BuildCollisionChains()` pour la collision
- `CurvePath2DEditor` : édition dans la Scene view (points, tangentes, modes Free/Aligned/Vector/Auto, Undo)
- `CurveCollider2D` : crée la collision statique du chemin (backend par défaut : `LowLevelQuads`)
- `PinballFlipper` : flipper (2 cercles + quad), charnière à moteur (`useTorque`) ou kinematic piloté en vitesse
- `BLOB` : la balle du joueur (structure physique uniquement)

## Rendu
- Rendu debug de la physique uniquement (`drawOptions`, gizmos)
- AUCUN mesh, sprite 
- Aucun son pour l'instant

## Conventions de code
- Pas de commentaires, sauf point vraiment non évident
- Paramètres exposés : toujours `[Header]` + `[Tooltip]` (en français, précis sur l'effet et les unités),
  plus `[Min]` / `[Range]` quand pertinent
- Noms de variables et classes en anglais ; champs privés préfixés `_` (le `m_` de BLOB est un reste, à harmoniser)
- Un script = une responsabilité ; ne pas renommer les champs publics existants (valeurs d'inspecteur perdues)
- Maths avec `Unity.Mathematics` pour la physique ; dimensions en unités Unity : toujours préciser
  si un paramètre est relatif à une échelle ou absolu

## Façon de travailler
- Modifications ciblées : ne toucher qu'aux fichiers demandés
- Proposer un plan avant les changements importants
- Réponses courtes : dire ce qui a été modifié, sans longs développements

## À ne pas toucher
- Fichiers `.unity`, `.prefab`, `.meta` (modifiés dans l'éditeur Unity)
- Dossiers `Library/`, `Temp/`, `Logs/`, `obj/`
- Ne jamais supprimer de fichiers sans me demander d'abord
