# Projet

JEU DE SLIMES (décision du 2026-10-21) : jeu mobile en écran VERTICAL, autour de slimes mous qu'on prend plaisir à voir
tomber, s'écraser, trembler et fusionner. Point de départ du gameplay : un « Suika » (jeu de la pastèque) de slimes : on lâche
des slimes dans un réservoir, deux slimes de même couleur qui se touchent fusionnent en un plus gros (« division cellulaire à
l'envers »). Déborder n'est PAS la fin (décision du 2026-10-21) : on peut empiler au-dessus du bord jusqu'au bout ; on PERD
quand un slime tombe hors du réservoir. Très simple, très addictif, très satisfaisant.
Cœur de jeu (décision du 2026-10-26) : la mécanique SPACE (boîte fermée, gumori très mous, réserve et LANCER, voir étape 5)
est gardée, jugée bien plus intéressante ; le jeu « terrestre » (lâcher façon Suika) est mis de côté (code et scène gardés).
Les niveaux varient par des MÉCANIQUES de niveau (bocaux, moulin…), le geste reste le même (combiner), comme Candy Crush ;
chaque mécanique doit se lire en une seconde sur un écran de téléphone, une nouveauté à la fois.

Avant : un jeu de momentum avec un BLOB (balle souple) poussé par des flippers, puis un skateur (abandonnés). Tout cet état
(code, scènes, ancien CLAUDE.md complet avec toutes les mesures) est conservé par les tags git `blob-et-debut-skate` et
`avant-slime`. Leur code a été retiré du projet le 2026-10-25 (voir « Code du jeu »).

Noms (décision du 2026-10-23) : le jeu et son univers (personnages réutilisables dans d'autres jeux) = « Gumori » (marque à
déposer, classes 9 et 41, et nom du jeu sur les stores) ; nom commercial de la micro-entreprise = « Gumori Games »
(décision du 2026-10-25, avant : « Gumori » ; dénomination légale = nom de l'exploitant ; « Igueldan » gardé en réserve
pour une future société). Libres à l'INPI le 2026-10-23, gumori.com aussi ; pseudo @gumori pris (variante
@gumori.games, X : sans point ; compte Google « Gumori Games », gumori.games@gmail.com). Structure : micro-entreprise d'abord, société (EURL / SASU) plus tard si le jeu rapporte. Communication en anglais, sortie mondiale. Modèle : gratuit + publicité (surtout récompensée),
récompenses au hasard gagnées en jouant ; achats intégrés plus tard, DONT des tirages au hasard PAYANTS (loot boxes,
décision du 2026-10-25) : chances affichées avant l'achat (Apple / Google), garantie de rare au bout de N tirages,
interrupteur par pays réglable à distance (Belgique : interdits ; Brésil : mineurs ; Australie : note 15+ minimum), tirage
fait côté serveur, jamais dans la simulation. Toute récompense de pub qui agit sur la
partie (secousse, continuer…) doit passer par `LevelInput` (entrée enregistrée), sinon les replays divergent.

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
- En ligne (idée du 2026-10-25, plus tard) : amis (s'ajouter, voir la collection de l'autre, profil public choisi par le
  joueur), événements en ligne limités dans le temps. Piste : Unity Gaming Services (Authentication en compte invisible puis
  lié à Google / Apple, pas de compte e-mail ; Cloud Save en JSON à nous ; Friends ; Leaderboards ; Remote Config pour les
  événements), appels isolés dans une seule classe (`GumoriCloud`) pour pouvoir changer de service. Un événement = des
  données (graine, règles, récompenses) lues AVANT la partie, jamais pendant : la partie reste déterministe et rejouable.
  Scores vérifiables en rejouant les entrées côté serveur. RGPD : suppression de compte, e-mail non demandé
