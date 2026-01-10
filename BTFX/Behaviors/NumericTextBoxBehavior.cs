using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace BTFX.Behaviors;

/// <summary>
/// Numeric input behavior for TextBox
/// </summary>
public partial class NumericTextBoxBehavior : Behavior<TextBox>
{
    /// <summary>
    /// Allow decimal point
    /// </summary>
    public static readonly DependencyProperty AllowDecimalProperty =
        DependencyProperty.Register(
            nameof(AllowDecimal),
            typeof(bool),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(false));

    /// <summary>
    /// Maximum decimal places
    /// </summary>
    public static readonly DependencyProperty MaxDecimalPlacesProperty =
        DependencyProperty.Register(
            nameof(MaxDecimalPlaces),
            typeof(int),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(2));

    /// <summary>
    /// Allow decimal point
    /// </summary>
    public bool AllowDecimal
    {
        get => (bool)GetValue(AllowDecimalProperty);
        set => SetValue(AllowDecimalProperty, value);
    }

    /// <summary>
    /// Maximum decimal places
    /// </summary>
    public int MaxDecimalPlaces
    {
        get => (int)GetValue(MaxDecimalPlacesProperty);
        set => SetValue(MaxDecimalPlacesProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.LostFocus += OnLostFocus;
        DataObject.AddPastingHandler(AssociatedObject, OnPaste);

        // Disable IME (Input Method Editor) for numeric input
        System.Windows.Input.InputMethod.SetIsInputMethodEnabled(AssociatedObject, false);
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.LostFocus -= OnLostFocus;
        DataObject.RemovePastingHandler(AssociatedObject, OnPaste);
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        // Block any non-numeric characters including IME input
        if (string.IsNullOrEmpty(e.Text))
        {
            e.Handled = true;
            return;
        }

        // Get current text and what it would be after this input
        var currentText = textBox.Text;
        var proposedText = GetProposedText(textBox, e.Text);

        // Check each character in the input
        foreach (char c in e.Text)
        {
            // Allow digits
            if (char.IsDigit(c))
                continue;

            // Allow decimal point if enabled and would not create duplicate
            if (AllowDecimal && c == '.')
            {
                // Check if decimal point already exists in the proposed text
                int decimalCount = proposedText.Count(ch => ch == '.');
                if (decimalCount <= 1)
                    continue;
            }

            // Block everything else (Chinese characters, special symbols, etc.)
            e.Handled = true;
            return;
        }

        // Validate the proposed text
        if (!IsValidInput(proposedText))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Allow navigation keys
        if (e.Key == Key.Back || e.Key == Key.Delete || 
            e.Key == Key.Left || e.Key == Key.Right || 
            e.Key == Key.Tab || e.Key == Key.Home || e.Key == Key.End)
        {
            return;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        // Validate the current text (to catch any IME or composition input)
        var currentText = textBox.Text;

        if (!IsValidInput(currentText))
        {
            // Remove invalid characters
            var validText = RemoveInvalidCharacters(currentText);

            // Only update if text actually changed
            if (validText != currentText)
            {
                var caretIndex = textBox.CaretIndex;
                textBox.Text = validText;

                // Restore caret position (adjusted for removed characters)
                textBox.CaretIndex = Math.Min(caretIndex, validText.Length);
            }
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        var textBox = sender as TextBox;
        if (textBox == null) return;

        var text = textBox.Text;

        // Remove trailing decimal point when losing focus
        if (AllowDecimal && !string.IsNullOrEmpty(text) && text.EndsWith('.'))
        {
            textBox.Text = text.TrimEnd('.');
        }
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));

            // Remove invalid characters from pasted text
            var validText = RemoveInvalidCharacters(text);

            if (string.IsNullOrEmpty(validText) || !IsValidInput(validText))
            {
                e.CancelCommand();
            }
            else if (validText != text)
            {
                // Replace with cleaned text
                e.CancelCommand();
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    var caretIndex = textBox.SelectionStart;
                    var selectionLength = textBox.SelectionLength;
                    var newText = textBox.Text.Remove(caretIndex, selectionLength)
                                             .Insert(caretIndex, validText);

                    if (IsValidInput(newText))
                    {
                        textBox.Text = newText;
                        textBox.CaretIndex = caretIndex + validText.Length;
                    }
                }
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private string GetProposedText(TextBox textBox, string input)
    {
        var currentText = textBox.Text;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        return currentText.Remove(selectionStart, selectionLength)
                         .Insert(selectionStart, input);
    }

    private string RemoveInvalidCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = new System.Text.StringBuilder();
        bool hasDecimalPoint = false;

        foreach (char c in text)
        {
            // Allow digits
            if (char.IsDigit(c))
            {
                result.Append(c);
            }
            // Allow only one decimal point if enabled
            else if (AllowDecimal && c == '.' && !hasDecimalPoint)
            {
                result.Append(c);
                hasDecimalPoint = true;
            }
            // Skip all other characters (including Chinese, symbols, etc.)
        }

        return result.ToString();
    }

    private bool IsValidInput(string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        // Check if text contains only valid characters
        foreach (char c in text)
        {
            if (!char.IsDigit(c) && !(AllowDecimal && c == '.'))
                return false;
        }

        if (AllowDecimal)
        {
            // Check decimal point rules
            var parts = text.Split('.');

            // Only one decimal point allowed
            if (parts.Length > 2)
                return false;

            // Allow trailing decimal point (e.g., "175.")
            // Check decimal places only if there are digits after the decimal point
            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]) && MaxDecimalPlaces > 0)
            {
                if (parts[1].Length > MaxDecimalPlaces)
                    return false;
            }
        }

        return true;
    }
}
