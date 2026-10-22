# Projet

JEU DE SLIMES (décision du 2026-10-21) : jeu mobile en écran VERTICAL, autour de slimes mous qu'on prend plaisir à voir
tomber, s'écraser, trembler et fusionner. Point de départ du gameplay : un « Suika » (jeu de la pastèque) de slimes : on lâche
des slimes dans un réservoir, deux slimes de même couleur qui se touchent fusionnent en un plus gros (« division cellulaire à
l'envers »). Déborder n'est PAS la fin (décision du 2026-10-21) : on peut empiler au-dessus du bord jusqu'au bout ; on PERD
quand un slime tombe hors du réservoir. Très simple, très addictif, très satisfaisant. Le cœur de jeu définitif
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
- Format « niveau » façon campagne Candy Crush / Block Blast (une partie à la Suika, sans fin, jugée bien trop longue) :
  1 min 30 à 2 min 30 par niveau, bornée par un NOMBRE DE LÂCHERS (≈ 30 à 3–4 s chacun), objectif de points à 1–3 étoiles
  (les chaînes = la 3e étoile), suite à graine fixe par niveau (seuils calés sur des parties réelles) ; perdre = un slime
  tombe du réservoir. Points restants comptés dès le dernier lâcher posé
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
1. Strict minimum (scène `Slime/reservoir`) : un réservoir (rectangle ouvert en haut), on lâche des blobs d'en haut
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
   (`growth`), capsules doublées jusqu'à 32, couleur par niveau (dégradé de 10 teintes depuis le cyan du modèle : vert, jaune, orange, rouge, rose, violet),
   `maxLevel` = 10 par défaut. Taille LINÉAIRE depuis le 2026-10-21 (exponentielle avant, jugée trop rapide) : diamètre du
   niveau n = modèle × (1 + (`growth` − 1) × n) ; avec `growth` 1,37 : 1 ; 1,37 ; 1,74… ; 4,7 m au niveau 10 (tient dans le
   réservoir de 8 m ; en exponentiel, 1,25 donnait déjà 9,3 m). Historique de l'exponentiel (sans limite : blobs de 8 puis 16 m
   dans le réservoir de 8 m, éjectés à 200 m/s). Mitose à l'envers : nouvel anneau posé sur une cacahuète (union adoucie des
   deux cercles, col `waist`) TENUE par l'anneau (`SetShape` : angles de repos des joints = angles de la cacahuète − 2π/m,
   capsules allongées à son périmètre), puis ramenée au cercle en `mergeTime` selon `mergeCurve` ; capsules qui s'épaississent.
   Avant : laissée s'arrondir seule = quasi instantané, et avec `growth` < 2 les joints contractaient le périmètre d'un coup. Écarté : recoudre les capsules réelles des deux anneaux (un blob écrasé par sa chute se replie,
   l'anneau recousu se croise en 8 et y reste bloqué). Rejeu exact au bit près (d'un bloc et pas à pas, 33 fusions).
   Tenue selon la taille (`sizeStiffness`) : affaissement relatif ≈ g / (fréquence² × taille), donc à joints égaux un gros
   blob est plus dur ; 1 = fréquences × taille^−½ (même comportement : rapport hauteur / largeur au repos 0,717–0,726 de ×1 à
   ×3,8, sans tendance, contre 0,72 → 0,92 sans ajustement), > 1 plus durs, < 1 plus mous, 0 = aucun ajustement. Amortissement
   inchangé : c'est un ratio sans unité, garder le même ratio garde la similitude. Mesurer au repos COMPLET : un gros blob
   oscille plus lentement (temps × √taille), à 3 s il bougeait encore (faux écart de 3 %). Exact seulement à nombre de
   capsules égal (modèle à 16 : la 1re fusion passe à 32, plus souple).
   À revoir : lâcher en rafale (Espace tenu, un lâcher toutes les 0,3 s) fait naître le slime DANS le précédent (tombé de
   0,7 m seulement) : anneaux enchevêtrés pour de bon (mesuré : capsules qui se chevauchent de 0,39 m, dès le 3e lâcher,
   sans aucune fusion ; très visible avec l'habillage). Piste : ne lâcher que quand le précédent a libéré la place (test
   de distance dans LevelTick, déterministe) ; ceux du bas de la pile s'aplatissent ; surface (et
   masse) du blob fusionné = 2 × celle des deux blobs réunis (conserver la matière = diamètre ×1,41)
   Suite, aperçu, points (2026-10-21) : niveaux définis à un seul endroit (`BlobFusion.LevelDiameter / LevelSides /
   LevelColor / LevelStiffness`, diamètre calculé pareil pour lâchés et fusionnés : égal au bit près,
   c'est ce qui reconnaît deux slimes de même niveau). `BlobDropper` : suite aléatoire à graine fixe (`seed`, xorshift32 en
   entiers, tirée au départ et à chaque lâcher dans `LevelTick` ; pas `UnityEngine.Random`), niveaux 0 à `maxDropLevel` (3),
   `smallBias` pour favoriser les petits ; slime à lâcher dessiné au point de lâcher + « Suivant » à côté ; zone de lâcher
   selon le rayon (2026-10-22 : `halfWidth` = demi-largeur intérieure jusqu'au verre dessiné, 3,8 m ; tout au bord, un
   slime de toute taille longe la paroi à `wallGap` 0,15 m sans la toucher, mesuré ; avant, son bord entrait dans le verre).
   `dropInterval` vaut aussi pour les appuis répétés, plus seulement pour la touche tenue. H = formes physiques (capsules)
   dessinées par-dessus l'habillage (`showHitboxes`). Objectif (`SlimeGoal`, 2026-10-22, case `useGoal`, appliquée au
   prochain R) : `maxDrops` coups (30) et `targetScore` points (3 000, à caler sur de vraies parties ; une partie scriptée
   bête en fait 4 790) qui vont ensemble. Score atteint = plus de lâcher, la chaîne en cours se FINIT (ses points comptent :
   plus de fusion pendant `comboWindow` + `mergeTime`), puis réussi : la simulation retire les slimes et `SlimeFinale`
   (Cosmetics, purement visuel, slimes virtuels dessinés par `SlimeSkin.DrawVirtual`) les fait se diviser en cascade
   jusqu'au plus petit (éclat + son à chaque division, confettis, fanfare) ; les plus petits s'envolent par le haut ; panneau
   après `celebrationTime` (3,5 s). Coups épuisés sans objectif : jugé quand rien n'a fusionné depuis `settleTime` (2,5 s),
   « Raté… ». R = rejouer. Identique sur 2 essais (score, fusions, fin)
   Objectif calculé (2026-10-22, `autoTarget`) : base = points SANS AUCUNE CHAÎNE en fusionnant parfaitement la suite à
   graine (`BlobDropper.PredictSequence`, vérifiée identique au jeu ; paires de même niveau fusionnées comme une retenue
   binaire : `SlimeGoal.PerfectPoints`) avec tous les coups, divisés par (1 + marge) ; marge : Facile 33 %, Moyen 16,5 %,
   Difficile 5 % ; `comboShare` = part de l'objectif à faire en chaînes (0 = chaînes en bonus). Compté en POINTS : en coups
   (points parfaits sur `maxDrops` / (1 + marge) coups), les paliers (un coup de plus ne complète pas toujours une fusion)
   donnaient le même objectif à deux difficultés (graine 2, 30 coups : Moyen = Difficile = 2 340) ; en points : 1 805 /
   2 060 / 2 286. L'inspecteur affiche coups nécessaires / donnés et la difficulté réelle d'un objectif saisi à la main.
   « Parfait » = aucune fusion ratée : en vrai, on en rate
   (slimes coincés loin de leur pareil), donc même Facile peut être dur ; à caler sur de vraies parties.
   Parois = limites (`SlimeWallHold`, 2026-10-22) : un slime lâché au bord s'écrasait contre la paroi et partait en roulant
   jusqu'à l'autre côté (mesuré : niveau 4, de x = 2,68 à −0,67 en 3 s, toujours lancé). Tant qu'il touche une paroi (face
   intérieure = `halfWidth`, la collision y est aussi), la vitesse de son centre qui l'en éloigne est annulée
   (`maxAwaySpeed` 0), même correction pour toutes ses capsules : l'écrasement et le rebond restent. Mesuré : reste à
   x = 2,70 ; partie scriptée identique au bit près (2 essais)
   `SlimeScore` : fusion vers le niveau n = `pointsPerMerge` ×
   n(n+1)/2, fusions à moins de `comboWindow` (1,2 s, en pas de simulation) l'une de l'autre = chaîne, + `comboPercent` (50 %)
   par maillon ; points qui s'envolent (OnGUI, pas pendant le rejeu accéléré), score / meilleur / plus gros niveau. Vérifié :
   partie scriptée de 3600 pas (22 fusions, chaîne ×6, 2 735 points) identique au bit près, score compris, sur 3 essais
   (2 d'un bloc, 1 pas à pas) ; 4000 tirages ≈ 25 % par niveau
   Slimes enfermés (`SlimeEscape`, 2026-10-21) : un slime qui fusionne démarre sur une cacahuète tracée sur des cercles
   idéaux, plus gros que des slimes écrasés dans la pile ; il peut chevaucher la membrane d'un voisin et Box2D le pousse
   DEDANS (reproduit : deux niveau 1 contre un niveau 5 → le niveau 2 né à l'intérieur, et il y restait même après que le
   gros a refusionné). Détection pendant `mergeTime` + `watchTime` après chaque fusion (centre du petit dans le polygone des
   capsules du gros, tri grossier par une capsule) ; sortie : le petit prend le groupe de collision du gros
   (`BLOB.SetCollisionGroup`, il traverse sa membrane et touche tout le reste), poussé à `escapeSpeed` vers l'extérieur et le
   haut, groupe rendu dès qu'il est dégagé. Vérifié : partie en rafale de 3600 pas (65 fusions, 1 sortie), plus aucun slime
   enfermé, identique au bit près sur 3 essais
2. Choisir la représentation des slimes selon les mesures (blob actuel allégé / anneau simple / cercle + gélatine visuelle)
3. Fusion des slimes de même couleur (faite en version brute, voir étape 1), ligne de débordement, score : premier vrai prototype jouable
4. Sensation (gélatine, sons, effets), puis idées retenues une par une. Commencé (2026-10-22), objet « Habillage » de
   `Slime/reservoir` :
   - `SlimeSkin` : un design par niveau (11, inspirés de Slime Rancher sans copie : Goutte bleu, Pousse vert, Bonbon rose,
     Citron jaune, Cristal violet, Tigrou orange, Braise rouge, Miel or, Lagon turquoise, Nébuleuse indigo, Roi irisé ; les 5
     premiers, les plus fréquents, très différents (avant : dégradé bleu → or, jugé trop proche) ; mêmes couleurs dans
     `BlobFusion.levelColors`) = couleurs, motif,
     accessoire, style de visage, lueur. La gelée suit l'anneau physique (contour lissé entre les capsules) ; motif et visage
     dans un repère qui se déforme avec elle ; visage remis à l'endroit après avoir roulé (`uprightTime`). Vivant : clignements,
     regard vers le slime à lâcher ou une fusion voisine, bouche ouverte en chute, grimace au choc, sourire après fusion,
     éclair et accessoires qui poussent à l'éclat de la fusion. Yeux agrandis (2026-10-22). Vitrine des designs dans la
     Scene view
   - `SlimeFx` : fusion en deux temps (l'explosion cachait la mitose, 2026-10-22) : pendant la fusion, seulement une lueur
     douce DERRIÈRE ; à `popAt` (80 % de `mergeTime`) l'éclat : halo et onde derrière les slimes (file 2990), gouttes et
     étincelles depuis sa surface, cloche et son de chaîne au même moment ; points affichés au-dessus du nouveau slime.
     Plus fort avec le niveau et la chaîne :
     tremblement de l'écran, pulsation du fond, annonce « ×3 Super ! » (dès 2 maillons), ralenti (dès 4 maillons ou niveau 9),
     éclair plein écran, confettis (record dès le niveau 5, niveau max). Éclaboussures aux chocs, pointillés de visée
   - `SlimeBackdrop` : fond dégradé à bulles floues, bocal de verre sur la courbe du réservoir ; `SlimeSound` : voir Son
   - Mesuré (PC) : habillage 0,53 ms par image pour 15 slimes + aperçus (contour adaptatif : ≈ 6 px par côté, 72 points max ;
     1,6 ms avant optimisation). Partie réelle avec tous les effets (ralenti compris) puis rejeu de ses entrées : identiques au
     bit près (5 875 pas, 17 fusions, même score)
5. **Carte du monde, Aventure / Sans fin** (commencé le 2026-10-22, scène `Slime/carte`, première des Build Settings) :
   - Écran titre sur la carte : « Aventure » (la carte) ou « Sans fin » (réservoir sans objectif, graine tirée au hasard à
     chaque nouvelle partie et affichée, record gardé ; la partie reste rejouable au bit près avec sa graine).
   - Campagne = asset `Settings/Campagne` (`SlimeCampaign`) : zones de niveaux réglés UN PAR UN à la main (graine, coups,
     difficulté ou objectif à la main, part en chaînes, niveau max lâché, notes pour de futures mécaniques). Zone 1
     « Plaine », 30 niveaux : 1–5 faciles, puis moyens, difficiles aux 10 / 20 / 30, facile juste après. Inspecteur : tableau
     avec objectif calculé et marge réelle (règles lues dans le réservoir ouvert), « Jouer » lance n'importe quel niveau (même
     verrouillé, outil de dev), « Remettre la progression à zéro ». Étoiles : 1 = réussi, 2 / 3 selon les coups restants
     (≥ 10 % / ≥ 20 %, à caler sur de vraies parties)
   - Style (décision du 2026-10-22, après un essai en 3D low-poly jugé raté) : diorama de PAPIER DÉCOUPÉ façon Paper Mario /
     Candy Crush / Don't Starve. Sol peint à plat (herbe à taches, grandes dalles cerclées d'encre, jetons de carton numérotés
     : gris verrouillé, rose en cours, doré fini avec ses étoiles), tout le reste en cartes plates face à la caméra, contour
     encré teinté + liseré de papier blanc + ombre au sol. Caméra en perspective inclinée (42°) et monde courbé au loin
     (façon Animal Crossing : on ne voit pas le bout), nuages de papier sur ce qui n'est pas découvert (niveaux débloqués + le
     suivant en avant-goût) qui s'écartent à chaque victoire
   - Avatar : vrai anneau Box2D qui sautille TOUT SEUL vers le niveau touché (pas un saut de gameplay ; physique dans un plan
     vu de côté, dessin posé au point du chemin), placeholder = le Roi. Retour d'une victoire : jeton doré + étoiles, nuages,
     saut au niveau suivant, sa fiche s'ouvre
   - Mesuré (PC) : scripts de la carte ≈ 0,08 ms par image (avatar compris), construction 12 ms ; ≈ 2 500 découpes en un
     seul maillage trié ; 30 images/s quand rien ne bouge (`idleFrameInterval`)
   - À faire : commandes tactiles du réservoir (encore au clavier), défaite « un slime tombe » (en Sans fin on quitte par
     Menu), sons de la carte, zones suivantes
