using System.Collections.Generic;
using UnityEngine;

// Habillage d'un gumori découpé en catégories (une case = un élément, obtenable à part) : tout se combine avec tout, rien n'est
// une texture (formes et motifs calculés). Ajouter un élément = une valeur d'enum + son dessin (GumoriSkin / shader Slime/Body).
// L'ordre des valeurs est enregistré dans les scènes : toujours AJOUTER à la fin, ne jamais insérer ni renuméroter.

// Famille de couleur = niveau du gumori (même ordre). Une famille est une fourchette (teinte + luminosité), pas une couleur.
public enum GumoriFamily
{
    Orange, Violet, Rose, [InspectorName("Bleu")] Blue, [InspectorName("Jaune")] Yellow, [InspectorName("Rouge")] Red,
    [InspectorName("Crème")] Cream, [InspectorName("Chocolat")] Chocolate, [InspectorName("Vert")] Green,
    [InspectorName("Noir")] Black, [InspectorName("Spécial")] Special
}

// Palier d'un gumori : commun (dont la version de base, vue en jeu), rare (même thème, un détail ou une couleur de détail en
// plus), ultra rare = « shiny » (même thème + effets de brillance, réservés à ce palier, sans perturber l'identité).
public enum GumoriRarity
{
    [InspectorName("Commun")] Common, Rare, [InspectorName("Ultra rare (shiny)")] UltraRare, [InspectorName("Spécial")] Special
}

// Motif sur la gelée (au-dessus de la couleur du corps).
public enum GumoriPattern
{
    [InspectorName("Aucun")] None, [InspectorName("Bulles")] Bubbles, [InspectorName("Pois")] Spots,
    [InspectorName("Ondes")] Rings, [InspectorName("Alvéoles")] Honeycomb, [InspectorName("Rayures")] Stripes,
    [InspectorName("Magma")] Cracks, [InspectorName("Paillettes")] Sparkles, [InspectorName("Cristal")] Facets,
    [InspectorName("Galaxie")] Galaxy, [InspectorName("Arc-en-ciel")] Rainbow,
    [InspectorName("Anneaux lunaires")] MoonRings, [InspectorName("Vermicelles")] Sprinkles,
    [InspectorName("Eau")] Water, Cactus, [InspectorName("Filets de chocolat")] Drizzle,
    [InspectorName("Fleurs de sakura")] Sakura
}

// Reflet par-dessus tout le gumori (façon carte brillante) : réservé aux ultra rares.
public enum GumoriSheen { [InspectorName("Aucun")] None, Holo, [InspectorName("Étincelles")] Glitter, [InspectorName("Braises")] Embers,
    [InspectorName("Pétales de sakura")] Petals }

// Case « tête » (chapeaux, antennes, pousses…).
public enum GumoriHead
{
    [InspectorName("Aucun")] None, [InspectorName("Pousse")] Leaf, Antennae, [InspectorName("Oreilles de chat")] Ears,
    [InspectorName("Cornes")] Horns, [InspectorName("Nœud")] Bow, [InspectorName("Cristaux")] Crystals,
    [InspectorName("Auréole")] Halo, [InspectorName("Couronne")] Crown, [InspectorName("Pousse de fleur")] Flower,
    [InspectorName("Oreilles d'ourson")] RoundEars, [InspectorName("Cerise")] Cherry,
    [InspectorName("Gerbe de cristaux")] CrystalCluster, [InspectorName("Bandeau de ninja")] Headband
}

// Case « dos » (dessinée derrière le corps).
public enum GumoriBack
{
    [InspectorName("Aucun")] None, [InspectorName("Ailes de chauve-souris")] BatWings, [InspectorName("Ailes de fée")] FairyWings,
    Katana, [InspectorName("Deux katanas")] TwinKatanas
}

public enum GumoriEyes { [InspectorName("Ronds")] Round, [InspectorName("Étoilés")] Starry, [InspectorName("Lumineux")] Glowing }

public enum GumoriMouth
{
    [InspectorName("Sourire")] Smile, [InspectorName("Chat")] Cat, [InspectorName("Grand sourire")] Grin,
    [InspectorName("En coin")] Smirk, [InspectorName("Croc")] Fang, [InspectorName("Museau")] Muzzle,
    [InspectorName("Masque de ninja")] Mask
}

