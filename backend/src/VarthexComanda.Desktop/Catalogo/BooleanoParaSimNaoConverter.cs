using System.Globalization;
using System.Windows.Data;

namespace VarthexComanda.Desktop.Catalogo;

/// <summary>Exibe um booleano como "Sim"/"Não" (nulo ou outro tipo vira "Não").</summary>
public class BooleanoParaSimNaoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "Sim" : "Não";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
