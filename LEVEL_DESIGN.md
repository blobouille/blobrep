# Règles de level design

Pour moi (rappel des bons principes) et surtout pour Claude, à lire avant de créer ou modifier un niveau.
Référence actuelle : la scène `Assets/Scenes/poc niveau.unity`, courte mais riche en éléments et en variations.

## Objectif
Amener le blob au bout du niveau.

## Sensations recherchées
Le joueur doit éprouver de la satisfaction en voyant le blob prendre les rampes et faire des sauts. Il doit se sentir
récompensé d'arriver au bout du niveau, d'autant plus que le niveau est difficile.

- **Courbes** : conçues pour être prises de façon satisfaisante, le blob doit bien « plonger » dedans
  (entrée tangente à sa trajectoire, pas de cassure).
- **Flippers** :
  - quand ils projettent le blob après qu'il a accumulé du momentum, ils prolongent le terrain (le levier au repos continue la pente) ;
  - autre cas : un passage vertical, où le but est d'envoyer le blob en hauteur.

## Cohérence des paramètres (dans tout le jeu)
Un joueur qui progresse ne doit jamais être surpris par un changement de comportement. **Pour ces éléments, seul le
placement change**. Leurs valeurs peuvent évoluer pendant le développement, mais toujours pour tous les exemplaires à la fois.

Hiérarchie des valeurs : **la scène `poc niveau` est le modèle** → ses réglages sont les valeurs par défaut des scripts
→ tout nouveau niveau utilise les scripts tels quels, sans retoucher ces paramètres. Si un réglage officiel change,
le changer dans `poc niveau` ET dans la valeur par défaut du script.

- **Flippers** : même taille, même angle (45°) et même vitesse (500 °/s) partout. Le mode torque n'est jamais utilisé.
  Une seule touche pour tous les flippers : seuls la position et l'orientation changent ; pour un flipper retourné,
  le signe de `activeAngle` donne le sens de la frappe (ex. -45).
- **Bumpers** (`PinballBumper`) : même taille et même vitesse partout, même touche que les flippers. Le déplacement
  (`travel`) est en cours de réglage : une fois fixé, il devient lui aussi une valeur officielle.
- **Blob** : toujours les paramètres par défaut de son script.
- **Boost zones** : paramètres fixes (`acceleration` 150 m/s², plafond `maxSpeed` 30 m/s, à régler). Les futures variantes (couleurs) auront chacune leurs valeurs fixes.
- **Caméra** et réglages du monde : ceux de `poc niveau` (valeurs par défaut des scripts).
- Libres selon le niveau : la forme des courbes, le nombre et la taille des piques, la taille et la vitesse des scies.

## Difficulté
Progression : facile → moyen → difficile, puis éventuellement plus loin.

Elle se dose par :
- **la vitesse** : le temps qu'il reste pour réagir alors que le blob va vite ;
- **la fréquence** : combien de fois il faut actionner les leviers ;
- **la précision** : la fenêtre de timing pour actionner un levier et envoyer le blob au bon endroit.

Mesure objective (pour Claude) : pour chaque coup de levier, simuler des appuis plus tôt et plus tard que le coup idéal
(rejeu déterministe) et mesurer la plage de timing qui réussit, en millisecondes. Large = facile, étroite = difficile.

## Architecture
- Plus proche de *Trackmania* que de *Geometry Dash* : **plusieurs chemins** peuvent mener au même endroit. Un chemin plus
  difficile d'accès doit rapporter en conséquence (temps gagné, raccourci).
- **Le chemin classique** doit être suivi naturellement et facile à comprendre, sans indication.
- **Les raccourcis restent subtils** : le jeu ne prend pas le joueur par la main pour les trouver. Laisser de la place aux
  tricks difficiles : les joueurs trouveront eux-mêmes des glitchs et des raccourcis auxquels on n'a pas pensé.
