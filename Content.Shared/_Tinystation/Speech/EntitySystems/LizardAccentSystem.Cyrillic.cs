using System.Text.RegularExpressions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Speech.EntitySystems;

public sealed partial class LizardAccentSystem
{
    private static readonly Regex RegexLowerCyrS = new("с+");
    private static readonly Regex RegexUpperCyrS = new("С+");
    private static readonly Regex RegexLowerZ = new("з+");
    private static readonly Regex RegexUpperZ = new("З+");
    private static readonly Regex RegexLowerSh = new("ш+");
    private static readonly Regex RegexUpperSh = new("Ш+");
    private static readonly Regex RegexLowerCh = new("ч+");
    private static readonly Regex RegexUpperCh = new("Ч+");
    private static readonly Regex RegexInternalKha = new(@"(\w)х");
    private static readonly Regex RegexInternalUpperKha = new(@"(\w)Х");
    private static readonly Regex RegexLowerEndKha = new(@"\bх([\-|р|Р]|\b)");
    private static readonly Regex RegexUpperEndKha = new(@"\bХ([\-|р|Р]|\b)");


    private static string ApplyCyrillicTransformations(string message)
    {
        message = RegexInternalKha.Replace(message, "$1кхс");
        message = RegexInternalUpperKha.Replace(message, "$1КХС");
        message = RegexLowerEndKha.Replace(message, "экс$1");
        message = RegexUpperEndKha.Replace(message, "ЭКС$1");
        message = RegexLowerCyrS.Replace(message, "ссс");
        message = RegexUpperCyrS.Replace(message, "ССС");
        message = RegexLowerZ.Replace(message, "ссс");
        message = RegexUpperZ.Replace(message, "ССС");
        message = RegexLowerSh.Replace(message, "шшш");
        message = RegexUpperSh.Replace(message, "ШШШ");
        message = RegexLowerCh.Replace(message, "щщщ");
        message = RegexUpperCh.Replace(message, "ЩЩЩ");
        return message;
    }
}
