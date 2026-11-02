namespace DulceAtardecer.Data;

internal static class CatalogSeedData
{
    public static readonly CategorySeed[] Categories =
    [
        new("Crepas",
        [
            new("Frutales",
            [
                new("Crepa 1 Untable", "Crepa con 1 untable", 50m),
                new("Crepa Durazno", "Crepa con durazno", 65m),
                new("Crepa Fresa", "Crepa con fresa", 70m),
                new("Crepa Fresa/Durazno", "Crepa con fresa y durazno", 75m),
                new("Crepa Fresa/Plátano", "Crepa con fresa y plátano", 75m),
                new("Crepa Fresa/Uva", "Crepa con fresa y uva", 75m),
                new("Crepa Plátano", "Crepa con plátano", 60m),
                new("Crepa Uva", "Crepa con uva", 70m),
            ]),
            new("Saladas",
            [
                new("Crepa 3 Quesos", "Crepa con tres quesos", 65m),
                new("Crepa Hawaiana", "Crepa hawaiana", 75m),
                new("Crepa Jamón/Queso Philadelphia", "Crepa con jamón y queso Philadelphia", 65m),
                new("Crepa Pepperoni", "Crepa con pepperoni", 75m),
            ]),
            new("Premium",
            [
                new("Crepa Conejito Turin", "Crepa con Conejito Turin", 88m),
                new("Crepa Crema Cacahuate/Plátano", "Crepa con crema de cacahuate y plátano", 65m),
                new("Crepa Ferrero", "Crepa con Ferrero", 88m),
                new("Crepa Kinder Bueno/Fresa", "Crepa con Kinder Bueno y fresa", 90m),
                new("Crepa Oreo/Helado", "Crepa con Oreo y helado", 75m),
                new("Crepa Oreo/Fresa/Helado", "Crepa con Oreo, fresa y helado", 80m),
            ]),
            new("Especialidad",
            [
                new("Crepa Dulce Atardecer", "Crepa con fresa, plátano, uva, manzana y durazno", 85m),
            ]),
        ]),
        new("Waffles",
        [
            new("Frutales",
            [
                new("Waffle 1 Untable", "Waffle con 1 Untable", 50m),
                new("Waffle Durazno", "Waffle con Durazno", 65m),
                new("Waffle Fresa", "Waffle con Fresa", 70m),
                new("Waffle Fresa/Durazno", "Waffle con Fresa y Durazno", 75m),
                new("Waffle Fresa/Plátano", "Waffle con Fresa y Plátano", 75m),
                new("Waffle Fresa/Uva", "Waffle con Fresa y Uva", 75m),
                new("Waffle Plátano", "Waffle con Plátano", 60m),
                new("Waffle Uva", "Waffle con Uva", 70m),
            ]),
            new("Especialidad",
            [
                new("Waffle Dulce Atardecer", "Waffle con Fresa, Plátano, Uva, Manzana y Durazno", 80m),
            ]),
            new("Premium",
            [
                new("Waffle Conejito Turin/Helado", "Waffle con Conejito Turin y Helado", 85m),
                new("Waffle Crema Cacahuate/Plátano", "Waffle con Crema de Cacahuate y Plátano", 65m),
                new("Waffle Ferrero", "Waffle con Ferrero", 85m),
                new("Waffle Kinder Bueno/Fresa", "Waffle con Kinder Bueno y Fresa", 90m),
                new("Waffle Oreo/Fresa/Helado", "Waffle con Oreo, Fresa y Helado", 80m),
                new("Waffle Oreo/Helado", "Waffle con Oreo y Helado", 70m),
            ]),
        ]),
        new("Bebidas",
        [
            new("Frías",
            [
                new("Café Frío (caramel, vainilla, o crema irlandesa)", "Café frío con sabores a elegir: caramelo, vainilla o crema irlandesa.", 50m),
                new("Frappe Café", "Frappe frío a base de café espresso.", 50m),
                new("Frappe Chocolate", "Frappe frío de chocolate con hielo frappé.", 50m),
                new("Frappe Ferrero", "Frappe con sabor a chocolate Ferrero Rocher.", 65m),
                new("Frappe Mazapán", "Frappe cremoso con auténtico mazapán.", 50m),
                new("Frappe Oreo", "Frappe de galleta Oreo con crema batida.", 55m),
                new("Frezada (base agua)", "Bebida refrescante de fresa, sin leche.", 55m),
                new("Malteada Chocolate", "Malteada fría de chocolate con leche cremosa.", 60m),
                new("Malteada Fresa", "Malteada cremosa con sabor a fresa natural.", 60m),
                new("Malteada Vainilla", "Deliciosa malteada con esencia de vainilla.", 60m),
                new("Mangonada (base agua)", "Mangonada con chamoy y chile en polvo.", 50m),
            ]),
            new("Calientes",
            [
                new("Americano", "Café americano caliente, sabor clásico y fuerte.", 30m),
                new("Capuchino", "Capuchino espumoso con leche vaporizada.", 40m),
                new("Chocolate", "Chocolate caliente y espeso, ideal para el frío.", 40m),
                new("Mokachino", "Café con leche y un toque de chocolate.", 40m),
            ]),
        ]),
    ];

    public static readonly ExtraSeed[] Extras =
    [
        new("Crema Cacahuate", 8m),
        new("Cajeta", 8m),
        new("Mermelada Zarzamora", 8m),
        new("Mermelada Fresa", 8m),
        new("Philadelphia", 8m),
        new("Nutella", 8m),
        new("Fresa", 10m),
        new("Plátano", 10m),
        new("Durazno", 10m),
        new("Uva", 10m),
        new("Helado", 15m),
        new("Nuez", 10m),
        new("Ferrero", 20m),
        new("Turin", 20m),
        new("Kinder Bueno", 28m),
    ];
}

internal sealed record CategorySeed(string Name, SubcategorySeed[] Subcategories);

internal sealed record SubcategorySeed(string Name, ProductSeed[] Products);

internal sealed record ProductSeed(string Name, string Description, decimal Price);

internal sealed record ExtraSeed(string Name, decimal Price);
