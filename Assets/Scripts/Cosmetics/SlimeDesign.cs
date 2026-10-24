using System.Collections.Generic;
using UnityEngine;

// Habillage d'un gumori découpé en catégories (une case = un élément, obtenable à part) : tout se combine avec tout, rien n'est
// une texture (formes et motifs calculés). Ajouter un élément = une valeur d'enum + son dessin (SlimeSkin / shader Slime/Body).
// L'ordre des valeurs est enregistré dans les scènes : toujours AJOUTER à la fin, ne jamais insérer ni renuméroter.

// Famille de couleur = niveau du gumori (même ordre). Une famille est une fourchette (teinte + luminosité), pas une couleur.
public enum GumoriFamily
{
    Orange, Violet, Rose, [InspectorName("Bleu")] Blue, [InspectorName("Jaune")] Yellow, [InspectorName("Rouge")] Red,
    [InspectorName("Crème")] Cream, [InspectorName("Chocolat")] Chocolate, [InspectorName("Vert")] Green,
    [InspectorName("Noir")] Black, [InspectorName("Spécial")] Special
}

public enum GumoriRarity
{
    [InspectorName("Commun")] Common, Rare, [InspectorName("Ultra rare")] UltraRare, [InspectorName("Spécial")] Special
}

// Motif sur la gelée (au-dessus de la couleur du corps).
public enum SlimePattern
{
    [InspectorName("Aucun")] None, [InspectorName("Bulles")] Bubbles, [InspectorName("Pois")] Spots,
    [InspectorName("Ondes")] Rings, [InspectorName("Alvéoles")] Honeycomb, [InspectorName("Rayures")] Stripes,
    [InspectorName("Magma")] Cracks, [InspectorName("Paillettes")] Sparkles, [InspectorName("Cristal")] Facets,
    [InspectorName("Galaxie")] Galaxy, [InspectorName("Arc-en-ciel")] Rainbow,
    [InspectorName("Anneaux lunaires")] MoonRings, [InspectorName("Vermicelles")] Sprinkles
}

// Reflet par-dessus tout le gumori (façon carte brillante).
public enum SlimeSheen { [InspectorName("Aucun")] None, Holo, [InspectorName("Étincelles")] Glitter }

// Case « tête » (chapeaux, antennes, pousses…).
public enum SlimeHead
{
    [InspectorName("Aucun")] None, [InspectorName("Pousse")] Leaf, Antennae, [InspectorName("Oreilles de chat")] Ears,
    [InspectorName("Cornes")] Horns, [InspectorName("Nœud")] Bow, [InspectorName("Cristaux")] Crystals,
    [InspectorName("Auréole")] Halo, [InspectorName("Couronne")] Crown, [InspectorName("Pousse de fleur")] Flower,
    [InspectorName("Oreilles d'ourson")] RoundEars, [InspectorName("Cerise")] Cherry
}

// Case « dos » (dessinée derrière le corps).
public enum SlimeBack { [InspectorName("Aucun")] None, [InspectorName("Ailes de chauve-souris")] BatWings, [InspectorName("Ailes de fée")] FairyWings }

public enum SlimeEyes { [InspectorName("Ronds")] Round, [InspectorName("Étoilés")] Starry, [InspectorName("Lumineux")] Glowing }

public enum SlimeMouth
{
    [InspectorName("Sourire")] Smile, [InspectorName("Chat")] Cat, [InspectorName("Grand sourire")] Grin,
    [InspectorName("En coin")] Smirk, [InspectorName("Croc")] Fang
}

// Attitude au repos : yeux fermés (rêveur), paupières mi-closes (fier), sourcils (féroce).
public enum SlimeAttitude { [InspectorName("Calme")] Calm, [InspectorName("Rêveur")] Dreamy, [InspectorName("Fier")] Proud, [InspectorName("Féroce")] Fierce }

