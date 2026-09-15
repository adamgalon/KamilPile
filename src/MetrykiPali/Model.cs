namespace MetrykiPali;

/// <summary>
/// One row of the source table ("tabelka z palami"): a contiguous range of pile
/// numbers that share the same diameter, design length and reinforcement.
/// </summary>
public sealed class PileRange
{
    public int From { get; set; }
    public int To { get; set; }
    public double Diameter { get; set; }
    public double Length { get; set; }
    public string Reinforcement { get; set; } = "Brak";

    public int Count => To - From + 1;
}

/// <summary>A single pile, i.e. one column of a "METRYKA PALI" sheet.</summary>
public sealed class Pile
{
    public int Number { get; set; }
    public double Diameter { get; set; }
    public double DesignLength { get; set; }
    public double ActualLength { get; set; }
    public double Concrete { get; set; }
    public string ConcretePlant { get; set; } = "";
    public string Reinforcement { get; set; } = "";
}

/// <summary>Everything the generator needs besides the pile list itself.</summary>
public sealed class MetrykaSettings
{
    public string Budowa { get; set; } = "Budynek mieszkalny wielorodzinny, Łódź ul. Tuwima.";
    public string Wykonawca { get; set; } = "Greifbau sp. z o.o., ul. Jerozolimska 2/LU2, 30-555 Kraków";
    public string Metoda { get; set; } = "CFA (Wykonanego w technologii betonowania ciągłego)";
    public string Betoniarnia { get; set; } = "Bosta";
    public string Firma { get; set; } = "GREIFBAU SP. Z O.O.";
    public string DokumentacjaNaglowek { get; set; } = "DOKUMENTACJA POWYKONAWCZA";
    public DateTime Data { get; set; } = DateTime.Today;

    /// <summary>
    /// Overbreak coefficient: actual concrete / theoretical cylinder volume.
    /// 1.30 reproduces the volumes in the reference documentation for most pile
    /// lengths (e.g. D=0.4 / L=9 m -> 1.47 m3, L=8 m -> 1.31 m3, L=10 m -> 1.63 m3).
    /// </summary>
    public double ConcreteFactor { get; set; } = 1.30;

    public int PilesPerPage { get; set; } = 12;
}

public static class PileMath
{
    /// <summary>Theoretical shaft volume of a pile, m3.</summary>
    public static double TheoreticalVolume(double diameter, double length)
        => Math.PI * diameter * diameter / 4.0 * length;

    /// <summary>Concrete placed, m3, rounded the way the metryka reports it.</summary>
    public static double Concrete(double diameter, double length, double factor)
        => Math.Round(TheoreticalVolume(diameter, length) * factor, 2, MidpointRounding.AwayFromZero);
}
