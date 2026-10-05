using System.ComponentModel.DataAnnotations;
namespace web_tcg.Data.Enums;
public enum PoliticalOrientation
{
    [Display(Name = "Extrême gauche")] FarLeft = 1,
    [Display(Name = "Gauche")] Left = 2,
    [Display(Name = "Centre")] Centre = 3,
    [Display(Name = "Droite")] Right = 4,
    [Display(Name = "Extrême droite")] FarRight = 5
}
