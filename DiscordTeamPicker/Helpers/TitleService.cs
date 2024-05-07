using System;

namespace DiscordTeamPicker.Helpers;

public static class TitleService
{
    public static string ChooseTitle()
    {
        Random random = new Random();
        string[] titles =
        [
            "Banda de los Tramposos",
            "Club de los Chistes Malos",
            "Patrulla Chiflada", 
            "Peor es nada",
            "Legion de la Perdicion",
            "Bajas Expectativas",
            "Club de exnovias locas",
            "Chicas Malas",
            "Los telefonos rodantes",
            "Abismo de las princesas",
            "Gremio de las abuelas",
            "Mamis Calientes",
            "Compañeros de bolos",
            "Nombre genial pendiente"
        ];

        return titles[random.Next(0, titles.Length - 1)];
    }
}