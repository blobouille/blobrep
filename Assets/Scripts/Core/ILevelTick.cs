// Logique de simulation d'un élément de niveau, appelée une fois par pas physique par LevelSimulation
// (ordre de la hiérarchie), juste avant le pas. Remplace FixedUpdate : un pas peut être rejoué en accéléré.
public interface ILevelTick
{
    void LevelTick();
}