[System.Serializable]
public class SlimeDesign
{
    public string name;
    public GumoriFamily family;
    [Tooltip("Rareté, selon la sophistication : commun = couleur + visage + au plus un élément simple ; rare = motif et/ou\n" +
             "accessoire ; ultra rare = effets lumineux, plusieurs cases, reflet.")]
    public GumoriRarity rarity;

    [Header("Corps")]
    [Tooltip("Couleur principale de la gelée (haut du slime), dans la fourchette de sa famille. Linéaire (Hex() convertit le sRGB).")]
    public Color body = Color.white;
    [Tooltip("Ombre : bas du slime et liseré du contour.")]
    public Color shade = Color.gray;

    [Header("Motif")]
    public SlimePattern pattern;
    [Tooltip("Couleur du motif (bulles, pois, rayures, fissures…) et de ses lueurs ; aussi les yeux lumineux.")]
    public Color accent = Color.white;
    [Tooltip("Lueur du motif et des accessoires : 0 = aucune ; au-delà de 1, ça brille (bloom).")]
    [Range(0f, 4f)] public float glow;

    [Header("Reflet")]
    public SlimeSheen sheen;

    [Header("Tête")]
    public SlimeHead head;
    public Color headColor = Color.white;

    [Header("Dos")]
    public SlimeBack back;
    public Color backColor = Color.white;

    [Header("Visage")]
    public SlimeEyes eyes;
    public SlimeMouth mouth;
    public SlimeAttitude attitude;
    [Tooltip("Joues roses.")]
    public bool blush;

    [Header("Shiny (même gumori, couleurs rares dans SA famille)")]
    public Color shinyBody = Color.white;
    public Color shinyShade = Color.gray;
    public Color shinyAccent = Color.white;
    public Color shinyHead = Color.white;
    public Color shinyBack = Color.white;

    [System.NonSerialized] SlimeDesign _shiny;

    // Version shiny : mêmes cases, couleurs shiny, étincelles si aucun reflet.
    public SlimeDesign Shiny
    {
        get
        {
            if (_shiny != null) return _shiny;
            _shiny = (SlimeDesign)MemberwiseClone();
            _shiny._shiny = _shiny;
            _shiny.name = name + " shiny";
            _shiny.body = shinyBody;
            _shiny.shade = shinyShade;
            _shiny.accent = shinyAccent;
            _shiny.headColor = shinyHead;
            _shiny.backColor = shinyBack;
            if (_shiny.sheen == SlimeSheen.None) _shiny.sheen = SlimeSheen.Glitter;
            return _shiny;
        }
    }

    public void ResetShinyCache() => _shiny = null;

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

    SlimeDesign Colors(string body, string shade, string accent)
    {
        this.body = Hex(body);
        this.shade = Hex(shade);
        this.accent = Hex(accent);
        return this;
    }

    SlimeDesign ShinyColors(string body, string shade, string accent, string head = null, string back = null)
    {
        shinyBody = Hex(body);
        shinyShade = Hex(shade);
        shinyAccent = Hex(accent);
        shinyHead = head != null ? Hex(head) : headColor;
        shinyBack = back != null ? Hex(back) : backColor;
        return this;
    }

    SlimeDesign Head(SlimeHead item, string color)
    {
        head = item;
        headColor = Hex(color);
        return this;
    }

    SlimeDesign Back(SlimeBack item, string color)
    {
        back = item;
        backColor = Hex(color);
        return this;
    }

    static SlimeDesign New(string name, GumoriFamily family, GumoriRarity rarity) => new SlimeDesign { name = name, family = family, rarity = rarity };

