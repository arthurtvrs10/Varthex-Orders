using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public class CategoriaSelecionadaParaCorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 2 && values[0] is Categoria item && values[1] is Categoria selecionada && item.Id == selecionada.Id)
        {
            return new SolidColorBrush(Color.FromRgb(0xED, 0xED, 0xEA));
        }

        return Brushes.White;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
