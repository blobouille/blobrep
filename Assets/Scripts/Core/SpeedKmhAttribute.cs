using UnityEngine;

// Vitesse stockée en m/s (valeur utilisée par la simulation), affichée et saisie en km/h dans l'inspecteur.
// Sans effet sur la simulation : la valeur n'est réécrite que si on la modifie dans l'inspecteur.
public class SpeedKmhAttribute : PropertyAttribute
{
    public const float KmhPerMs = 3.6f;
}