    // Un design par niveau (= par famille, dans l'ordre des niveaux) : ce qu'on voit en jeu. Les petits (vus en nombre mais
    // petits à l'écran) restent simples ; les détails et effets montent avec la taille.
    public static List<SlimeDesign> Levels() => new List<SlimeDesign>
    {
        New("Clémentine", GumoriFamily.Orange, GumoriRarity.Common).Colors("#FFA63D", "#E0611A", "#FFE2B0")
            .Set(SlimePattern.Spots, SlimeEyes.Round, SlimeMouth.Smile, true)
            .ShinyColors("#FF8A4C", "#D2452A", "#FFF4C8"),
        New("Raisin", GumoriFamily.Violet, GumoriRarity.Common).Colors("#8A2DB8", "#4A1373", "#E6C8FF")
            .Set(SlimePattern.Bubbles, SlimeEyes.Round, SlimeMouth.Cat, true)
            .ShinyColors("#6A3FD0", "#34197A", "#FFD6F5"),
        New("Bonbon", GumoriFamily.Rose, GumoriRarity.Rare).Colors("#FF8DC4", "#E0457F", "#FFFFFF")
            .Set(SlimePattern.Sparkles, SlimeEyes.Round, SlimeMouth.Smile, true, 0.8f).Head(SlimeHead.Bow, "#F2335F")
            .ShinyColors("#FF7FB0", "#C93C7C", "#FFE58A", "#8E4BFF"),
        New("Bulle", GumoriFamily.Blue, GumoriRarity.Common).Colors("#2456C8", "#122C78", "#BFE6FF")
            .Set(SlimePattern.Bubbles, SlimeEyes.Round, SlimeMouth.Smile, true)
            .ShinyColors("#3A47D8", "#1A1F7A", "#FFE6A8"),
        New("Citron", GumoriFamily.Yellow, GumoriRarity.Rare).Colors("#FFC62A", "#E08A0A", "#FFF6C0")
            .Set(SlimePattern.Rings, SlimeEyes.Starry, SlimeMouth.Smile, false, 0.8f)
            .ShinyColors("#F2DC3A", "#B49A0C", "#FFFFFF"),
        New("Braise", GumoriFamily.Red, GumoriRarity.UltraRare).Colors("#E0303F", "#6E0C1C", "#FFB52E")
            .Set(SlimePattern.Cracks, SlimeEyes.Round, SlimeMouth.Grin, false, 2.2f, SlimeAttitude.Fierce)
            .Head(SlimeHead.Horns, "#4A1414")
            .ShinyColors("#C8204E", "#4E0A26", "#7FE8FF", "#1E1E2A"),
        New("Chantilly", GumoriFamily.Cream, GumoriRarity.Rare).Colors("#FBF3E3", "#D9C3A0", "#FFFFFF")
            .Set(SlimePattern.Sprinkles, SlimeEyes.Round, SlimeMouth.Cat, true).Head(SlimeHead.Cherry, "#E3263B")
            .ShinyColors("#FFEFF3", "#E0BFC8", "#FFFFFF", "#FFC83D"),
        New("Ourson", GumoriFamily.Chocolate, GumoriRarity.Common).Colors("#7A4524", "#3E1E0E", "#A86A3E")
            .Set(SlimePattern.None, SlimeEyes.Round, SlimeMouth.Smile, true).Head(SlimeHead.RoundEars, "#6A3A1E")
            .ShinyColors("#94602F", "#4A2A12", "#C89060", "#86532A"),
        New("Bourgeon", GumoriFamily.Green, GumoriRarity.Rare).Colors("#8BD63A", "#3F9020", "#E2FFB8")
            .Set(SlimePattern.None, SlimeEyes.Round, SlimeMouth.Smile, true, 0f, SlimeAttitude.Dreamy)
            .Head(SlimeHead.Flower, "#FF9FCB")
            .ShinyColors("#6FD65A", "#2F8A30", "#F4FFD0", "#FFE36B"),
        New("Lune", GumoriFamily.Black, GumoriRarity.UltraRare).Colors("#221C2B", "#0C0A12", "#FFD54A")
            .Set(SlimePattern.MoonRings, SlimeEyes.Glowing, SlimeMouth.Fang, false, 1.6f).Back(SlimeBack.BatWings, "#4B3D63")
            .ShinyColors("#1C2236", "#0A0D18", "#6FD8FF", null, "#3D4A6E"),
        New("Roi", GumoriFamily.Special, GumoriRarity.Special).Colors("#FEFEFF", "#C4B3F2", "#FFFFFF")
            .Set(SlimePattern.Rainbow, SlimeEyes.Round, SlimeMouth.Smirk, true, 0.5f, SlimeAttitude.Proud)
            .Head(SlimeHead.Crown, "#FFD24A")
            .ShinyColors("#FFF6DC", "#E6B866", "#FFFFFF", "#E8E8F5"),
    };

