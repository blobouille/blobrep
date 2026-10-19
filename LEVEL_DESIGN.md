# Règles de level design

Pour moi (rappel des bons principes) et surtout pour Claude, à lire avant de créer ou modifier un niveau.
Référence actuelle : la piste `Assets/Scenes/skate reboot.unity` (démo du skateur), en attendant un premier vrai niveau.
Les règles de l'époque du blob sont dans le tag git `blob-et-debut-skate`.

## Objectif
Amener le skateur au bout du niveau, d'une traite, sans tomber, en gardant sa vitesse.

## Sensations recherchées
Le joueur doit éprouver de la satisfaction à garder son élan, à enchaîner les courbes, à réussir ses sauts, ses figures et
ses réceptions. Il doit se sentir récompensé d'arriver au bout, d'autant plus que le niveau est difficile.

- **Momentum** : aucun moteur ne fait avancer le skateur. Toute la vitesse vient des pentes et des éléments (flippers,
  bumpers, accélérateurs). Un niveau se lit comme un budget d'énergie : chaque montée, bosse, choc ou freinage en coûte.
- **Courbes** : conçues pour être prises proprement, la planche doit « plonger » dedans (entrée tangente à la trajectoire, pas
  de cassure). Les creux serrés font cabrer la planche (accélération v²/R) : rayon large dès que la vitesse est grande.
- **Réceptions** : une réception se prépare. Pente descendante dans le sens du saut (façon tremplin de saut à ski), assez
  longue pour accepter tous les arcs plausibles ; une réception à plat ou contre une pente montante casse l'élan.
- **Figures** : un saut doit laisser le temps d'une rotation si on veut en faire une (temps en l'air ≈ hauteur), avec une
  réception lisible. Plus tard, les figures rendront de la vitesse (boost).
- **Frein (tail au sol)** : outil de précision, pas une obligation permanente. Prévoir des passages où doser la vitesse
  (avant un saut trop long, une scie, un flipper) ; ne pas forcer à freiner partout, cela casse le rythme.
- **Flippers et bumpers (Espace)** :
  - quand ils projettent le skateur après qu'il a pris de l'élan, ils prolongent le terrain (le levier au repos continue la pente) ;
  - tremplin tenu levé, renvoi vers le haut dans un passage vertical, relance après un creux.
- **Mort** : la tête ou le corps qui se cogne, un obstacle, une chute sur le nose. Plafonds et murs bas sont donc des
  dangers aussi réels que les piques : en tenir compte sous les sauts et dans les tubes.

## Cohérence des paramètres (dans tout le jeu)
Un joueur qui progresse ne doit jamais être surpris par un changement de comportement. **Pour ces éléments, seul le
placement change**. Leurs valeurs peuvent évoluer pendant le développement, mais toujours pour tous les exemplaires à la fois.

Hiérarchie des valeurs : **les valeurs par défaut des scripts sont les réglages officiels** → tout nouveau niveau utilise les
scripts tels quels, sans retoucher ces paramètres. Si un réglage officiel change, le changer dans la valeur par défaut du
script ET dans les scènes de référence. (L'ancienne scène modèle `Legacy/Blob/poc niveau` était pensée pour le blob : une scène
modèle du skate reste à définir, `skate reboot` en tient lieu.)

- **Skateur** : toujours les paramètres par défaut de `Skater` et `Skateboard`.
- **Flippers** : même taille, même angle (45°) et même vitesse (500 °/s) partout. Le mode torque n'est jamais utilisé.
  Une seule touche (Espace) pour tous les leviers : seuls la position et l'orientation changent ; pour un flipper retourné,
  le signe de `activeAngle` donne le sens de la frappe (ex. -45).
- **Bumpers** (`PinballBumper`) : même taille et même vitesse partout, même touche que les flippers. Le déplacement
  (`travel`) est en cours de réglage : une fois fixé, il devient lui aussi une valeur officielle.
- **Boost zones** : paramètres fixes (`acceleration`, plafond `maxSpeed`, à régler pour le skateur). Les futures variantes
  (couleurs) auront chacune leurs valeurs fixes.
- **Caméra** et réglages du monde : valeurs par défaut des scripts (gravité −20, caméra ortho 9 sur `skate reboot`).
- Libres selon le niveau : la forme des courbes, le nombre et la taille des piques, la taille et la vitesse des scies.

## Difficulté
Progression : facile → moyen → difficile, puis éventuellement plus loin.

Elle se dose par :
- **la vitesse** : le temps qu'il reste pour réagir alors que le skateur va vite ;
- **la fréquence** : combien de gestes il faut enchaîner (rotations, freinages, leviers) ;
- **la précision** : la fenêtre de timing d'un levier, la durée d'appui d'une rotation pour replaquer, le dosage d'un frein ;
- **la gestion de l'élan** : une erreur qui coûte de la vitesse peut rendre impossible le passage suivant (punition différée :
  à doser, un niveau facile laisse de la marge d'énergie).

