using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Converters;

public class PassthroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string p)
            return Localization.Get(p);

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class CasingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is CharacterCasing casing && value is string s)
        {
            return casing switch
            {
                CharacterCasing.Upper => s.ToUpper(),
                CharacterCasing.Lower => s.ToLower(),
                _ => s
            };
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}


public class LowerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string p)
            return Localization.Get(p).ToLower();

        if (value is string s)
            return s.ToLower();

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class UpperConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (parameter is string p && value is not FrameworkElement)
            value = Localization.Get(p).ToUpper();

        if (value is string s)
            return s.ToUpper();

        if (value is FrameworkElement)
            return value;

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class ZoomBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value != null)
        {
            var count = int.Parse(value.ToString());
            if (count > 0)
                return 1;
        }
        return 0.3;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class VisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string str)
            return !string.IsNullOrEmpty(str) ? Visibility.Visible : Visibility.Collapsed;

        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class IsNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool r = parameter is null ? true : false;
        return value is null ? r : !r;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