    // Autres gumori de la collection (pas encore en jeu) : variantes d'une famille, chacune dans sa fourchette de couleur.
    public static List<SlimeDesign> Variants() => new List<SlimeDesign>
    {
        New("Tigrou", GumoriFamily.Orange, GumoriRarity.Rare).Colors("#FF9A2C", "#D2541A", "#6B2A0A")
            .Set(SlimePattern.Stripes, SlimeEyes.Round, SlimeMouth.Cat, false).Head(SlimeHead.Ears, "#FF9A2C")
            .ShinyColors("#FFB347", "#D97A1A", "#3A2A6B", "#FFB347"),
        New("Cristal", GumoriFamily.Violet, GumoriRarity.UltraRare).Colors("#7442CC", "#3A1680", "#E6F7FF")
            .Set(SlimePattern.Facets, SlimeEyes.Round, SlimeMouth.Smile, false, 0.7f).Head(SlimeHead.Crystals, "#9EEBFF")
            .Sheen(SlimeSheen.Holo)
            .ShinyColors("#9C3FCB", "#521A80", "#FFF0FA", "#FFB8F0"),
        New("Fée", GumoriFamily.Rose, GumoriRarity.UltraRare).Colors("#FFA4D1", "#E0558F", "#FFFFFF")
            .Set(SlimePattern.Sparkles, SlimeEyes.Starry, SlimeMouth.Smile, true, 0.6f).Back(SlimeBack.FairyWings, "#FFE6F6")
            .Sheen(SlimeSheen.Holo)
            .ShinyColors("#FF8DB8", "#C94080", "#FFF2A0", null, "#E8F6FF"),
        New("Lagon", GumoriFamily.Blue, GumoriRarity.Common).Colors("#3068DA", "#163A8E", "#6FE8E0")
            .Set(SlimePattern.Spots, SlimeEyes.Round, SlimeMouth.Smile, true)
            .ShinyColors("#2A4FC0", "#121F6E", "#FFF2A8"),
        New("Abeille", GumoriFamily.Yellow, GumoriRarity.Rare).Colors("#FFD444", "#E09A0E", "#FFF2B8")
            .Set(SlimePattern.Honeycomb, SlimeEyes.Round, SlimeMouth.Smile, true, 0.3f).Head(SlimeHead.Antennae, "#3A2414")
            .ShinyColors("#F5E066", "#B89A10", "#FFFFFF", "#2A2A4A"),
        New("Nébuleuse", GumoriFamily.Black, GumoriRarity.UltraRare).Colors("#2E2638", "#100C18", "#FF8CEB")
            .Set(SlimePattern.Galaxy, SlimeEyes.Round, SlimeMouth.Smile, false, 1.2f, SlimeAttitude.Dreamy)
            .Head(SlimeHead.Halo, "#FFE68C")
            .ShinyColors("#1E2A3A", "#0A1018", "#8CF0FF", "#FFFFFF"),
    };

    SlimeDesign Set(SlimePattern pattern, SlimeEyes eyes, SlimeMouth mouth, bool blush, float glow = 0f, SlimeAttitude attitude = SlimeAttitude.Calm)
    {
        this.pattern = pattern;
        this.eyes = eyes;
        this.mouth = mouth;
        this.blush = blush;
        this.glow = glow;
        this.attitude = attitude;
        return this;
    }

    SlimeDesign Sheen(SlimeSheen sheen)
    {
        this.sheen = sheen;
        return this;
    }
}