Mesure objective (pour Claude) : pour chaque geste, simuler des appuis plus tôt / plus tard et plus courts / plus longs que le
geste idéal (rejeu déterministe) et mesurer la plage qui réussit, en millisecondes. Large = facile, étroite = difficile.

## Architecture
- Plus proche de *Trackmania* que de *Geometry Dash* : **plusieurs chemins** peuvent mener au même endroit. Un chemin plus
  difficile d'accès doit rapporter en conséquence (temps gagné, raccourci).
- **Le chemin classique** doit être suivi naturellement et facile à comprendre, sans indication.
- **Les raccourcis restent subtils** : le jeu ne prend pas le joueur par la main pour les trouver. Laisser de la place aux
  tricks difficiles : les joueurs trouveront eux-mêmes des glitchs et des raccourcis auxquels on n'a pas pensé.
- **Pas de piques partout** : les obstacles servent à limiter les trajectoires et à désigner l'objectif.
- **Rythme** : après une série de passages durs, un moment plus calme (plusieurs courbes satisfaisantes à enchaîner, une
  grande descente, éventuellement des accélérateurs) avant de repartir sur de nouveaux défis. Alterner sol (lecture du
  terrain, frein) et air (figures, réceptions).

## Méthode de construction (surtout pour Claude)
1. **Partir du mouvement, pas des obstacles.** On donne d'abord de l'élan au skateur (une descente, un accélérateur, un coup de
   levier), on réalise le saut et la réception, puis on place les obstacles autour. Le saut donne la direction à suivre.
2. **La difficulté = la marge autour du geste idéal.** Selon le niveau de difficulté, les obstacles (et les plafonds) sont plus
   ou moins proches de la trajectoire idéale.
3. **Varier les gestes.** Projeter, renvoyer vers le haut, freiner avant un saut, tourner en l'air pour replaquer, tenir un
   tremplin : changer régulièrement ce que le joueur doit accomplir.
4. **Tester par morceaux.** Utiliser les points de sauvegarde (`TrainingMode`) : modifier un passage puis le tester
   directement depuis le point juste avant, sans refaire tout le niveau.
5. **Vérifier l'énergie.** Mesurer la vitesse en entrée et en sortie de chaque passage, sans erreur et avec une petite erreur
   (rotation un peu tardive, frein un peu long) : le passage suivant doit rester faisable selon la difficulté voulue.

## Repères mesurés (skateur, `skate reboot`)
Mesurés en simulant les appuis pas à pas (1 pas = 8,3 ms). À compléter, et à revérifier si les réglages officiels changent.
- **Kicker** : tape de 50 à 85 ms pour replaquer ; salto arrière = appui de 0,4 à 0,6 s.
- **Frein tenu** (tail au sol) : 17 → 4 m/s par le seul frottement.
- **Bumper au sol** : sous la planche il l'envoie à ≈ 5,5 m de haut ; trop tôt, il fait mur.
- **Vitre posée au sol** : la planche la brise, 23 → 14 m/s.
- **Bascule avant tenue** : nose au sol puis chute ≈ 0,3 s plus tard (`noseFallTime`).

## Anciens repères (époque blob, `Legacy/Blob/level2 by claude`) — À REMESURER avec le skateur
Gardés comme ordres de grandeur pour les éléments communs ; le skateur ne réagit pas comme le blob.
- **Flipper au bout d'une pente** (9–13 m/s) : fenêtre utile de 90 à 160 ms, arc très variable (2 à 13 m de haut) ; réception =
  longue pente raide façon tremplin de saut à ski, fosse à piques dessous pour un échec lisible.
- **Flipper tenu levé** = tremplin : geste facile mais coûte beaucoup de vitesse (≈ 21 → 8 m/s).
- **Catapulte sur une paroi à grande vitesse** : fenêtre de 2 à 4 pas, injouable au-dessus de ~8 m/s.
- **Sommet à passer sans appui** : garder de la marge (≥ 5 m/s au sommet). Toute modification du niveau, même loin, change
  l'ordre interne de Box2D, donc la vitesse après les chocs : un sommet trop juste finit par bloquer.
- **Bosses et vagues** : chaque bosse coûte beaucoup d'énergie, plus encore si on décolle à la crête. Une longue pente douce
  (−3 à −6°) garde la vitesse.
- **Accélérateur** (200 m/s², plafond 45) : une zone fait passer de 10 à ≈ 32 m/s. À réserver aux lignes droites.
- **Géométrie** (toujours valable) : une seule courbe continue sur le trajet (deux courbes jointes bout à bout laissent passer
  un corps entre elles), coins vifs = poignées nulles. Une scie posée au sol s'enfonce dedans.

## Objectif actuel
Rendre le skate satisfaisant sur la piste `skate reboot`, puis construire un premier niveau de skate d'environ 30 secondes
(temps d'un passage réussi d'une traite). Pas encore d'éléments de départ / arrivée : viendront plus tard (peut-être des
checkpoints façon Trackmania, but = finir le plus vite).
