using System.Windows;
using System.Windows.Controls;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;

[TemplatePart(Name = PartPasswordBox, Type = typeof(PasswordBox))]
[TemplatePart(Name = PartTextBox, Type = typeof(TextBox))]
[TemplatePart(Name = PartToggleButton, Type = typeof(Button))]
public class GamePasswordBox : Control
{
    private const string PartPasswordBox = "PART_PasswordBox";
    private const string PartTextBox = "PART_TextBox";
    private const string PartToggleButton = "PART_ToggleButton";

    private PasswordBox? _passwordBox;
    private TextBox? _textBox;
    private Button? _toggleButton;
    private bool _isUpdating;

    static GamePasswordBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GamePasswordBox),
            new FrameworkPropertyMetadata(typeof(GamePasswordBox)));
    }

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(GamePasswordBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPasswordChanged));

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    public static readonly DependencyProperty IsPasswordVisibleProperty =
        DependencyProperty.Register(
            nameof(IsPasswordVisible),
            typeof(bool),
            typeof(GamePasswordBox),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsPasswordVisibleChanged));

    public bool IsPasswordVisible
    {
        get => (bool)GetValue(IsPasswordVisibleProperty);
        set => SetValue(IsPasswordVisibleProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(GamePasswordBox),
            new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(GamePasswordBox),
            new PropertyMetadata(string.Empty));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_passwordBox != null)
        {
            _passwordBox.PasswordChanged -= OnPasswordBoxPasswordChanged;
        }

        if (_textBox != null)
        {
            _textBox.TextChanged -= OnTextBoxTextChanged;
        }

        if (_toggleButton != null)
        {
            _toggleButton.Click -= OnToggleButtonClick;
        }

        _passwordBox = GetTemplateChild(PartPasswordBox) as PasswordBox;
        _textBox = GetTemplateChild(PartTextBox) as TextBox;
        _toggleButton = GetTemplateChild(PartToggleButton) as Button;

        if (_passwordBox != null)
        {
            _passwordBox.Password = Password ?? string.Empty;
            _passwordBox.PasswordChanged += OnPasswordBoxPasswordChanged;
        }

        if (_textBox != null)
        {
            _textBox.Text = Password ?? string.Empty;
            _textBox.TextChanged += OnTextBoxTextChanged;
        }

        if (_toggleButton != null)
        {
            _toggleButton.Click += OnToggleButtonClick;
        }

        UpdateVisibility();
    }

    private void OnPasswordBoxPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            Password = _passwordBox?.Password ?? string.Empty;
            if (_textBox != null)
            {
                _textBox.Text = Password;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            Password = _textBox?.Text ?? string.Empty;
            if (_passwordBox != null)
            {
                _passwordBox.Password = Password;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnToggleButtonClick(object sender, RoutedEventArgs e)
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GamePasswordBox box && !box._isUpdating)
        {
            box._isUpdating = true;
            try
            {
                var val = (string?)e.NewValue ?? string.Empty;
                if (box._passwordBox != null && box._passwordBox.Password != val)
                {
                    box._passwordBox.Password = val;
                }
                if (box._textBox != null && box._textBox.Text != val)
                {
                    box._textBox.Text = val;
                }
            }
            finally
            {
                box._isUpdating = false;
            }
        }
    }

    private static void OnIsPasswordVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GamePasswordBox box)
        {
            box.UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        if (_passwordBox == null || _textBox == null) return;

        if (IsPasswordVisible)
        {
            _passwordBox.Visibility = Visibility.Collapsed;
            _textBox.Visibility = Visibility.Visible;
            _textBox.Focus();
            _textBox.CaretIndex = _textBox.Text.Length;
        }
        else
        {
            _textBox.Visibility = Visibility.Collapsed;
            _passwordBox.Visibility = Visibility.Visible;
            _passwordBox.Focus();
        }
    }
}