6. Avatar à créer : le blob du joueur sur la carte, mélange de tous les accessoires et shaders possibles des slimes
   (en attendant : le Roi)

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
- `Scripts/Slime/` : le nouveau jeu. `BlobDropper` (lâcher, suite aléatoire, aperçu ; possède la liste des blobs et leur
  pas ; événement `Dropped`), `BlobFusion` (fusion et définition des niveaux), `SlimeScore` (points, événement `Scored`
  après chaque fusion comptée), `SlimeEscape` (sort les slimes enfermés dans un autre), `SlimeGoal` (coups max + objectif
  de points calculé selon la difficulté, fin de niveau ; limite donnée à `BlobDropper.dropLimit`), `SlimeWallHold` (parois
  qui retiennent au lieu de relancer), `SlimeCampaign` (niveaux de l'Aventure), `SlimeSession` (partie choisie, progression
  dans PlayerPrefs, gardée en mémoire), `SlimeLevelSetup` (applique la partie choisie au réservoir, boutons de fin ; Play
  direct dans l'éditeur = réglages de la scène, rien ne change), `SlimeUi` (boutons et textes OnGUI communs).
- `Scripts/Map/` : la carte (`SlimeMap` décor et caméra, `SlimeMapMesh` sol, `SlimeMapAvatar`, `SlimeMapMenu` titre / fiche /
  enchaînements). Purement visuel, hors simulation (l'avatar a sa physique Box2D, mais rien n'y est rejoué).
- `Scripts/Cosmetics/` : habillage du jeu de slimes (`SlimeSkin`, `SlimeFx`, `SlimeBackdrop`, `SlimeSound`, `SlimeFinale`) ; ils mettent
  les événements de la simulation en file et ignorent le rejeu accéléré. `SlimeSkin.Impact` = chocs (vus à l'affichage)
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
- Habillage (décision du 2026-10-22) : maillages procéduraux reconstruits à chaque image par `Cosmetics/` (`SlimeSkin`,
  `SlimeFx`, `SlimeBackdrop`), dessinés par `Graphics.RenderMesh` pour chaque caméra ; shaders `Assets/Shaders/`
  (`Slime/Body`, `Slime/Fx`, `Slime/Backdrop`). Ordre de dessin par file de rendu : fond 2900, vitre 2950, slimes 3000,
  parois 3010, effets 3020. Lueur : couleurs > 1 + bloom (volume « Effets écran », profil `Settings/Slime Volume`)
- Toujours aucun sprite ni texture importée : formes, motifs et visages calculés (distances signées dans le shader)
- Carte : `Slime/Map` (sol peint), `Slime/MapCard` (découpes de papier : une sorte = une fonction de forme, alpha prémultiplié
  + profondeur écrite, maillage trié du plus loin au plus près), `Slime/MapSprite` (ciel, étoiles, ombres). Files : ciel 1000,
  sol 2000, ombres 2950, découpes 2970, étoiles 2990, avatar 3000. Couleurs de sommets données en sRGB : les convertir
  (`Color.linear`), sinon tout ressort délavé (projet en linéaire)
- Dessin debug de la physique : `monde_settings.drawOptions` (formes sélectionnées seulement) et `BlobDropper.debugDraw`

## Son
- Procédural (décision du 2026-10-22) : `SlimeSound` calcule tous les sons au lancement à partir de formules, aucun fichier
  audio. Lâcher = « plop » ; choc = « splotch » (volume selon la vitesse perdue, silencieux sous 3,5 m/s) ; fusion = « bloup »
  plus grave pour les gros + cloche dont la note monte à chaque maillon (gamme pentatonique) ; grondement (niveau 6+),
  paillettes (chaîne de 3+), fanfare (record dès le niveau 4, niveau max). Hauteur variée au hasard, son placé gauche / droite
- Je ne peux pas écouter : les réglages se font à l'oreille par l'utilisateur (volumes, `semitonesPerLevel`, `jitter`…)

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
