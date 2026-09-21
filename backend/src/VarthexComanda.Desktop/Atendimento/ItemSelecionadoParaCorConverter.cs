using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VarthexComanda.Desktop.Atendimento;

/// <summary>
/// Destaque da linha selecionada da comanda (RNF18): values[0] = id do item da linha,
/// values[1] = <c>ItemSelecionadoId</c> (int?). Parametro "Marca" devolve a cor da barra lateral
/// (transparente quando nao selecionada); sem parametro devolve o fundo da linha.
/// </summary>
public class ItemSelecionadoParaCorConverter : IMultiValueConverter
{
    private static readonly Brush Fundo = Congelar(new SolidColorBrush(Color.FromRgb(0xFB, 0xE3, 0xC2)));
    private static readonly Brush Marca = Congelar(new SolidColorBrush(Color.FromRgb(0xD9, 0x82, 0x2B)));

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var selecionada = values.Length == 2 && values[0] is int id && values[1] is int selecionadoId && id == selecionadoId;
        var marca = parameter is string p && p == "Marca";

        if (!selecionada)
        {
            return Brushes.Transparent;
        }

        return marca ? Marca : Fundo;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Congelar(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