// Attitude au repos : yeux fermés (rêveur), paupières mi-closes (fier), sourcils (féroce).
public enum GumoriAttitude { [InspectorName("Calme")] Calm, [InspectorName("Rêveur")] Dreamy, [InspectorName("Fier")] Proud, [InspectorName("Féroce")] Fierce }

[System.Serializable]
public class GumoriDesign
{
    public string name;
    public GumoriFamily family;
    [Tooltip("Commun (dont la version de base du niveau), rare (même thème, un détail en plus), ultra rare = shiny (effets de\n" +
             "brillance réservés à ce palier, détails en plus, sans perturber l'identité du thème).")]
    public GumoriRarity rarity;

    [Header("Corps")]
    [Tooltip("Couleur principale de la gelée (haut du gumori), dans la fourchette de sa famille. Linéaire (Hex() convertit le sRGB).")]
    public Color body = Color.white;
    [Tooltip("Ombre : bas du gumori et liseré du contour.")]
    public Color shade = Color.gray;

    [Header("Motif")]
    public GumoriPattern pattern;
    [Tooltip("Couleur du motif (bulles, pois, rayures, fissures…) et de ses lueurs ; aussi les yeux lumineux et les braises.")]
    public Color accent = Color.white;
    [Tooltip("Lueur du motif et des accessoires : 0 = aucune ; au-delà de 1, ça brille (bloom).")]
    [Range(0f, 4f)] public float glow;

    [Header("Reflet (ultra rare)")]
    public GumoriSheen sheen;

    [Header("Tête")]
    public GumoriHead head;
    public Color headColor = Color.white;

    [Header("Dos")]
    public GumoriBack back;
    public Color backColor = Color.white;

    [Header("Visage")]
    public GumoriEyes eyes;
    public GumoriMouth mouth;
    public GumoriAttitude attitude;
    [Tooltip("Joues roses.")]
    public bool blush;

    // Couleur sRGB (#RRGGBB, comme sur une palette) → linéaire, ce qu'attend le shader. Calcul C# pur : appelé pendant la
    // construction du composant (initialiseur de champ), où les appels au moteur sont interdits.
    public static Color Hex(string hex)
    {
        int v = System.Convert.ToInt32(hex.TrimStart('#'), 16);
        return new Color(Linear((v >> 16) & 255), Linear((v >> 8) & 255), Linear(v & 255), 1f);
    }

    static float Linear(int c)
    {
        float s = c / 255f;
        return s <= 0.04045f ? s / 12.92f : (float)System.Math.Pow((s + 0.055f) / 1.055f, 2.4);
    }

    static GumoriDesign New(string name, GumoriFamily family, GumoriRarity rarity) => new GumoriDesign { name = name, family = family, rarity = rarity };