- **Pas de piques partout** : les éléments qui détruisent le blob servent à limiter ses déplacements et à désigner l'objectif.
- **Rythme** : après une série de passages durs, un moment plus calme (plusieurs courbes satisfaisantes à enchaîner,
  éventuellement avec des accélérateurs) avant de repartir sur de nouveaux défis.

## Méthode de construction (surtout pour Claude)
1. **Partir du mouvement, pas des obstacles.** On ne pose pas des obstacles en cherchant ensuite comment les passer.
   On donne d'abord du momentum au blob (une chute puis un boost, ou directement un coup de levier), on réalise le saut,
   puis on place les obstacles autour en respectant les règles ci-dessus. Le saut donne la direction à suivre.
2. **La difficulté = la marge autour du coup idéal.** Selon le niveau de difficulté, les obstacles sont plus ou moins
   proches de la trajectoire du coup de levier idéal.
3. **Varier les usages du levier.** Il y a plein de façons de s'en servir (prolonger une pente, renvoyer vers le haut,
   rattraper, relancer…) : être créatif et changer régulièrement ce que le blob doit accomplir.
4. **Tester par morceaux.** Utiliser les points de sauvegarde (`TrainingMode`) : modifier un passage puis le tester
   directement depuis le point juste avant, sans refaire tout le niveau.

## Repères mesurés (`level2 by claude`, réglages de `poc niveau 1`)
Mesurés en simulant les appuis pas à pas (1 pas = 8,3 ms). À revérifier si les réglages officiels changent.
- **Flipper au bout d'une pente** (blob à 9–13 m/s) : fenêtre utile de 90 à 160 ms, mais l'arc change beaucoup selon le moment
  (de 2 à 13 m de haut). Réception : une longue pente raide, façon tremplin de saut à ski, qui accepte tous les arcs de la fenêtre,
  avec une fosse à piques dessous pour un échec lisible. Les appuis tardifs donnent des arcs plus plats, donc plus rapides.
- **Bumper au sol** : saut de 7 à 10 m de haut quelle que soit la vitesse, fenêtre ≈ 120 ms à 15 m/s. Appuyé trop tôt, il fait mur
  et arrête le blob.
- **Flipper tenu levé** = tremplin : geste facile (tenir pendant le passage), mais il coûte beaucoup de vitesse (≈ 21 → 8 m/s).
- **Berceau contre un mur** (flipper au fond d'un V, le blob s'écrase contre le mur) : le lancer part du repos, donc presque
  identique quelle que soit l'arrivée. Remet à zéro les écarts de vitesse accumulés, et fait un moment calme.
- **Catapulte sur une paroi à grande vitesse** : fenêtre de 2 à 4 pas, injouable au-dessus de ~8 m/s.
- **Sommet à passer sans appui** : garder de la marge (≥ 5 m/s au sommet). Toute modification du niveau, même loin, change l'ordre
  interne de Box2D, donc la vitesse après les chocs (tremplin, réception dure) : un sommet trop juste finit par bloquer le blob.
- **Bosses et vagues** : chaque bosse coûte beaucoup d'énergie (≈ 12 m de hauteur pour une vague de 55 m à 20 m/s), plus encore si
  le blob décolle à la crête. Une longue pente douce (−3 à −6°) garde la vitesse (≈ 23 m/s sur 100 m).
- **Accélérateur** (200 m/s², plafond 45) : une zone fait passer de 10 à ≈ 32 m/s. Trop fort avant un flipper ; à réserver
  aux lignes droites (ici le raccourci).
- **Géométrie** : une seule courbe continue sur le trajet (deux courbes jointes bout à bout laissent passer le blob entre elles),
  coins vifs = poignées nulles. Une scie posée au sol s'enfonce dedans, sinon le blob peut être poussé à travers le sol.

## Objectif actuel
Un nouveau niveau d'environ 30 secondes (temps d'un passage réussi d'une traite, sans erreur), inspiré de `poc niveau`.
Pas encore d'éléments de départ / arrivée : viendront plus tard (peut-être des checkpoints façon Trackmania, but = finir le plus vite).