- Histoire (décision du 2026-10-25, ligne directrice légère, pas un jeu à histoire), découpée en ACTES, un méchant par
  grosse mise à jour :
  - Acte 1 (sortie) : FONDER LE VILLAGE. Les gumori vivent dispersés, seuls, piégés dans de vieux bocaux (cause à choisir :
    tempête, marée, entrepôt abandonné). Le joueur (gardien, l'avatar) brise les bocaux : chaque niveau = un bocal (le
    réservoir), fusionner = plus forts ensemble, assez pour briser le verre, finale (division + envol) = la libération.
    Les libérés le suivent librement et fondent le village ; chaque zone finie y ajoute un bâtiment (maison, fontaine,
    pont…) jusqu'au CHÂTEAU en fin d'acte. Pas de vrai méchant : le défi = les bocaux, peut-être un gros gumori grognon à
    rallier en fin de zone. Gumori pas encore libérés = silhouettes GRISÉES (village, collection), qui prennent leur
    couleur quand on les libère
  - Acte 2 (grosse mise à jour) : la GRISAILLE (méchant Gris, boudeur plutôt que cruel, trouve les couleurs « trop »)
    VOLE les couleurs du village et des gumori du joueur : nouvelle carte, brume grise qui recule à chaque victoire, les
    TERNES à battre en fin de zone (gumori vidés de leur couleur, gris délavé, PAS le corps noir ; battus = recolorés, ils
    rejoignent le village), Gris à la fin (il trouve sa propre couleur et rejoint le village). Apparitions de Gris dans
    les niveaux seulement écrites dans les données du niveau (déterministe)
  - Actes suivants : un nouveau méchant / une nouvelle menace par grosse mise à jour
  Village = bocal-jouet : les gumori y vivent LIBRES et restent par choix (vocabulaire : « tes amis », « ton village »,
  jamais « possédés » : le joueur n'est pas un collectionneur)
- Bocal-jouet : écran d'accueil où la collection se laisse toucher, écraser, secouer (attachement, aucune contrainte)
- Deuxième piste de cœur de jeu, en réserve : « chute molle » (un slime tombe dans un puits ; tenu = mou, il se faufile ;
  relâché = dur, il rebondit et casse). Écartées : remplir un moule (incompréhensible sans démonstration), slime qui se
  déplace tout seul (perte de contrôle)
- Piste technique : physique de jeu SIMPLE par slime (cercle ou petit anneau de 8–12 points) + gélatine VISUELLE (ressort
  par slime calculé seulement à l'affichage, jamais dans la simulation). Le blob actuel (anneau de 32 capsules + joints) est
  trop lourd pour 30 slimes à l'écran : à mesurer (étape 1)

## Création et design des gumori (décision du 2026-10-24)
Vocabulaire : les personnages s'appellent des « gumori » (invariable : un gumori, des gumori). Ne plus utiliser les anciens
noms de designs (Goutte, Pousse…) ni parler de « Suika » pour les désigner.
- 10 gumori + 1 spécial (le dernier, libre : irisé, doré… ce qu'on veut). Plus tard, beaucoup de variantes (skins) par
  gumori, SANS jamais changer l'ordre des fusions : un joueur ne réapprend rien
- Ce qui dit le niveau : la TAILLE et surtout la COULEUR. Chaque niveau = une FAMILLE de couleur, pas une couleur exacte :
  une fourchette de teinte + une fourchette de luminosité (ex. bleu ≈ 205–235°, moyen-foncé). Un variant peut être bleu nuit,
  glacier, électrique… avec motifs et accessoires de toute couleur, mais la couleur de sa famille couvre la majorité du corps
  (≈ 70 %) et le contour reste
- Familles = couleurs que presque toutes les langues nomment (sortie mondiale : « un gumori bleu » partout)
- Deux niveaux qui se suivent (tailles proches, +20 à 27 %) doivent être OPPOSÉS : luminosité inversée (clair ↔ foncé,
  lisible pour les daltoniens, en niveaux de gris, en plein soleil) et si possible température (chaud ↔ froid). Les familles
  proches (rouge / rose / orange, bleu / violet / turquoise, crème / jaune) séparées par plusieurs niveaux : la taille tranche.
  Pas d'arc-en-ciel dans l'ordre (voisins trop semblables)
- Les petits niveaux sont les plus vus en NOMBRE mais les moins vus en TAILLE (détails peu lisibles) : aux tout premiers,
  les familles simples par nature ; les grandes classiques, riches en variantes (rouge, jaune, bleu, rose), juste après
  (assez grandes pour leurs détails, encore fréquentes) ; les plus grands = la vitrine des designs les plus sophistiqués
- Ordre retenu, validé sur la planche de test du 2026-10-24 (`Captures/bancs/palette.js` → `Captures/palette/`) :
  1 orange #FFA63D (clair, chaud) · 2 violet vif #A048DE (moyen) · 3 rose bonbon #FFA4D1 (clair) · 4 bleu #3F86EE (moyen,
  froid) · 5 jaune miel #FFD444 (clair, chaud) · 6 rouge #E0303F (foncé, chaud) · 7 crème rosée #FFEFF3 (très clair) ·
  8 chocolat au lait #7A4524 (foncé, chaud) · 9 vert pomme #8BD63A (clair) · 10 noir #2E2638 (très foncé) · 11 spécial
  (couleurs de référence ; 3 nuances par famille dans le script = première idée des fourchettes). Retouche du 2026-10-24
  demandée par l'utilisateur : bleu plus clair (avant #2456C8, trop sombre et profond), violet plus vif (avant #8A2DB8), rose
  plus bonbon (avant #FF8DC4) ; l'ordre tient sans changement
  Pourquoi : niveaux impairs clairs, pairs foncés. Couleurs VIVES aux tout premiers niveaux (les plus nombreux à l'écran) :
  chocolat / crème en premier, essayé, faisait « pas des couleurs » (peu saturées, lues comme neutres) ; un petit orange
  plutôt qu'un petit violet (choix de l'utilisateur). Classiques (rose, bleu, jaune, rouge) aux niveaux 3 à 6 ; crème,
  chocolat, noir aux grands niveaux, où leurs détails (chantilly, biscuit, réglisse…) se voient. Turquoise RETIRÉ (essayé en
  famille) : beaucoup le nomment « bleu clair », il faisait doublon ; le cyan n'est pas non plus une nuance de CORPS du bleu
  (trop clair, casse sa luminosité foncée) : seulement en accent / motif. Noir pris comme 10e famille (couleur de base,
  ressemble à rien d'autre ; le chocolat passe « au lait » pour s'en détacher). Les couleurs de base vraiment utilisables
  sont ≈ 9 à 10 : 10 familles + spécial, c'est le maximum.
  Mesuré (OKLab ΔE, pire nuance contre pire nuance, 5 modes : normal, deutéranopie, protanopie, tritanopie, gris) : niveaux
  voisins ≥ 0,10 partout (le plus juste : rose / bleu en protanopie et en gris, depuis le bleu plus clair). À 2 niveaux d'écart, faible pour violet / bleu et rouge / chocolat en daltonisme rouge-vert, et
  plusieurs paires en gris / tritanopie : la taille (+26 à 54 %) départage. Crème à surveiller sur fond clair
- Appliqué au jeu (2026-10-24) : `GumoriDesign.Levels()` (un design par niveau), mêmes couleurs dans `GumoriFusion.levelColors`,
  toutes les scènes. Purement visuel : aucun effet sur la simulation ni les replays

### Règles de création des skins (décision du 2026-10-24)
- TOUT est fait par le code : aucune texture, aucun sprite, aucun fichier image. Formes des accessoires = polygones calculés
  (`GumoriSkin`), motifs / visages / reflets = fonctions du shader `Slime/Body` (distances signées). Rien d'autre n'est permis
- Un gumori = une combinaison de CASES, un élément par case, chaque élément obtenable séparément (collection, futur avatar
  personnalisé). Données : `Cosmetics/GumoriDesign.cs` (enums des cases + catalogue). Cases actuelles :
  - Couleur du corps (« couleur du fond ») : famille + nuance (`body`, `shade`), toujours dans la fourchette de la famille
  - Motif (effets sur la gelée) : bulles, pois, ondes, alvéoles, rayures, magma, paillettes, cristal, galaxie, arc-en-ciel,
    anneaux lunaires, vermicelles, eau (reflets de vagues + bulles de verre), cactus (côtes + épines), filets de chocolat,
    fleurs de sakura ;
    couleur `accent`, `glow` = lueur
  - Reflet (par-dessus tout, façon carte brillante ; RÉSERVÉ aux ultra rares) : holo, étincelles, braises, pétales de sakura
  - Tête : pousse, pousse de fleur, antennes, oreilles de chat, oreilles d'ourson, cornes, nœud, cristaux, gerbe de cristaux,
    auréole, couronne, cerise, bandeau de ninja (bande peinte sur la gelée par le shader + nœud et pans en polygones)
  - Dos (derrière le corps, sur les côtés) : ailes de chauve-souris, ailes de fée, katana, deux katanas
  - Yeux : ronds, étoilés, lumineux ; Bouche : sourire, chat, grand sourire, en coin, croc, museau
    (d'ourson : ovale clair + truffe), masque de ninja (cache la bouche et les joues) ; Attitude au repos : calme,
    rêveur (yeux fermés), fier (paupières mi-closes), féroce (sourcils) ; Joues roses
  Les expressions de jeu (joie après fusion, surprise en chute, grimace au choc, clignements) passent par-dessus toutes les
  cases : même vie pour tous
- TOUT doit se combiner avec tout, sans bug : cases indépendantes (tête en haut, dos sur les côtés, motif atténué sous le
  visage), contraste automatique (encre du visage plus noire sur un corps moyen, claire sur un corps noir ; contour clair pour
  un corps noir, sinon invisible sur fond nuit). Tout nouvel élément est vérifié sur un corps clair ET un corps noir
- Nouveau thème de skin demandé : s'il apporte un élément réutilisable, l'AJOUTER comme élément d'une case (ou créer une
  case : aura, traînée, effet de fusion…), jamais un design figé d'un bloc. Valeurs d'enum toujours ajoutées à la FIN
  (enregistrées dans les scènes par leur numéro)
- DA commune : rond, mou, gélatineux, kawaii (grands yeux brillants, joues, petite bouche) ; les variations sont dans les
  cases, jamais dans cette base. Couleurs écrites en sRGB (#RRGGBB, comme une palette), converties par `GumoriDesign.Hex`
- Chaque gumori de base est un THÈME qui a un sens avec sa couleur (petit tigre orange, cristal violet, bonbon rose, eau
  bleue, ruche jaune, magma rouge, vanille, ourson en chocolat, cactus vert, nébuleuse noire), pas une couleur avec un
  motif posé au hasard. Un gumori trop simple (une couleur + un détail) est « boring » : lui donner un thème, sans
  sur-compliquer
- Paliers (décision du 2026-10-24) : COMMUN (dont la version de base, celle vue en jeu, + autres thèmes de la famille), RARE
  (même thème, un détail ou une couleur de détail en plus : ex. magma à lave bleue), ULTRA RARE = « shiny » (le thème de
  base en plus beau : effets de brillance, détails qu'il n'avait pas, couleur des détails libre, ex. lave dorée ; la couleur
  dominante et l'identité du thème restent). Les effets de brillance (reflets holo / étincelles / braises, galaxie très
  lumineuse, auréole, trait de brillance du cristal) sont RÉSERVÉS aux ultra rares ; la version de base n'en a pas.
  Spécial = le dernier niveau. Pas besoin de remplir toutes les cases. On ne supprime pas un design qui plaît : il devient
  une variante
- Écarté (2026-10-24) : shiny = recoloration automatique de chaque gumori + étincelles (jugé « vraiment nul »)
- Tirage en jeu des variantes rares / ultra rares : pas encore ; le jour où il existe, par une suite à graine SÉPARÉE de
  celle des lâchers (sinon la suite des niveaux change et les replays divergent)
- Versions de base (une par niveau) : Tigrou (orange, rayures + oreilles de chat), Cristal (violet, facettes + 3 cristaux),
  Bonbon (rose, paillettes + nœud), Bulle (bleu, eau), Abeille (jaune, alvéoles + antennes), Braise (rouge, magma : fissures
  courbes qui coulent + cornes, féroce), Vanille (crème ivoire, vermicelles + cerise rouge ; remplace Chantilly, crème rosée
  à cerise dorée, le 2026-10-27, préférée par l'utilisateur), Ourson (chocolat, filets de
  chocolat blanc + museau + oreilles), Cactus (vert, côtes + épines + fleur), Nébuleuse (noir, galaxie douce, rêveur), Roi
  (spécial). Variantes gardées le 2026-10-27 (les autres retirées à la demande de l'utilisateur) : rare Braise bleue (lave
  bleue) ; ultra rares Cristal étincelant (gerbe de 5 cristaux, holo), Fée (ailes de fée, yeux étoilés, holo), Braise d'or
  (lave dorée, yeux lumineux, braises), Nébuleuse étoilée (galaxie lumineuse, auréole, étincelles). Les éléments qu'aucun
  gumori ne porte plus (motifs bulles, pois, ondes, anneaux lunaires, sakura ; reflet pétales ; têtes pousse, bandeau ; dos
  chauve-souris et katanas ; bouches croc et masque) gardent leur dessin, rangés à part : menu « Inutilisés/… » des enums, en fin de chaîne dans `Slime/Body` et dans
  `GumoriSkin.EmitAccessories` (anciens designs retirés : Clémentine, Raisin, Lagon, Bourgeon, Ninjakura, Citron, Lune,
  Ninjakura en fleurs ; dans git avant le 2026-10-27)
- Vitrine (Scene view, hors Play) : un tableau, une colonne par famille (à la taille de son niveau) ; 1re rangée = versions
  de base, puis une rangée par palier (communs, rares, ultra rares). Captures par `unity command eval_file` :
  `Captures/bancs/shot_gallery.cs` (tableau) et `Captures/bancs/shot_zoom.cs` (gros plans)

## Roadmap (étape actuelle en gras)
1. Strict minimum (scène `Slime/reservoir`) : un réservoir (rectangle ouvert en haut), on lâche des blobs d'en haut
   (`GumoriDropper`), rien d'autre. But : voir comment ils s'empilent et combien on peut en mettre avant de ralentir.
   Fait (2026-10-21) : réservoir 8 × 14 m (courbe ouverte en U), `Lâcher` (flèches = viser, Espace = lâcher, tenu = rafale,
   R = vider), `Blob modèle (désactivé)` = réglages des blobs lâchés (défauts : 16 capsules, Ø 1 m, joints 48 Hz), gravité −15.
   Mesuré (PC, éditeur, blobs empilés au repos) : 10 blobs 0,24 ms par pas, 30 : 0,9, 60 : 2,3, 90 : 3,5, 120 : 5,1 ms (réservoir
   presque plein) ; budget 8,3 ms par pas à 120 pas/s, mobile 2 à 4 × plus lent → ≈ 40–60 blobs max sur mobile en l'état.
   Moins de la moitié est Box2D (120 blobs : 2,0 ms) : le reste = chaque blob relit TOUS les événements de contact du monde
   (`BLOB.RefreshContacts`, coût ∝ blobs × événements) → première optimisation évidente (faite le 2026-10-25, voir « Code du jeu »). Rejeu exact au bit près (d'un bloc
   et pas à pas). Corrigé au passage : tous les blobs avaient le même groupe de collision (−1) et se traversaient ; un groupe
   négatif par blob maintenant (`Gumori._groupCounter`)
   Fusion (2026-10-21, `GumoriFusion` sur `Lâcher`) : deux blobs de même taille qui se touchent → un blob ×2 en diamètre
   (`growth`), capsules doublées jusqu'à 32, couleur par niveau (dégradé de 10 teintes depuis le cyan du modèle : vert, jaune, orange, rouge, rose, violet),
   `maxLevel` = 10 par défaut. Taille LINÉAIRE depuis le 2026-10-21 (exponentielle avant, jugée trop rapide) : diamètre du
   niveau n = modèle × (1 + (`growth` − 1) × n) ; avec `growth` 1,37 : 1 ; 1,37 ; 1,74… ; 4,7 m au niveau 10 (tient dans le
   réservoir de 8 m ; en exponentiel, 1,25 donnait déjà 9,3 m). Depuis le 2026-10-25, au choix dans `GumoriFusion.growthMode` :
   Arithmétique (+, `growth`, défaut) ou Multiplication (×, `geometricGrowth` 1,168 = même 4,7 m au niveau 10). Historique de l'exponentiel (sans limite : blobs de 8 puis 16 m
   dans le réservoir de 8 m, éjectés à 200 m/s). Mitose à l'envers : nouvel anneau posé sur une cacahuète (union adoucie des
   deux cercles, col `waist`) TENUE par l'anneau (`SetShape` : angles de repos des joints = angles de la cacahuète − 2π/m,
   capsules allongées à son périmètre), puis ramenée au cercle en `mergeTime` selon `mergeCurve` ; capsules qui s'épaississent.
   Avant : laissée s'arrondir seule = quasi instantané, et avec `growth` < 2 les joints contractaient le périmètre d'un coup. Écarté : recoudre les capsules réelles des deux anneaux (un blob écrasé par sa chute se replie,
   l'anneau recousu se croise en 8 et y reste bloqué). Rejeu exact au bit près (d'un bloc et pas à pas, 33 fusions).
   Deux méthodes au choix depuis le 2026-10-25 (`GumoriFusion.style`, À TESTER à l'œil par l'utilisateur ; Cacahuète par défaut,
   scènes inchangées) : Cacahuète (ci-dessus) ou ABSORPTION : l'anneau neuf part de la forme RÉELLE des deux gumori (contour de
   l'union de leurs polygones de capsules, rayons depuis le milieu, bord le plus éloigné), jamais au-delà du premier voisin
   croisé, col arrondi (`absorbNeck`) dans la place libre seulement ; chaque capsule garde la vitesse de la capsule d'origine
   la plus proche (`inheritMotion`) ; s'arrondit en `absorbTime` selon `absorbCurve`, molle au départ (`absorbStiffness`).
   Pareils qui ne fusionnent pas (2026-10-28, signalé par l'utilisateur, « parfois, sans raison », pas reproduit) :
   contacts réels de Box2D comparés à chaque pas aux paires comptées par les événements (`Captures/bancs/touch_audit.cs`,
   bot Space, 4 parties) : jamais plus d'un pas de retard. Les seuls contacts finis sans fusion (≈ 1 sur 8) ont lieu
   pendant que l'un des deux finit sa fusion (attente voulue par l'utilisateur : fusion immédiate essayée puis REFUSÉE,
   « trop rapide ») ; tous refusionnent plus tard dans le bot. Attraction des deux pendant l'attente essayée : aucun
   effet mesurable, retirée. Trou corrigé par logique (jamais vu dans le bot) : deux pareils EMMÊLÉS sans contact (pointe
   passée entre deux capsules) étaient séparés par `GumoriUntangle` ; ils fusionnent maintenant (`RequestMerge`). Sonde :
   F9 en Play (éditeur) → `Captures/fusion_probe.txt` + `.png` (chaque paire de pareils à moins de 30 cm : écart, contacts
   Box2D, comptés, en fusion, groupes, réserve). Piège des bancs : `SpaceEndless` tire une graine au hasard à chaque Play,
   comparer deux versions du code dans la MÊME session de Play (ou fixer la graine).
   Réglages propres (durée effective lue par Escape / Goal / Fx via `MergeDuration`). Mesuré (`Captures/bancs/fusion_run.sh`,
   même partie scriptée) : chevauchement des voisins à la naissance, terrestre 17,3 → 3,0 cm en moyenne (> 5 cm : 28/32 →
   2/31), Space 7,1 → 1,5 cm (25/32 → 0/28), Space : sorties forcées 5 → 0, démêlages 30 → 10 ; même coût ; identique au bit
   près sur 2 essais. Premier essai sans la limite des voisins : un voisin logé dans le creux entre les deux (ou dans un creux
   d'un gumori mou) se retrouvait DEDANS (5 en Space)
   Habillage qui se chevauchait au début de l'absorption (signalé par l'utilisateur, 2026-10-25) : l'anneau se PLIAIT (une
   capsule repliée sur sa voisine, coude de 130–176°, 2 à 24 pas après la naissance ; mesuré : anneau des capsules qui se
   recoupe, ≈ 300–500 pas par partie Space contre 0 en cacahuète). Causes, dans l'ordre : (1) l'élan gardé incluait la vitesse
   d'APPROCHE des deux parents (gumori lancé : jusqu'à 7 m/s d'écart) et les capsules d'un même anneau ne se heurtent pas :
   la moitié rapide traversait l'autre → seul le mouvement PROPRE de chaque parent est gardé (vitesse moins la sienne), lissé
   le long de l'anneau, sans rotation par capsule ; (2) gelée trop molle au départ (`absorbStiffness` 0,35 sur des joints
   Space déjà mous) → défaut 1 (raideur normale du gumori : 0, 9, 0 pas croisés sur 3 graines ; 0,6–0,8 : 3 à 40). Aussi :
   capsules posées sur les cordes du contour, chacune à sa longueur (allongement par capsule, négatif permis : `Gumori.SetShape`
   accepte jusqu'à − ½ longueur), contour et angles visés lissés (`absorbSmoothing` passes). Essayé sans effet : fermeture /
   ouverture morphologique du contour (poussait les voisins), retourner les coudes à ± 2π (jamais arrivé), pression Space.
   Coût d'un pas de fusion : 1,0 ms en absorption, 2,7 ms en cacahuète (sa marche de rayons). Bancs : `fusion_variants.sh`
   (réglages essayés sans toucher la scène, ex. "1 seed=5 stiff=0.6"), `fusion_trace.cs` (SVG des anneaux), `fusion_trace2.cs`
   Absorption RECOUSUE (2026-10-27, signalé par l'utilisateur : bleu – violet – bleu dans la réserve, le bleu de droite lancé
   à 100 % vers la gauche, les deux bleus se touchent au-dessus du violet → le jaune renaissait minuscule DANS le violet).
   Cause : contour tracé en rayons depuis le milieu des deux centres, coupés au premier voisin croisé ; le milieu tombait
   dans le violet, tous les rayons butaient dessus (plancher 0,1 × rayon). Une forme en rayons depuis un point ne peut pas
   contourner un voisin. Maintenant (`GumoriFusion.BuildStitch`) : les deux contours réels (axes des capsules) sont OUVERTS
   à leur zone de contact (capsules à moins de 2,1 épaisseurs de l'autre anneau, `ContactReach`) et RECOUSUS par deux ponts
   courts : la gelée part de la forme exacte des deux, le voisin reste dehors dans le creux, puis l'anneau s'arrondit à
   partir du pont et pousse le voisin. Contour par rayons gardé en secours (contour recousu qui se recoupe : jamais vu).
   Zone de contact : recousus en un seul point, deux gumori plaqués sur toute une face gardaient une fente dont les bords
   se croisaient (pas à anneau croisé, 4 parties : 52) ; zone large (3 épaisseurs), le pont coupe le creux à côté du
   contact, là où se trouve justement un voisin (chevauchement à la naissance jusqu'à 17 cm) ; 2,1 : 8 pas croisés (un
   gumori écrasé dans un coin, pas dû au pont), chevauchement 1–8 cm. Ancienne absorption, mêmes parties (graines 2, 5, 7) :
   chevauchement moyen 11 / 3,2 / 3,7 cm, pire 27 cm. Cas signalé : né à 2,8–2,9 m² (deux bleus) au lieu de 0,02–0,07,
   violet jamais dedans, à 5 inclinaisons de tir. Niveau à bocaux (bot) : gagné en 15 tirs, identique au bit près
   (2 essais). Bancs : `fusion_bvb.cs` (cas signalé, "attente pas_suivis inclinaison"), `fusion_bvb_shot.sh` (images →
   `Captures/fusion/bvb_*.png`), `fusion_compare.cs` (chevauchement par naissance, pas croisés). Les anciennes parties ne
   se rejouent plus pareil (sans importance, voir Déterminisme)
   Habillage d'une forme en U : voir « Gelée élastique » (étape 4, `GumoriSkin`)
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
   Suite, aperçu, points (2026-10-21) : niveaux définis à un seul endroit (`GumoriFusion.LevelDiameter / LevelSides /
   LevelColor / LevelStiffness`, diamètre calculé pareil pour lâchés et fusionnés : égal au bit près,
   c'est ce qui reconnaît deux slimes de même niveau). `GumoriDropper` : suite aléatoire à graine fixe (`seed`, xorshift32 en
   entiers, tirée au départ et à chaque lâcher dans `LevelTick` ; pas `UnityEngine.Random`), niveaux 0 à `maxDropLevel` (3),
   `smallBias` pour favoriser les petits ; slime à lâcher dessiné au point de lâcher + « Suivant » à côté ; zone de lâcher
   selon le rayon (2026-10-22 : paroi intérieure mesurée sur la courbe `reservoir` (axe − ½ épaisseur, moitié haute ;
   suit la box quand on la redimensionne ; `halfWidth` à la main seulement sans courbe) ; tout au bord, un
   slime de toute taille longe la paroi à `wallGap` 0,15 m sans la toucher, mesuré ; avant, son bord entrait dans le verre).
   `dropInterval` vaut aussi pour les appuis répétés, plus seulement pour la touche tenue. H = formes physiques (capsules)
   dessinées par-dessus l'habillage (`showHitboxes`). Objectif (`GumoriGoal`, 2026-10-22, case `useGoal`, appliquée au
   prochain R) : `maxDrops` coups (30) et `targetScore` points (3 000, à caler sur de vraies parties ; une partie scriptée
   bête en fait 4 790) qui vont ensemble. Score atteint = plus de lâcher, la chaîne en cours se FINIT (ses points comptent :
   plus de fusion pendant `comboWindow` + `mergeTime`), puis réussi : la simulation retire les slimes et `GumoriFinale`
   (Cosmetics, purement visuel, slimes virtuels dessinés par `GumoriSkin.DrawVirtual`) les fait se diviser en cascade
   jusqu'au plus petit (éclat + son à chaque division, confettis, fanfare) ; les plus petits s'envolent par le haut ; panneau
   après `celebrationTime` (3,5 s). Coups épuisés sans objectif : jugé quand rien n'a fusionné depuis `settleTime` (2,5 s),
   « Raté… ». R = rejouer. Identique sur 2 essais (score, fusions, fin)
   Objectif calculé (2026-10-22, `autoTarget`) : base = points SANS AUCUNE CHAÎNE en fusionnant parfaitement la suite à
   graine (`GumoriDropper.PredictSequence`, vérifiée identique au jeu ; paires de même niveau fusionnées comme une retenue
   binaire : `GumoriGoal.PerfectPoints`) avec tous les coups, divisés par (1 + marge) ; marge : Facile 33 %, Moyen 16,5 %,
   Difficile 5 % ; `comboShare` = part de l'objectif à faire en chaînes (0 = chaînes en bonus). Compté en POINTS : en coups
   (points parfaits sur `maxDrops` / (1 + marge) coups), les paliers (un coup de plus ne complète pas toujours une fusion)
   donnaient le même objectif à deux difficultés (graine 2, 30 coups : Moyen = Difficile = 2 340) ; en points : 1 805 /
   2 060 / 2 286. L'inspecteur affiche coups nécessaires / donnés et la difficulté réelle d'un objectif saisi à la main.
   « Parfait » = aucune fusion ratée : en vrai, on en rate
   (slimes coincés loin de leur pareil), donc même Facile peut être dur ; à caler sur de vraies parties.
   Parois = limites (`GumoriWallHold`, 2026-10-22) : un slime lâché au bord s'écrasait contre la paroi et partait en roulant
   jusqu'à l'autre côté (mesuré : niveau 4, de x = 2,68 à −0,67 en 3 s, toujours lancé). Tant qu'il touche une paroi (face
   intérieure = `halfWidth`, la collision y est aussi), la vitesse de son centre qui l'en éloigne est annulée
   (`maxAwaySpeed` 0), même correction pour toutes ses capsules : l'écrasement et le rebond restent. Marge de contact
   0,08 m (`contactMargin`) : à 0,03, un slime lâché pile contre la paroi gauche, repoussé en s'écrasant au sol, sortait de la
   marge en un pas et repartait en roulant de 2 m (mesuré, 2026-10-22) ; à 0,08 il reste collé, niveaux 0, 3 et 6, des deux
   côtés. Marge + chemin d'un pas (vitesse × durée du pas) : tient aussi à 50 et 60 Hz ; tri grossier à 2 diamètres (à 1, un slime
   écrasé contre la paroi gauche était écarté). Mesuré : reste à
   x = 2,70 ; partie scriptée identique au bit près (2 essais)
   `GumoriScore` : fusion vers le niveau n = `pointsPerMerge` ×
   n(n+1)/2, fusions à moins de `comboWindow` (1,2 s, en pas de simulation) l'une de l'autre = chaîne, + `comboPercent` (50 %)
   par maillon ; points qui s'envolent (OnGUI, pas pendant le rejeu accéléré), score / meilleur / plus gros niveau. Vérifié :
   partie scriptée de 3600 pas (22 fusions, chaîne ×6, 2 735 points) identique au bit près, score compris, sur 3 essais
   (2 d'un bloc, 1 pas à pas) ; 4000 tirages ≈ 25 % par niveau
   Slimes enfermés (`GumoriEscape`, 2026-10-21) : un slime qui fusionne démarre sur une cacahuète tracée sur des cercles
   idéaux, plus gros que des slimes écrasés dans la pile ; il peut chevaucher la membrane d'un voisin et Box2D le pousse
   DEDANS (reproduit : deux niveau 1 contre un niveau 5 → le niveau 2 né à l'intérieur, et il y restait même après que le
   gros a refusionné). Détection pendant `mergeTime` + `watchTime` après chaque fusion (centre du petit dans le polygone des
   capsules du gros, tri grossier par une capsule) ; sortie : le petit prend le groupe de collision du gros
   (`Gumori.SetCollisionGroup`, il traverse sa membrane et touche tout le reste), poussé à `escapeSpeed` vers le bord le plus
   PROCHE du gros (jamais plus bas que `maxDownAngle` 30° sous l'horizontale), groupe rendu dès qu'il est dégagé, et jamais
   tant qu'il chevauche encore la membrane (au plus 3 × `maxEscapeTime`). Avant (2026-10-25, signalé par l'utilisateur) :
   sortie vers l'extérieur ET le haut, le petit traversait tout le gros et remontait à la surface de la pile (mesuré en
   Space, graine 2 : +3,5 et +2,6 m), et la membrane était rendue à cheval au bout de 3 s (Box2D les séparait d'un coup).
   Après : même partie, −0,2 m ; pire cas sur 4 graines, +1,25 m (petit enfermé dans le haut d'un gros, sorti par le haut).
   Banc : `Captures/bancs/space_fly.cs`. Vérifié : partie en rafale de 3600 pas (65 fusions, 1 sortie), plus aucun slime
   enfermé, identique au bit près sur 3 essais
2. Choisir la représentation des slimes selon les mesures (blob actuel allégé / anneau simple / cercle + gélatine visuelle)
3. Fusion des slimes de même couleur (faite en version brute, voir étape 1), ligne de débordement, score : premier vrai prototype jouable
4. Sensation (gélatine, sons, effets), puis idées retenues une par une. Commencé (2026-10-22), objet « Habillage » de
   `Slime/reservoir` :
   - `GumoriSkin` : un design par niveau (11 ; depuis le 2026-10-24, découpés en cases combinables et dans l'ordre des
     familles de couleur : voir « Création et design des gumori » ; mêmes couleurs dans `GumoriFusion.levelColors`).
     La gelée suit l'anneau physique (contour lissé entre les capsules) ; motif et visage
     dans un repère qui se déforme avec elle et TOURNE avec l'anneau (2026-10-28, demandé par l'utilisateur ; avant :
     visage remis à l'endroit en glissant sur le corps, `uprightTime` retiré). Vivant : clignements,
     regard vers le slime à lâcher ou une fusion voisine, bouche ouverte en chute, grimace au choc, sourire après fusion,
     éclair et accessoires qui poussent à l'éclat de la fusion. Yeux agrandis (2026-10-22). Vitrine des designs dans la
     Scene view. Rebord translucide (2026-10-23, façon Slime Rancher) : pourtour de gelée plus claire de la couleur du slime,
     où l'on devine le fond, épaisseur en fraction du rayon (`rimWidth` 0,074, `rimOpacity` 0,538, `rimLighten` 0,35,
     réglés par l'utilisateur, mêmes valeurs dans toutes les scènes ; `_Rim` du shader, `rimWidth` 0 = sans).
     Gelée = éventail de triangles depuis le centre moyen (le visage est dessiné autour de lui, repère 0 du motif).
     GELÉE ÉLASTIQUE (2026-10-27, signalé par l'utilisateur : gumori plié autour du moulin, plis de papier, yeux et motif
     coupés ou disparus ; « une balle qu'on étire : la texture s'étire et se tasse, elle ne disparaît pas »). Un éventail
     depuis UN point ne peut pas couvrir une forme en V / U sans que ses triangles se croisent. Pour un gumori vraiment plié
     (plus petit triangle de l'éventail sur l'anneau des capsules < 0,25 × le triangle moyen, retour à l'éventail au-dessus de
     0,4), la gelée devient un disque à 3 anneaux intérieurs qui suit le contour comme une feuille élastique
     (`GumoriSkin.SolveJelly`) : chaque triangle cherche sa forme au repos (tournée ; échelle = moyenne géométrique de la
     sienne et de celle du gumori : il peut se tasser dans un bras mince), sommets intérieurs = moyenne de ce que demandent
     leurs triangles, 3 passes par image reprises de l'image d'avant (12 au départ d'un pli), calcul sauté quand le contour
     n'a pas bougé et que la gelée est au repos ; plié de plus de ≈ 10° quelque part, aucun sommet ne sort de la peau
     (ramené à 3 % du rayon à l'intérieur). Calculé sur l'anneau des capsules (16–32 points), lissé à la résolution du
     contour (Catmull-Rom le long des anneaux) ; fondu éventail ↔ gelée élastique en 0,12 s (temps réel). Rond = même dessin.
     Mesuré (bot du niveau à bocaux, plis les plus forts, `Captures/bancs/fold_shot.sh` + `jelly_check.cs`) : peau hors du
     corps 0,2 / 5,5 / 4,4 % (feuille rigide sans ces deux règles : 34 / 11 / 10 %), triangles retournés 6–10 % (tassés le
     long du creux, invisibles) ; plus de plis de papier, yeux tassés au lieu de coupés (`Captures/fusion/gelee_elastique.png`).
     Coût en partie réelle (`skin_motion_ref.cs`, 1 800 images, 12 gumori) : habillage 0,30 → 0,45 ms par image, pire 1,3 ms
     (essais : feuille pour tous 1,5 ms ; test « dans la peau » à chaque passe 3,4 ms). Remplace `FanCenter` (centre poussé
     dans le corps, réglages `fan*` retirés ; bancs `fan_center.cs` / `fan_jitter*.cs` ne compilent plus) et le découpage en
     oreilles du U (facettes, visage écrasé)
     GELÉE UNIQUE (2026-10-28, remplace l'éventail ET la gelée à seuil ci-dessus ; signalé par l'utilisateur : choc contre
     une paroi, scène `space endless 2`, visage coupé, sur le côté, disparu, morceau de texture en retard ; « un seul
     système, simple, robuste, sans retard ») : TOUS les gumori, à chaque image, même maillage (centre + 3 anneaux à la
     résolution des capsules + contour) collé à l'anneau (point k du contour = capsule k). Intérieur par ARAP local / global :
     rotation (et échelle, `JellyFreedom` 0,5 = moyenne géométrique avec celle du gumori) de chaque triangle, puis système
     linéaire de l'intérieur (Cholesky en bande factorisé une fois par nombre de capsules) ; départ = image d'avant déplacée
     comme le contour (translation, rotation, échelle), jusqu'à 5 itérations (20 la première fois), arrêt quand plus rien ne
     bouge de 0,25 px ; gumori au repos : sauté. Gumori virtuels : même disque, rond. Mesuré (`Captures/bancs/jelly_watch.sh`,
     1 500 images, un tir à pleine vitesse toutes les 0,5 s, `Assets/Temp/JellyWatch.cs` ; film d'un tir contre la paroi :
     `jelly_shot.cs` + `Assets/Temp/JellyRec.cs` → `Captures/gelee/planche_*.png`) : visage toujours là (écrasé contre la
     paroi, plié avec la banane) ; triangles retournés : pire 5–11 % de l'aire, seulement dans des gumori écrasés en
     lamelle ou des naissances en haltère (ancienne gelée : 37 %) ; gelée 0,07–0,10 ms par image en moyenne, p95 0,5 ;
     habillage complet 0,72 ms contre 0,56 avant (PC). Essayé : liberté 1 (conformal) : moins de plis mais le visage glisse
     dans le creux d'un gumori en C ; 0,75 : entre les deux. Marqueurs Profiler `GumoriSkin.Build` / `GumoriSkin.Jelly`
   - `GumoriFx` : fusion en deux temps (l'explosion cachait la mitose, 2026-10-22) : pendant la fusion, seulement une lueur
     douce DERRIÈRE ; à `popAt` (80 % de `mergeTime`) l'éclat : halo et onde derrière les slimes (file 2990), gouttes et
     étincelles depuis sa surface, cloche et son de chaîne au même moment ; points affichés au-dessus du nouveau slime.
     Plus fort avec le niveau et la chaîne :
     tremblement de l'écran, pulsation du fond, annonce « ×3 Super ! » (dès 2 maillons), ralenti (dès 4 maillons ou niveau 9),
     éclair plein écran, confettis (record dès le niveau 5, niveau max). Éclaboussures aux chocs, pointillés de visée
   - Fondu de design à la fusion (2026-10-25, `GumoriSkin.fadeDesign`, coupé par défaut, À TESTER à l'œil) : le né passe en
     fondu enchaîné du design de ses parents au sien sur `MergeDuration` (`fadeCurve`) : nouveau design dessous, ancien
     par-dessus qui s'efface (opacité = alpha des sommets, multiplié dans `Slime/Body`), accessoires qui se fondent aussi
     (pas de pousse à l'éclat). Banc : `Captures/bancs/fade_shot.sh` (ralenti, `Captures/fusion/fondu_*.png`)
   - Bandeau des stades (`GumoriLevelStrip`, 2026-10-28, demandé par l'utilisateur, scènes Space du jeu libre) : tous les
     niveaux côte à côte à leurs tailles relatives, designs en jeu (plus tard : ceux choisis par le joueur) ; sous la boîte
     (ou au-dessus, `above`) si l'écran a la place, sinon en bas de l'écran ; largeur bornée par la boîte. Réglages à
     juger à l'œil (2026-10-28) : `sizeContrast` (1 vraies tailles, 0 toutes pareilles, ≈ 0,3 légère différence),
     `align` (Centre / Sol / Plafond), `gap` ; aperçu HORS Play (`showInEditor`, Scene et Game view, via
     `GumoriSkin.DrawEditorExtras`). Banc : `Captures/bancs/strip_preview.cs` (portrait, réglages dans `strip_preview.txt`).
     Liens `GumoriDropper.reservoir` et `GumoriLauncher.box` perdus à la réparation des scènes, reliés à « Réservoir »
     (`Captures/bancs/relink_box.cs`). Mode Space
     (`GumoriLauncher` présent) : `GumoriDropper` ne dessine plus son point de lâcher (gizmo, aperçu, « Suivant ») ;
     le gizmo du Launcher montre la ligne de la réserve et les points d'arrivée réels (bords, par taille).
     PROBLÈME repéré : caméra à taille fixe (orthographique 9) : en portrait de téléphone (1080 × 2340), la boîte de 9 m
     déborde des deux côtés et il ne reste pas de place pour le bandeau sous la boîte (`Captures/bancs/shot_portrait.cs`)
   - `GumoriBackdrop` : fond dégradé à bulles floues, bocal de verre sur la courbe du réservoir (paroi tracée sur les bords
     `left` / `right` de la courbe, avec onglet : même épaisseur partout ; avant, normale moyenne sans onglet, coins et côtés
     sans point intermédiaire amincis à ≈ 60 %) ; `GumoriSound` : voir Son (refait le 2026-10-24 : DA grave et apaisante, mouvement discret lu à l'écran)
   - Mesuré (PC) : habillage 0,53 ms par image pour 15 slimes + aperçus (contour adaptatif : ≈ 6 px par côté, 72 points max ;
     1,6 ms avant optimisation). Partie réelle avec tous les effets (ralenti compris) puis rejeu de ses entrées : identiques au
     bit près (5 875 pas, 17 fusions, même score)
5. **Carte du monde, Aventure / Sans fin** (commencé le 2026-10-22, scène `Slime/carte`, première des Build Settings) :
   - Écran titre sur la carte : « Aventure » (la carte) ou « Sans fin » (réservoir sans objectif, graine tirée au hasard à
     chaque nouvelle partie et affichée, record gardé ; la partie reste rejouable au bit près avec sa graine).
   - Campagne = asset `Settings/Campagne` (`GumoriCampaign`) : zones de niveaux réglés UN PAR UN à la main (graine, coups,
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
   - Space Endless (essai du 2026-10-23, scène À PART `Slime/space endless`, copie du réservoir ; réservoir et carte
     intouchés) : même jeu, slimes bien plus mous qui comblent l'espace (idée vue dans « sobosuba », Steam). Réglages dans
     la scène seulement : gravité −4, joints du modèle 12 Hz (flexion) / 20 Hz (étirement), amortissement 1,5, fond nuit.
     6 Hz de flexion : trop mou (le gros slime devient concave et enveloppe ses voisins). `SpaceEndless` : sans objectif,
     graine au hasard (N = nouvelle partie), record à part. Pas encore relié à l'écran titre.
     Gameplay de sobosuba (2026-10-23, bande-annonce regardée image par image) : boîte FERMÉE carrée 9 × 9 m à fond en arc
     de cercle (flèche 2,5 m, rayon 5,3 m). Pas de lâcher mais un LANCER (`GumoriLauncher`) : RÉSERVE = rangée qui FLOTTE
     sous le plafond (gravité annulée, ressort vers une ligne à `rackDepth` 1,8 m sous le plafond : place au-dessus pour
     faire rebondir un tir sur le plafond), chaque slime attiré vers le MILIEU ; les nouveaux apparaissent au bord, à gauche
     puis à droite à tour de rôle, et filent s'y bousculer avec leur élan (2026-10-23 ; avant : plaqués au plafond et tirés
     vers la paroi gauche, jugé absurde). On en attrape un, flèche slime → doigt (puissance en %),
     relâcher = tir, vitesse ∝ longueur (max 16 km/h à 5 m). Chaque départ (tir, ou slime de la réserve mangé par une
     fusion) fait arriver UN slime ; le slime né d'une fusion avec la réserve y reste : lancer un slime sur son pareil de
     la réserve la fait passer à 4 (préparer le terrain) ; max `maxRack` 4 (sans plafond, elle grandissait sans fin en
     mangeant la pile). Lâcher de `GumoriDropper` neutralisé dans la scène (touches inutilisées, aperçus coupés), il garde
     la liste des slimes, leur pas et la fusion ; grabber souris de debug coupé dans la scène (il tirait UNE capsule :
     slimes qui se retournent). `GumoriFoam` : pression qui tient la SURFACE de chaque slime (ressort amorti sur l'aire du
     polygone des capsules, 8 Hz, cible × 1,25) + flexion très molle (1,5 Hz, étirement 10 Hz) = ils s'aplatissent les
     uns contre les autres et comblent l'espace ; freine aussi leur rotation (3/s). Gonflé à 1,4 : plus ronds, et un slime
     restait coincé au plafond par frottement ; frottement 0,4. Coût ≈ 0,5–0,8 ms par pas (PC). Même suite de tirs = même
     partie au bit près (vérifié, partie de 80 s, 2 essais) ; MAIS les tirs (analogiques) ne passent pas par `LevelInput` :
     `FastForward` ne les rejoue pas (liste `Shots` gardée). Plus tard : tourner la boîte, formes de niveaux à remplir.
     Slimes emmêlés (`GumoriUntangle`, 2026-10-23) : très mous (joints 10 Hz, amortissement 0,5, 32 capsules) et gonflés, leur
     anneau s'étire, des trous s'ouvrent entre capsules et la pointe d'un voisin passe DEDANS, puis y reste (l'anneau la
     retient de l'intérieur ; GumoriEscape ne surveille que les naissances de fusion). Détection tous les 2 pas (capsule d'un
     slime dans le polygone des capsules d'un autre) → le moins enfoncé prend le groupe de l'autre, écartés à 5 km/h,
     groupe rendu dès que dégagé (max 1,5 s). Mesuré, même partie de 1200 pas : sans, 2,3 paires emmêlées en moyenne (4 à
     la fin) ; avec, 0 (11 démêlages). Identique au bit près (2 essais). Coût total ≈ 1,1–1,2 ms par pas (PC) avec 32
     capsules par slime : à surveiller pour le mobile.
     Vocabulaire (2026-10-23) : jeu « TERRESTRE » = le réservoir normal (`Slime/reservoir`), jeu « SPACE » = Space Endless.
     Les correctifs du terrestre n'ont pas forcément lieu d'être en space : `GumoriWallHold` (parois qui retiennent) COUPÉ
     dans la scène space (interaction bizarre au bord, les slimes doivent pouvoir s'en écarter). `GumoriFoam` : cible d'aire
     toujours positive (avant, un slime retourné était TENU à l'envers). Dépliage (`foldAngle`, 2026-10-23) RETIRÉ le 2026-10-27 (coupé dans les scènes par
     l'utilisateur, rendu inutile par `selfCollision`).
     Slimes qui sautaient après une fusion (2026-10-23) : mesuré merge par merge (montée réelle d'un pas à l'autre), 5 sauts
     > 2,5 m/s vers le haut par partie de 1200 pas, dus surtout à `GumoriUntangle` : il rendait les groupes de collision dès
     qu'aucun CENTRE de capsule n'était dans l'autre, capsules encore à cheval → Box2D les séparait d'un coup. Rendus
     maintenant seulement une fois dégagés de l'épaisseur des capsules : 1 saut restant. `GumoriEscape` de la scène space
     adouci (1,2 m/s au lieu de 3, vers l'extérieur sans pente vers le haut ; réglages terrestres copiés). `GumoriSoftMerge`
     (vitesse du né et de ses voisins limitée à 4 km/h pendant 0,8 s) : effet non mesurable, RETIRÉ le 2026-10-27. Reste : un tout petit né pincé entre deux gros gonflés peut jaillir comme un pépin (pression).
     « Coup d'énergie » après fusion (suite, 2026-10-23) : à sa naissance, le slime fusionné occupe 1,4 à 1,9 × sa surface
     finale (tracé sur ses deux parents) et doit rétrécir de 30–50 % ; sans pression pendant la mitose, la foule l'écrasait,
     puis la pression revenait d'un coup (mesuré : un niveau 4 lancé à 3,5 m/s pile à la fin de sa mitose). `GumoriFoam` :
     pression dès la naissance, surface cible en rampe (lissée) de sa surface de naissance à sa cible en `growTime` 1,2 s (RETIRÉ le 2026-10-27 : coupé dans les scènes par l'utilisateur, inutile avec l'absorption recousue).
     Banc propre sans tir (lit de 14 slimes, 6 paires posées vitesse nulle, 3 dispositions) : coup moyen 9–21 → 3–5,
     pics 29–49 → 12–18 ; à 2 s pire (reste trop gros trop longtemps, coincé). Identique au bit près (2 essais).
     Tester les réglages du monde par eval : `var w = PhysicsWorld.defaultWorld; w.contactSpeed = …` (affecter directement
     `PhysicsWorld.defaultWorld.x` ne compile pas, et l'échec passe inaperçu dans une suite de commandes)
   - Premier niveau Space (2026-10-26, scène À PART `Slime/space niveau 1`, copie de `space endless 1`, graine 2, À TESTER
     par l'utilisateur) : « Les bocaux ». Objectif `SpaceLevel` : libérer les 3 bocaux en 15 tirs (étoiles selon les tirs
     restants : ≥ 15 % / ≥ 30 %), consigne au départ (cachée au premier tir), HUD « 1 / 3 bocaux » + tirs restants, panneau
     de fin (étoiles, Rejouer), fête de `GumoriFinale`. Réglages de la scène : stats du lâcher masquées.
     - BOCAL (`GumoriJars`) : gumori enfermé dans une bulle de verre statique (cercle Box2D, rayon = gumori + 0,16 m) ; une
       fusion dont une capsule du né passe à moins de `reach` 0,45 m du verre le fêle, au bout de `hits` fêlures il se brise
       et libère son gumori (un vrai, au pas suivant). Une même fusion peut fêler deux bocaux. Dessin : le gumori enfermé
       (même design, bouche ronde d'appel, regarde la réserve, flotte), verre à couvercle, fêlures, tremblement, éclats
       + onde + « bloup » au bris. Placement : en BAS (gravité faible, la pile reste basse : un bocal en l'air à 7,9 m
       n'a jamais été atteint)
     - MOULIN (`GumoriMill`) : pales en sucre d'orge (barre de 2 × 1,7 m) sur une charnière motorisée, 25 °/s, couple
       limité (cale sous une pile trop lourde au lieu d'écraser) ; option d'inversion de sens en pas. Placé au-dessus du
       bocal du milieu, il brasse et pousse les gumori dessus. Bout des pales < 2 cm par pas
     Mesuré (bot `Captures/bancs/level_bot.cs`, vise un pareil près d'un bocal ; captures `level_shots.sh` →
     `Captures/niveau/`) : gagné en 10 et 12 tirs, raté (2 / 3) à un autre rythme ; moulin jamais calé ; 0,3–0,45 ms par
     pas (PC) ; identique au bit près (2 essais). Réservoir terrestre : hash inchangé. Pas encore relié à la carte.
     Pistes suivantes (une par niveau, lisibles sur téléphone) : bascule, trappe qui s'ouvre au bout de N fusions,
     ingrédient à faire descendre jusqu'à une sortie, Ternes (gris, ne fusionnent pas, recolorés par une fusion voisine),
     boîte qui tourne
6. Avatar à créer : le blob du joueur sur la carte, mélange de tous les accessoires et shaders possibles des slimes
   (en attendant : le Roi)

Garder cette section à jour quand une étape avance ou que la vision du jeu se précise.

## Technique
- Unity 6000.6, physique 2D bas niveau `Unity.U2D.Physics` (Box2D v3 : PhysicsWorld,
  PhysicsBody, PhysicsShape, PhysicsChain, joints)
- PAS de Physics2D / Rigidbody2D / Collider2D classiques
- Un seul monde : `PhysicsWorld.defaultWorld` (pas fixe : 60 Hz, `fixedDeltaTime` 1/60, 4 sous-pas ; choix de l'utilisateur du
  2026-10-22 pour alléger, avant 120 Hz × 3. Mesures plus anciennes de ce fichier faites à 120 Hz)
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
  - Collision continue (`WorldSettings.continuousAllowed`) : à grande vitesse, un corps peut être arrêté pendant le pas sans
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

## Code du jeu
Nommage (décision du 2026-10-25) : tout ce qui est propre au jeu s'appelle `Gumori…` (corps `Gumori`, `GumoriDropper`,
`GumoriFusion`…). Gardés tels quels pour ne rien perdre : les clés PlayerPrefs (`slime.*`), les noms des shaders (`Slime/Body`…),
les noms des scènes (`Assets/Scenes/Slime/`) et des objets des scènes (« Blob modèle (désactivé) »…).
Audit du 2026-10-25 : code des anciens jeux SUPPRIMÉ (skate, flippers, bumpers, grappin, vitres, piques, caméra, mode
entraînement, leurs éditeurs et scènes ; restent dans les tags `avant-slime` / `blob-et-debut-skate`, avec le CLAUDE.md de
l'époque et ses mesures). L'ancien `BLOB` est devenu `Gumori`, réduit à ce que le jeu utilise : lest (touche ↓, qui triplait
la gravité de tous les gumori), obstacles, casse, comptage de contacts par capsule retirés. Ce comptage relisait TOUS les
événements de contact du monde pour CHAQUE gumori à chaque pas : sans lui, mêmes parties identiques au bit près
(`Captures/bancs/audit_ref.cs`, terrestre et Space), 0,35 → 0,21 ms par pas en terrestre, 2,7 → 1,0 ms en Space (PC).
- `Scripts/Core/` : moteur. `LevelSimulation` (seul moteur pas à pas : entrées → `ILevelTick` dans l'ordre de la hiérarchie →
  `PhysicsWorld.Simulate`, rejeu accéléré `FastForward`), `LevelInput` (entrées = un bit par `GameKey`, enregistrées ; lit le
  clavier), `LevelRestart` (R : détruit et recrée toute la physique `ILevelPhysics`), `WorldSettings` (seul endroit qui règle
  le monde : gravité, sous-pas, `simulationWorkers` = 1), `GizmoPickArea`, `SpeedKmhAttribute`, `LowLevelRaycastGrabber`
  (souris de debug : NON déterministe)
- `Scripts/Level/` : courbes `CurvePath2D` + `CurveCollider2D` (chaînes Box2D : parois du réservoir, boîte Space).
  Briques des mécaniques de niveau (2026-10-27) : `JarShape2D` (bocal : génère la courbe d'un bocal ouvert, cotes
  INTÉRIEURES : rayon du ventre, hauteur, arrondi du fond, largeur / hauteur du goulot, épaule, évasement, épaisseur ;
  collision en contour = paroi solide dedans ET dehors ; poignées dans la Scene view ; menu GameObject > 2D Object > Bocal ;
  contour debug en jeu en attendant son habillage). `MovingPath` (+ `IPathRider`, `PathShapes`, éditeur), repris du tag
  `avant-slime` : les enfants d'une trajectoire la suivent (vitesse, aller-retour / téléport / boucle, espacement, formes
  toutes faites) ; un `CurveCollider2D` enfant d'une trajectoire devient un corps kinematic piloté en vitesse (statique
  sinon, inchangé). Piège : Box2D refuse SANS PRÉVENIR toute une chaîne dont deux points sont à moins de ≈ 5 mm (le bocal
  en avait à 0,3 mm à l'épaule : les gumori le traversaient) ; `CurveCollider2D` retire les points à moins de 1 cm (aucun
  dans les scènes existantes : inchangées). Contour (Outline) d'une courbe ouverte : tracée dans le sens anti-horaire,
  `flipSide` décoché. Paroi PLEINE (`CurveCollider2D.solidWall`, cochée par `JarShape2D`, 2026-10-27, signalé par
  l'utilisateur : deux pareils qui se touchent par le goulot, le né s'arrondissait À TRAVERS le verre et restait à cheval) :
  capsules le long de la ligne médiane au lieu de chaînes (une chaîne ne retient que de sa face, une capsule passée derrière
  traverse). Mesuré (`Captures/bancs/jar_neck.cs`, un gumori au fond, son pareil poussé dans le goulot, 5 inclinaisons) :
  niveau 2, pas où l'anneau croise le verre 240/240 → 0/240 ; niveau 1 : 0 dans les deux cas. Bocal à remplir (petits gumori qui passent le goulot, fusionnent et épousent le verre, bris) : À FAIRE
- `Scripts/Gumori/` : le jeu.
  - `Gumori` : corps d'un gumori, anneau de capsules reliées par des joints fixes (précontraints : ils visent un angle relatif
    nul, seules les différences d'angle comptent). Limite d'étirement (`limitStretch`, 2026-10-26, coupée par défaut, À
    TESTER par l'utilisateur) : joint de distance sans ressort entre les bouts de deux capsules voisines, butée à
    `stretchLimit` × 2 × rayon des capsules (1 = cercles des bouts tangents, aucun trou). Mesuré (`Captures/bancs/stretch_one.cs`,
    bot du niveau à bocaux, ressort linéaire × 0,15) : paires ouvertes au-delà de 1,02 : 13,6 % → 0,16 %, pire 11,6 → 1,6 ;
    elle cède encore quand un gros gumori force en coin entre deux capsules d'un petit (butée résolue avec la fermeté des
    CONTACTS du monde, le `tuningFrequency` des joints n'y change rien) ; ≈ +50 % de coût par pas.
    Contact entre ses propres capsules (`selfCollision`, 2026-10-27, demandé par l'utilisateur : un gumori écrasé doit
    rester un anneau à deux parois côte à côte, pas une pile de capsules retournée sur elle-même ; ACTIVÉ par défaut, À
    VALIDER à l'œil) : un cercle « cœur » au milieu de chaque capsule (rayon `selfThickness` × rayon des capsules, plafonné
    à 45 % de l'écart entre deux capsules séparées d'une seule), groupe POSITIF propre au gumori et aucun masque : il ne
    touche que les cœurs du même gumori (deux voisines : écartées par leur joint, `collideConnected` faux). Hitbox,
    contacts avec les autres, `Untangle` / `Escape` inchangés. Sans masse ni événements. Coupé pendant une fusion
    (`SetSelfCollision`, rallumé quand il est rond). Mesuré (`Captures/bancs/self_bench.cs`, bot du niveau à bocaux) :
    anneaux retournés 0,66 % des relevés → 0, parois enfoncées de plus d'une épaisseur 6,5 % → 0, pire 2 → 0,5 épaisseur ;
    0,46 → 0,53 ms par pas (Space), 0,26 → 0,30 (terrestre, `fusion_compare.cs`) ; identique au bit près (2 essais
    chacun) ; les parties changent (terrestre scriptée : 20 → 39 fusions). `SetShape` (forme imposée), `SetCollisionGroup`, et ce que tous les
    scripts partagent : `Center`, `Velocity`, `AddVelocity` (même vitesse à tout l'anneau), `Contains` (point dans le
    polygone des capsules), `Index` (rang dans la liste). Les capsules portent leur `Gumori` en `userData.objectValue`
    (`Gumori.FromShape`)
  - `GumoriDropper` (lâcher, suite aléatoire, aperçu ; possède la liste des gumori en jeu `InPlay`, `IsInPlay` en O(1) ;
    événement `Dropped`), `GumoriFusion` (fusion et définition des niveaux), `GumoriScore` (points, événement `Scored`
    après chaque fusion comptée), `GumoriGoal` (coups max + objectif de points calculé selon la difficulté, fin de niveau ;
    limite donnée à `GumoriDropper.dropLimit`), `GumoriEscape` (sort les gumori enfermés dans un autre), `GumoriWallHold`
    (parois qui retiennent au lieu de relancer)
  - Space : `GumoriLauncher` (réserve et tir), `GumoriFoam` (pression), `GumoriUntangle`, `SpaceEndless`
  - Mécaniques de niveau (Space) : `GumoriJars` (bocaux), `GumoriMill` (moulin), `SpaceLevel` (objectif : bocaux en N tirs) ;
    dessinées par `Cosmetics/GumoriLevelArt`
  - Parties : `GumoriCampaign` (niveaux de l'Aventure), `GumoriSession` (partie choisie, progression dans PlayerPrefs,
    gardée en mémoire, graines tirées au hasard), `GumoriLevelSetup` (applique la partie choisie au réservoir, boutons de
    fin ; Play direct dans l'éditeur = réglages de la scène, rien ne change), `GumoriUi` (boutons et textes OnGUI communs,
    dont le texte cerné du HUD `Outlined`)
- `Scripts/Map/` : la carte (`GumoriMap` décor et caméra, `GumoriMapMesh` sol, `GumoriMapAvatar`, `GumoriMapMenu` titre / fiche /
  enchaînements). Purement visuel, hors simulation (l'avatar a sa physique Box2D, mais rien n'y est rejoué).
- `Scripts/Cosmetics/` : habillage (`GumoriSkin`, `GumoriFx`, `GumoriBackdrop`, `GumoriSound` + `GumoriSynth`, `GumoriFinale`,
  catalogue `GumoriDesign`) ; ils mettent les événements de la simulation en file et ignorent le rejeu accéléré.
  `GumoriSkin.Impact` = chocs (vus à l'affichage)
- Scènes : `Assets/Scenes/Slime/` (carte, réservoir, space endless, space endless 1)

## Organisation des scripts
- `Scripts/Gumori/` : tout le jeu. Code utilisable en build (pas de `UnityEditor`). Rangé (2026-10-27, avant tout sur un seul
  objet « Lâcher ») comme les sous-objets de « Jeu » dans les scènes : `GumoriDropper` (objet « Jeu » : gumori en jeu, suite)
  au-dessus, puis `Fusion/` (`GumoriFusion`, `GumoriEscape`), `Règles/` (`GumoriScore`, `GumoriGoal`, `SpaceEndless`,
  `SpaceLevel` ; objet « Règles » qui porte aussi `GumoriLevelSetup`), `Réserve/` (`GumoriLauncher`), `Corps/` (`Gumori`,
  `GumoriFoam`, `GumoriUntangle`, `GumoriWallHold`), `Mécaniques/` (`GumoriJars`, `GumoriMill`), `Parties/` (`GumoriCampaign`,
  `GumoriSession`, `GumoriLevelSetup`, `GumoriUi`). Mêmes catégories dans le menu Add Component (« Gumori/… »). Un système
  trouve les autres par `GumoriDropper.Find<T>(this)` (parent « Jeu » puis ses enfants) ou `dropper.Part<T>()`, jamais
  `GetComponent` (plus sur le même objet). L'ordre des sous-objets = ordre des `LevelTick` : refonte vérifiée identique au
  bit près (`Captures/bancs/ref_run.sh` : terrestre, space endless 1, niveau à bocaux)
- `Scripts/Core/` : moteur ; `Scripts/Cosmetics/` : effets purement visuels (ils LISENT l'état, jamais d'écriture, jamais
  dans `LevelTick`, dans `LateUpdate`) ; `Editor/` : inspecteurs et poignées (pas inclus dans le jeu)
- Déplacer / renommer un script uniquement via Unity (`AssetDatabase.MoveAsset`) : le `.meta` suit, les scènes gardent leurs références

## Construction des éléments
- Composant `[ExecuteAlways]` entièrement paramétrable depuis l'inspecteur, rien en dur ; défauts du script = réglages officiels
- Aperçu dans la Scene view AVANT de lancer le jeu (`OnDrawGizmos`), même géométrie que celle envoyée à la physique
- La physique n'est créée qu'en Play, dans `OnEnable`, et détruite dans `OnDisable` ; le composant implémente `ILevelPhysics`
  (tout corps créé hors d'un `ILevelPhysics` empêche la remise à zéro du monde)

## Rendu
- Habillage (décision du 2026-10-22) : maillages procéduraux reconstruits à chaque image par `Cosmetics/` (`GumoriSkin`,
  `GumoriFx`, `GumoriBackdrop`), dessinés par `Graphics.RenderMesh` pour chaque caméra ; shaders `Assets/Shaders/`
  (`Slime/Body`, `Slime/Fx`, `Slime/Backdrop`). Ordre de dessin par file de rendu : fond 2900, vitre 2950, slimes 3000,
  parois 3010, effets 3020. Lueur : couleurs > 1 + bloom (volume « Effets écran », profil `Settings/Slime Volume`)
- Toujours aucun sprite ni texture importée : formes, motifs et visages calculés (distances signées dans le shader)
- Carte : `Slime/Map` (sol peint), `Slime/MapCard` (découpes de papier : une sorte = une fonction de forme, alpha prémultiplié
  + profondeur écrite, maillage trié du plus loin au plus près), `Slime/MapSprite` (ciel, étoiles, ombres). Files : ciel 1000,
  sol 2000, ombres 2950, découpes 2970, étoiles 2990, avatar 3000. Couleurs de sommets données en sRGB : les convertir
  (`Color.linear`), sinon tout ressort délavé (projet en linéaire)
- Dessin debug de la physique : `WorldSettings.drawOptions` (formes sélectionnées seulement) et `GumoriDropper.debugDraw`

## Son
- Procédural (décision du 2026-10-22) : `GumoriSound` calcule tous les sons à partir de formules, aucun fichier audio
- DA sonore (décision du 2026-10-24) : grave, rond, APAISANT, satisfaisant ; peu d'aigus, pas d'énergie agressive. Tous les
  sons accordés dans UNE gamme (pentatonique majeure de `key`) : ensemble, ils sonnent toujours juste, jamais de brouhaha.
  Cloche de chaîne SUPPRIMÉE (jugée insupportable à la longue)
- Lâcher = « plop » ; tir (Space) = « fwoump » de souffle selon la vitesse (`GumoriLauncher.Launched`) ; choc = « splotch »
  sourd quand il est doux, mouillé quand il est violent (volume selon la vitesse perdue, silencieux sous 3,5 km/h), plus grave
  si le gumori est gros. Bruits : hauteur variée au hasard (`jitter`) ; sons placés gauche / droite
- Fusion = « bloup » (le son préféré de l'utilisateur) : sa note monte d'un cran dans la gamme à chaque maillon de chaîne et
  il gagne des couches (octave grave dès 2 maillons, harmonie dès 3), façon série de kills Valorant ; « doum » grave à
  l'éclat, sur la même note ; grondement (niveau 6+), cascade de bulles (chaîne de 3+), accord de fête doux (record dès le
  niveau 4, niveau max). Souffle et nappe baissent un instant sous une fusion (`duckDepth`)
- Validé par l'utilisateur (2026-10-24) : fusion, chaîne, lancement, confettis / fête
- Mouvement : `GumoriSkin.Motions` donne pour chaque gumori à l'écran son tremblement (vitesse de changement de forme de
  l'anneau : ovale + trèfle, 1/s), son étirement et sa vitesse. ÉCARTÉ (2026-10-24, « bruit de moustique, abominable,
  omniprésent ») : une voix TENUE à hauteur fixe (≈ 150 Hz + harmoniques) dont volume et filtre suivaient le tremblement.
  Règle : jamais de son continu à hauteur définie pour le mouvement. À la place, tout discret (≈ 10 dB sous les fusions) :
  - souffle d'air grave SANS hauteur (`GumoriSynth`, temps réel, bruit filtré) pour un gumori qui file (lancé, en chute) :
    à partir de 4 km/h, max à 12,6 km/h ; plus grave et plus présent s'il est gros ; les 3 plus rapides seulement ;
  - « blop » mou et grave à un gros rebond de forme (l'étirement passe un extrême, tremblement ≥ 1,2/s), 4 par seconde
    max en tout ; rien sur les frémissements de la pile ;
  - glissement doux d'un gumori qui arrive dans la réserve (Space, `GumoriLauncher.Arrived` ; il y entre à ≈ 5 km/h, trop
    lent pour le souffle) : plus long et plus grave s'il est gros.
  Mesuré : Space, un tir = tremblements de 1 à 3,7/s, retombés à 0,02–0,2 en ≈ 1 s ; terrestre, 0,5 à 1,9 à l'atterrissage,
  éteint en 0,3 s : le même système sonne tout seul plus flottant en basse gravité. Nappe d'ambiance (do sol ré qui
  respire) COUPÉE par défaut (`ambience` 0 : tenue en permanence)
- Mesuré (PC, enregistrement du mélange réel, un tir toutes les 1,6 s) : mélange −16 à −29 dB RMS par seconde, crête
  ≥ −1 dB, aucune saturation ; mouvement seul −32 à −44 dB. Synthé 0,8 % d'un cœur (4 souffles), fil audio. Simulation
  intouchée (identique au bit près, terrestre et Space)
- Piège (2026-10-24) : un seul échantillon NaN dans un son (ex. `Mathf.Pow` d'un `sin(π)` légèrement négatif) rendait tout
  le son NaN à la normalisation, puis tout le MÉLANGE pendant qu'il jouait (coupures, saturation). `Normalize` remplace
  maintenant tout échantillon invalide par 0, `GumoriSynth` se remet à zéro s'il en produit. Mesurer le son par
  `AudioListener.GetOutputData` depuis des commandes `eval` est faux (l'éditeur se fige ≈ 1,5 s par commande) : enregistrer
  le mélange avec `Assets/Temp/SoundTap.cs` (banc ignoré par git, posé sur l'AudioListener en Play, tire / lâche lui-même,
  WAV + rapport dans `SoundTap.Report`)
- Téléphone : haut-parleurs quasi muets sous ≈ 200 Hz : tous les sons graves ont des harmoniques (on « entend » la basse
  quand même). Écouter au casque ET sur le haut-parleur
- Je ne peux pas écouter : les réglages se font à l'oreille par l'utilisateur (volumes, `key`, `stepsPerLevel`, seuils du
  mouvement…). Écoute hors jeu : sons ponctuels `Captures/bancs/sound_wav.cs` (Play) → `Captures/sons/apercu.wav` ; mélange
  réel enregistré par `SoundTap` → `Captures/sons/space.wav`, `Captures/sons/terrestre.wav`

## Déterminisme
- Pas de `Random` sans graine fixe explicite
- Logique de simulation et pilotage physique dans `ILevelTick.LevelTick()`, jamais dans `Update` ni `FixedUpdate`
  (un pas doit pouvoir être rejoué en accéléré) ; utiliser `Time.fixedDeltaTime`
- `LevelSimulation` est le seul moteur : à chaque pas fixe, entrées → `LevelTick` de chaque élément (ordre de la hiérarchie)
  → `PhysicsWorld.Simulate` (monde en `SimulationType.Script`, réglé par lui seul). Actions entre deux pas
  (redémarrage, retour à une sauvegarde) : `LevelSimulation.RunBeforeNextStep`
  Pourquoi : avec chaque script dans son propre FixedUpdate, l'ordre dépendait de Unity et un pas ne pouvait pas être
  rejoué hors du temps réel. Un seul moteur = ordre fixe + rejeu accéléré exact (sauvegardes, replays, fantômes)
- Entrées du joueur : uniquement via `LevelInput` (un bit par `GameKey` : Espace, flèches…, enregistré à chaque pas), jamais
  le clavier directement dans la simulation. Toute action non enregistrée (ex. souris de debug) casse le rejeu
- Ordre de création des corps/joints stable et explicite (ne pas dépendre de l'ordre
  d'exécution des scripts) ; ordre d'itération stable (pas de dépendance aux Dictionary/HashSet)
- Ne jamais dépendre de l'ordre des événements ni des contacts renvoyés par Box2D (`triggerBeginEvents`, `contactBeginEvents`,
  `GetContacts`, `GetBodies`) : il suit ses identifiants internes, qui changent d'un essai à l'autre (alternés à chaque
  redémarrage, mesuré). La simulation Box2D reste identique, mais un script qui traite « le premier » diverge. Trier par une
  clé stable (ordre de création, index de membre), ou rendre le calcul indépendant de l'ordre (somme en entiers, maximum
  départagé par la position). `GumoriFusion` le fait (paires triées par ordre de création). Mesuré (audit du 2026-10-18, époque blob) : `GetBodies` change
  bien d'ordre, le `GetContacts` d'un même corps est resté stable ; trier reste une protection gratuite
- Une forme peut être détruite (vitre brisée) avant qu'un autre script lise l'événement de contact : tester `other.isValid`
- Audit du 2026-10-18 (époque blob, scène `Legacy/Blob/poc niveau 1`), pour ne pas le refaire : aucune divergence avec les
  touches appuyées (6 essais, nombreux scénarios). Banc de test réutilisable (`unity command eval_file`, hors dépôt) :
  entrées fixes + hash par pas des bits de tous les corps triés, temps d'un redémarrage. `Temp/` est vidé par Unity au
  redémarrage : garder les bancs dans le scratchpad.
- Signaler tout risque de non-déterminisme plutôt que de l'ignorer
- Anciennes parties (décision du 2026-10-27) : AUCUNE importance de pouvoir rejouer les parties enregistrées avant un
  changement de comportement (fusion, physique, réglages…) ; on change librement ce qui fait diverger les anciennes
  parties. Seule règle : le jeu tel qu'il est reste déterministe (même code + mêmes entrées = même partie au bit près).
  (« Ne jamais changer la simulation pour gagner du temps » reste vrai pour les OPTIMISATIONS : elles ne doivent rien changer.)
- Recommencer = `LevelRestart.Restart()` (R, la mort plus tard) : détruit toute la physique, `PhysicsWorld.Reset()`
  sur le monde vide (sinon l'historique interne de Box2D fait diverger les essais), puis recrée dans l'ordre de la hiérarchie.
  Le départ du niveau passe aussi par là (au chargement de la scène) : chaque essai est identique au bit près (~1 ms)
- Tout état à remettre à zéro hors physique (caméra, timer, musique…) s'abonne à `LevelRestart.Restarted`
  (et à `LevelSimulation.Jumped` pour un retour à une sauvegarde ; ignorer les événements pendant `LevelSimulation.FastForwarding`)
- Point de sauvegarde = entrées de chaque pas depuis le départ ; y revenir = redémarrage + rejeu accéléré (exact au bit près)

## Performance
Pourquoi c'est central : revenir à une sauvegarde rejoue TOUS les pas depuis le départ (60 par seconde de jeu). Le coût d'un
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
  (distance entre centres avant de tester les capsules), lire une fois puis réutiliser, ne pas écrire un Transform
  dont personne n'a besoin en jeu (obstacles sur trajectoire : le corps fait foi)
- **Se faire prévenir plutôt que demander à chaque pas** : événements de Box2D (début / fin de contact via `contactEvents`)
  au lieu d'interroger chaque corps à chaque pas
- **Aucune allocation par pas** (pas de `new`, de LINQ, de `List` créée dans `LevelTick`) : listes réutilisées ou tableaux préparés au départ
- **Sauter le calcul quand rien n'a changé** (ex. `GumoriFusion` ne cherche des paires que s'il y a des contacts)
- **Réglages du monde qui ne changent pas le résultat** : `simulationWorkers` = 1 (Box2D sur 12 threads passait plus de temps à
  coordonner les cœurs qu'à calculer : −33 %, et encore plus utile sur mobile). Résultat identique quel que soit ce nombre
- Le visuel (`LateUpdate`, `Cosmetics/`, dessin debug, dessin du personnage) ne tourne qu'une fois par image, jamais pendant le
  rejeu accéléré : il compte moins que la simulation, mais reste léger pour le mobile

Repères mesurés (PC, 1 thread, 2026-10-25, `Captures/bancs/audit_ref.cs`) : terrestre, partie scriptée de 3600 pas (≈ 20
gumori en fin de partie) ≈ 0,21 ms par pas ; Space, 1800 pas (≈ 18 gumori de 32 capsules) ≈ 1,0 ms par pas.

Vérifier une optimisation (méthode utilisée) :
1. Avec l'ancien code, en Play : redémarrer, rejouer N pas (`LevelSimulation.FastForward` avec des entrées fixes, flèches et
   Espace compris, et un scénario de mort), enregistrer les bits des positions / vitesses de tous les corps
   (`BitConverter.SingleToInt32Bits`), et chronométrer (meilleur de plusieurs essais : la machine varie de ±10 %)
2. Recompiler, refaire exactement pareil : « identique au bit près » obligatoire, puis comparer les temps
3. Valeurs d'inspecteur avant / après une refonte (renommage de classe, champ retiré) : `Captures/bancs/audit_values.cs`
   sur chaque scène, puis comparer les fichiers

## Conventions de code
- Pas de commentaires, sauf point vraiment non évident
- Paramètres exposés : toujours `[Header]` + `[Tooltip]` (en français, précis sur l'effet et les unités),
  plus `[Min]` / `[Range]` quand pertinent
- Vitesses linéaires exposées : stockées en m/s (unité du code et de Box2D), affichées et saisies en km/h dans l'inspecteur
  grâce à `[SpeedKmh]` (`Core/SpeedKmhAttribute`, dessin `Editor/Core/SpeedKmhDrawer`, float ou Vector2, respecte `[Min]`) ;
  le tooltip parle en km/h. Valeur réécrite seulement si on la modifie : sans effet sur la simulation. Pas pour les rotations
  (°/s) ni les accélérations (m/s²)
- Noms de variables et classes en anglais ; champs privés préfixés `_` (exception : les champs publics français de `Ball2D`/`WorldSettings`, gardés pour ne pas perdre les valeurs)
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