    // Version de base de chaque niveau (= famille, dans l'ordre des niveaux) : ce qu'on voit en jeu. Un THÈME par gumori, qui a
    // un sens avec sa couleur (tigre orange, cristal violet, bonbon rose, eau bleue, ruche jaune, magma rouge, chantilly,
    // ourson en chocolat, cactus vert, nébuleuse noire). Pas d'effet de brillance ici (réservés aux ultra rares).
    public static List<GumoriDesign> Levels() => new List<GumoriDesign>
    {
        New("Tigrou", GumoriFamily.Orange, GumoriRarity.Common).Colors("#FFA63D", "#D9581A", "#6B2A0A")
            .Set(GumoriPattern.Stripes, GumoriEyes.Round, GumoriMouth.Cat, false).Head(GumoriHead.Ears, "#FFA63D"),
        New("Cristal", GumoriFamily.Violet, GumoriRarity.Common).Colors("#A048DE", "#5A1E96", "#F0E2FF")
            .Set(GumoriPattern.Facets, GumoriEyes.Round, GumoriMouth.Smile, true, 0.2f).Head(GumoriHead.Crystals, "#C9F2FF"),
        New("Bonbon", GumoriFamily.Rose, GumoriRarity.Common).Colors("#FFA4D1", "#E0558F", "#FFFFFF")
            .Set(GumoriPattern.Sparkles, GumoriEyes.Round, GumoriMouth.Smile, true, 0.8f).Head(GumoriHead.Bow, "#F2335F"),
        New("Bulle", GumoriFamily.Blue, GumoriRarity.Common).Colors("#3F86EE", "#1D4FB0", "#D8F1FF")
            .Set(GumoriPattern.Water, GumoriEyes.Round, GumoriMouth.Smile, true),
        New("Abeille", GumoriFamily.Yellow, GumoriRarity.Common).Colors("#FFD444", "#E09A0E", "#FFF2B8")
            .Set(GumoriPattern.Honeycomb, GumoriEyes.Round, GumoriMouth.Smile, true, 0.3f).Head(GumoriHead.Antennae, "#3A2414"),
        New("Braise", GumoriFamily.Red, GumoriRarity.Common).Colors("#E0303F", "#6E0C1C", "#FFB52E")
            .Set(GumoriPattern.Cracks, GumoriEyes.Round, GumoriMouth.Grin, false, 2.2f, GumoriAttitude.Fierce)
            .Head(GumoriHead.Horns, "#4A1414"),
        New("Chantilly", GumoriFamily.Cream, GumoriRarity.Common).Colors("#FFEFF3", "#E0BFC8", "#FFFFFF")
            .Set(GumoriPattern.Sprinkles, GumoriEyes.Round, GumoriMouth.Cat, true).Head(GumoriHead.Cherry, "#FFC83D"),
        New("Ourson", GumoriFamily.Chocolate, GumoriRarity.Common).Colors("#7A4524", "#3E1E0E", "#FFF0D8")
            .Set(GumoriPattern.Drizzle, GumoriEyes.Round, GumoriMouth.Muzzle, true).Head(GumoriHead.RoundEars, "#6A3A1E"),
        New("Cactus", GumoriFamily.Green, GumoriRarity.Common).Colors("#8BD63A", "#3F9020", "#FFFFFF")
            .Set(GumoriPattern.Cactus, GumoriEyes.Round, GumoriMouth.Smile, true).Head(GumoriHead.Flower, "#FF7FB8"),
        New("Nébuleuse", GumoriFamily.Black, GumoriRarity.Common).Colors("#2E2638", "#100C18", "#FF8CEB")
            .Set(GumoriPattern.Galaxy, GumoriEyes.Round, GumoriMouth.Smile, false, 0.35f, GumoriAttitude.Dreamy),
        New("Roi", GumoriFamily.Special, GumoriRarity.Special).Colors("#FEFEFF", "#C4B3F2", "#FFFFFF")
            .Set(GumoriPattern.Rainbow, GumoriEyes.Round, GumoriMouth.Smirk, true, 0.5f, GumoriAttitude.Proud)
            .Head(GumoriHead.Crown, "#FFD24A"),
    };

