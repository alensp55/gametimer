using System.Globalization;

namespace GameTime;

internal sealed class IntegerInput : NumericUpDown
{
    private string _lastValidText = "0";
    private bool _restoring;

    protected override void OnTextChanged(EventArgs e)
    {
        if (_restoring)
            return;
        if (Text.Length > 0)
        {
            bool integer = decimal.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out decimal value);
            if (!integer || value > Maximum || (value == 0 && Minimum > 0))
            {
                RestoreText();
                return;
            }
            if (value >= Minimum)
                _lastValidText = Text;
        }
        base.OnTextChanged(e);
    }

    protected override void ValidateEditText()
    {
        if (!decimal.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out decimal value)
            || value < Minimum || value > Maximum)
            RestoreText();
        base.ValidateEditText();
    }

    private void RestoreText()
    {
        _restoring = true;
        Text = _lastValidText;
        Select(0, Text.Length);
        _restoring = false;
    }
}
