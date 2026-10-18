using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Helpers;

public static class MarkdownHelper
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.RegisterAttached(
            "Markdown",
            typeof(string),
            typeof(MarkdownHelper),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static string GetMarkdown(DependencyObject obj) => (string)obj.GetValue(MarkdownProperty);
    public static void SetMarkdown(DependencyObject obj, string value) => obj.SetValue(MarkdownProperty, value);

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock textBlock)
        {
            UpdateTextBlock(textBlock, e.NewValue as string ?? string.Empty);
        }
    }

    private static readonly Brush Header1Brush = new SolidColorBrush(Color.FromRgb(139, 37, 0));
    private static readonly Brush Header2Brush = new SolidColorBrush(Color.FromRgb(180, 83, 9));
    private static readonly Brush Header3Brush = new SolidColorBrush(Color.FromRgb(46, 125, 50));
    private static readonly Brush TextDarkBrush = new SolidColorBrush(Color.FromRgb(38, 19, 2));
    private static readonly Brush TextMutedBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
    private static readonly Brush DividerBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235));

    static MarkdownHelper()
    {
        Header1Brush.Freeze();
        Header2Brush.Freeze();
        Header3Brush.Freeze();
        TextDarkBrush.Freeze();
        TextMutedBrush.Freeze();
        DividerBrush.Freeze();
    }

    public static void UpdateTextBlock(TextBlock textBlock, string markdown)
    {
        textBlock.Inlines.Clear();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        string[] lines = markdown.Replace("\r\n", "\n").Split('\n');
        bool isFirstLine = true;

        foreach (string rawLine in lines)
        {
            string line = rawLine;

            if (!isFirstLine)
            {
                textBlock.Inlines.Add(new LineBreak());
            }
            isFirstLine = false;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string trimmed = line.Trim();

            if (trimmed == "---" || trimmed == "***" || trimmed == "___")
            {
                textBlock.Inlines.Add(new Run("──────────────────────────────────────────")
                {
                    Foreground = DividerBrush,
                    FontSize = 9
                });
                continue;
            }

            if (trimmed.StartsWith("# ") && !trimmed.StartsWith("## "))
            {
                string title = trimmed[2..].Trim();
                textBlock.Inlines.Add(new Run(title)
                {
                    FontWeight = FontWeights.ExtraBold,
                    FontSize = 13.5,
                    Foreground = Header1Brush
                });
                continue;
            }

            if (trimmed.StartsWith("## ") && !trimmed.StartsWith("### "))
            {
                string title = trimmed[3..].Trim();
                textBlock.Inlines.Add(new Run(title)
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 12.5,
                    Foreground = Header2Brush
                });
                continue;
            }

            if (trimmed.StartsWith("### "))
            {
                string title = trimmed[4..].Trim();
                textBlock.Inlines.Add(new Run(title)
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5,
                    Foreground = Header3Brush
                });
                continue;
            }

            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
            {
                int indent = line.Length - line.TrimStart().Length;
                string prefix = indent >= 2 ? "      •  " : "   •  ";
                string content = trimmed[2..].Trim();

                textBlock.Inlines.Add(new Run(prefix)
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = indent >= 2 ? TextMutedBrush : Header2Brush
                });

                AppendFormattedInline(textBlock, content, TextDarkBrush);
                continue;
            }

            AppendFormattedInline(textBlock, trimmed, TextDarkBrush);
        }
    }

    private static void AppendFormattedInline(TextBlock textBlock, string text, Brush defaultBrush)
    {
        string[] parts = text.Split("**");
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;

            bool isBold = (i % 2 == 1);
            textBlock.Inlines.Add(new Run(parts[i])
            {
                FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isBold ? TextDarkBrush : defaultBrush
            });
        }
    }
}