    // Autres gumori de la collection (pas encore en jeu), chacun dans la fourchette de couleur de sa famille.
    public static List<GumoriDesign> Variants() => new List<GumoriDesign>
    {
        // Communs : autres thèmes de la famille.
        New("Clémentine", GumoriFamily.Orange, GumoriRarity.Common).Colors("#FFA63D", "#E0611A", "#FFE2B0")
            .Set(GumoriPattern.Spots, GumoriEyes.Round, GumoriMouth.Smile, true),
        New("Raisin", GumoriFamily.Violet, GumoriRarity.Common).Colors("#8E38CC", "#4A1373", "#E6C8FF")
            .Set(GumoriPattern.Bubbles, GumoriEyes.Round, GumoriMouth.Cat, true),
        New("Lagon", GumoriFamily.Blue, GumoriRarity.Common).Colors("#3274E0", "#17459E", "#6FE8E0")
            .Set(GumoriPattern.Spots, GumoriEyes.Round, GumoriMouth.Smile, true),
        New("Vanille", GumoriFamily.Cream, GumoriRarity.Common).Colors("#FBF3E3", "#D9C3A0", "#FFFFFF")
            .Set(GumoriPattern.Sprinkles, GumoriEyes.Round, GumoriMouth.Cat, true).Head(GumoriHead.Cherry, "#E3263B"),
        New("Bourgeon", GumoriFamily.Green, GumoriRarity.Common).Colors("#8BD63A", "#3F9020", "#E2FFB8")
            .Set(GumoriPattern.None, GumoriEyes.Round, GumoriMouth.Smile, true, 0f, GumoriAttitude.Dreamy)
            .Head(GumoriHead.Flower, "#FF9FCB"),
        New("Ninjakura", GumoriFamily.Rose, GumoriRarity.Common).Colors("#FFB3CF", "#D9638F", "#FFEAF2")
            .Set(GumoriPattern.Sakura, GumoriEyes.Round, GumoriMouth.Mask, false, 0f, GumoriAttitude.Fierce)
            .Head(GumoriHead.Headband, "#3A2A48").Back(GumoriBack.Katana, "#3A2A48"),

        // Rares : même thème, un détail ou une couleur de détail en plus.
        New("Citron", GumoriFamily.Yellow, GumoriRarity.Rare).Colors("#FFC62A", "#E08A0A", "#FFF6C0")
            .Set(GumoriPattern.Rings, GumoriEyes.Starry, GumoriMouth.Smile, false, 0.8f),
        New("Braise bleue", GumoriFamily.Red, GumoriRarity.Rare).Colors("#C8204E", "#4E0A26", "#7FE8FF")
            .Set(GumoriPattern.Cracks, GumoriEyes.Round, GumoriMouth.Grin, false, 2.2f, GumoriAttitude.Fierce)
            .Head(GumoriHead.Horns, "#1E1E2A"),
        New("Lune", GumoriFamily.Black, GumoriRarity.Rare).Colors("#221C2B", "#0C0A12", "#FFD54A")
            .Set(GumoriPattern.MoonRings, GumoriEyes.Glowing, GumoriMouth.Fang, false, 1.6f).Back(GumoriBack.BatWings, "#4B3D63"),

        // Ultra rares (shiny) : le thème de base en plus beau, effets de brillance.
        New("Cristal étincelant", GumoriFamily.Violet, GumoriRarity.UltraRare).Colors("#B05CEA", "#6A2AA8", "#F2FBFF")
            .Set(GumoriPattern.Facets, GumoriEyes.Round, GumoriMouth.Smile, false, 0.9f).Head(GumoriHead.CrystalCluster, "#9EEBFF")
            .Sheen(GumoriSheen.Holo),
        New("Fée", GumoriFamily.Rose, GumoriRarity.UltraRare).Colors("#FFA4D1", "#E0558F", "#FFFFFF")
            .Set(GumoriPattern.Sparkles, GumoriEyes.Starry, GumoriMouth.Smile, true, 0.6f).Back(GumoriBack.FairyWings, "#FFE6F6")
            .Sheen(GumoriSheen.Holo),
        New("Braise d'or", GumoriFamily.Red, GumoriRarity.UltraRare).Colors("#D92A3A", "#5A0816", "#FFE680")
            .Set(GumoriPattern.Cracks, GumoriEyes.Glowing, GumoriMouth.Grin, false, 2.6f, GumoriAttitude.Fierce)
            .Head(GumoriHead.Horns, "#2A0A0A").Sheen(GumoriSheen.Embers),
        New("Nébuleuse étoilée", GumoriFamily.Black, GumoriRarity.UltraRare).Colors("#2E2638", "#100C18", "#FF8CEB")
            .Set(GumoriPattern.Galaxy, GumoriEyes.Round, GumoriMouth.Smile, false, 1.2f, GumoriAttitude.Dreamy)
            .Head(GumoriHead.Halo, "#FFE68C").Sheen(GumoriSheen.Glitter),
        New("Ninjakura en fleurs", GumoriFamily.Rose, GumoriRarity.UltraRare).Colors("#FFB8D4", "#D95C8E", "#FFF2F7")
            .Set(GumoriPattern.Sakura, GumoriEyes.Round, GumoriMouth.Mask, false, 0.6f, GumoriAttitude.Fierce)
            .Head(GumoriHead.Headband, "#3A2A48").Back(GumoriBack.TwinKatanas, "#3A2A48").Sheen(GumoriSheen.Petals),
    };

    GumoriDesign Colors(string body, string shade, string accent)
    {
        this.body = Hex(body);
        this.shade = Hex(shade);
        this.accent = Hex(accent);
        return this;
    }

    GumoriDesign Set(GumoriPattern pattern, GumoriEyes eyes, GumoriMouth mouth, bool blush, float glow = 0f, GumoriAttitude attitude = GumoriAttitude.Calm)
    {
        this.pattern = pattern;
        this.eyes = eyes;
        this.mouth = mouth;
        this.blush = blush;
        this.glow = glow;
        this.attitude = attitude;
        return this;
    }

    GumoriDesign Head(GumoriHead item, string color)
    {
        head = item;
        headColor = Hex(color);
        return this;
    }

    GumoriDesign Back(GumoriBack item, string color)
    {
        back = item;
        backColor = Hex(color);
        return this;
    }

    GumoriDesign Sheen(GumoriSheen sheen)
    {
        this.sheen = sheen;
        return this;
    }
}
