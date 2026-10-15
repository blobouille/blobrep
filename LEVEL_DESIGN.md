# Manifeste de level design — BLOB

Règles pour construire un bon niveau. Elles viennent des principes du jeu (CLAUDE.md) et de ce que la
physique du blob a montré en test. Les chiffres sont mesurés avec les réglages actuels (flipper et blob par
défaut, gravité 9,81, pas de 120 Hz).

## 1. Une idée à la fois : apprendre → tester → combiner
- Chaque nouvel élément est d'abord présenté **seul et sans risque**, puis demandé, puis combiné.
- Le premier usage d'une touche doit être évident : un seul flipper visible, le blob posé dessus.
- Ordre type : flipper gauche → flipper droit → combinaison → timing → finale.

## 2. Difficulté croissante, mais avec des respirations
- Courbe en dents de scie : après un passage exigeant, un moment « gratuit » (descente, glissade) pour souffler.
- La difficulté se mesure en **largeur de fenêtre de timing** :
  - facile : marche quel que soit le moment, dès que le blob est arrêté ;
  - moyen : fenêtre ≥ 0,5 s ;
  - difficile : fenêtre de 0,15–0,25 s, que le joueur peut quand même voir venir (repère visuel : « le bout du flipper »).
- Un passage difficile doit toujours avoir un **signal lisible** du bon moment.

## 3. Jamais de blocage (soft-lock)
- Tout endroit où le blob peut s'arrêter doit être **sur un flipper qui peut le relancer**.
- Sols en **pente continue** vers un flipper : pas de creux, de cuvette ni de replat hors flipper.
- Un sol en pente douce qui va et revient (oscillation) est acceptable s'il finit sur un flipper.
- Vérifier chaque raté jusqu'à l'arrêt complet du blob : où finit-il ?

## 4. Un raté coûte du temps, pas le niveau
- Tant qu'il n'y a pas de checkpoint, un échec ramène **au début du défi en cours** (boucle de retry),
  jamais au début du niveau.
- Boucle de retry courte : idéalement < 5 s, maximum ~10 s.

## 5. Guider plutôt que viser
- Le blob est mou : deux lancers « identiques » dispersent beaucoup. Ne jamais demander d'atterrir
  sur une petite cible en vol libre.
- Utiliser des **capots, tubes et entonnoirs** qui récupèrent toute une famille de trajectoires.
  La difficulté vient du *seuil* (assez fort / pas assez), pas de la précision du point de chute.
- Un mur sert de **frein** : le rebond (0,5) dissipe l'énergie. Pratique pour stopper le blob après une descente.

## 6. Comportements physiques à connaître (mesurés)
- **Lancer au repos contre une butée** : le blob monte d'environ **8 m** presque à la verticale, et dérive
  d'environ 1 m **vers la charnière** du flipper.
  → Le capot qui renvoie le blob se courbe **du côté de la charnière**.
- **Lancer sans butée** (blob posé ou qui roule) : départ selon la normale du flipper,
  donc en haut à droite pour un flipper gauche et en haut à gauche pour un flipper droit.
- **Le coup le plus fort part du bout du flipper** ; le coup près de la charnière est faible.
- **Un sol qui plonge est un tremplin** : le blob décolle. Pour une descente qui tourne vers le bas,
  le faire tomber d'un rebord et le récupérer avec une **paroi extérieure courbe**.
- Godet de flipper type : butée à **1,95** de la charnière, fond 1,9 sous la charnière, rotation de repos
  ±30° (gauche : −30° / droit : 210°).
- Garder **≥ 0,5** (rayon du blob) + demi-épaisseur entre une trajectoire et toute paroi qu'elle ne doit pas toucher.

## 7. Les touches agissent sur tous les flippers d'un côté
- Gauche = tous les flippers gauches (charnière à gauche), Droite = tous les flippers droits.
- Un flipper vide qui bouge ne doit jamais gêner : le vérifier quand plusieurs flippers du même côté
  sont proches.
- On peut s'en servir comme mécanique (enchaîner gauche puis droite, piéger un appui trop long).

## 8. Toutes les directions, en format horizontal
- Le niveau doit aller en haut, en bas, à gauche et à droite. Une tour en zigzag (gauche/droite) donne la montée,
  une grande descente donne la vitesse.
- Une section verticale doit tenir dans la hauteur de la caméra (16 m à taille ortho 8) : étages d'environ 5 m.

## 9. Durée et rythme
- Cible : environ **1 minute** pour un joueur moyen, retries compris.
- À titre indicatif : tuto ~15–20 s, respiration ~5 s, défis ~20–30 s.

## 10. Valider avec le bot avant de livrer
- Simulation déterministe pas à pas (monde en mode `Script`, flippers pilotés par un horaire d'appuis).
- Pour chaque défi : balayer le moment d'appui, noter la fenêtre de réussite, et vérifier que **tous**
  les ratés retombent dans la boucle de retry.
- Les sections faciles doivent réussir sur toute la plage d'appuis raisonnables.
