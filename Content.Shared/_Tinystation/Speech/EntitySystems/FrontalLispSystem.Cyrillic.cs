using System.Text.RegularExpressions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class FrontalLispSystem
{
    private static readonly Regex RegexUpperCyrTh = new("[С]+[Ц]+|[К]+[С]+(?=[ИЕЫЭЁЮЯ]+)|[Т]+[С]+|[Ц]+(?=[ИЕЫЭЁЮЯ]+)|[З]+|[С]+");
    private static readonly Regex RegexLowerCyrTh = new("[с]+[ц]+|[к]+[с]+(?=[иеыэёюя]+)|[т]+[с]+|[ц]+(?=[иеыэёюя]+)|[з]+|[с]+");
    private static readonly Regex RegexUpperCyrHush = new("[ШЩЖЧ]+");
    private static readonly Regex RegexLowerCyrHush = new("[шщжч]+");

    private static string ApplyCyrillicTransformations(string message)
    {
        message = RegexUpperCyrTh.Replace(message, "ТЬ");
        message = RegexLowerCyrTh.Replace(message, "ть");
        message = RegexUpperCyrHush.Replace(message, "С");
        message = RegexLowerCyrHush.Replace(message, "с");
        return message;
    }
}
