using CRDebugger.Core.Options;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.WinForms.Controls;

/// <summary>
/// OptionKindに応じたWinFormsコントロールを動的に生成するファクトリ
/// モダンデザイン: 改善されたスペーシング、フォント、FlatStyleボタン
/// </summary>
public static class OptionControlFactory
{
    /// <summary>
    /// OptionItemViewModelからWinFormsコントロールを生成する
    /// </summary>
    public static Control Create(OptionItemViewModel item, ThemeColors colors)
    {
        // ActionItemの場合はボタンを生成
        if (item is ActionItemViewModel actionItem)
            return CreateActionButton(actionItem, colors);

        return item.Kind switch
        {
            OptionKind.Boolean => CreateBooleanControl(item, colors),
            OptionKind.Integer => CreateIntegerControl(item, colors),
            OptionKind.Float => CreateFloatControl(item, colors),
            OptionKind.String => CreateStringControl(item, colors),
            OptionKind.Enum => CreateEnumControl(item, colors),
            OptionKind.ReadOnly => CreateReadOnlyControl(item, colors),
            _ => CreateReadOnlyControl(item, colors),
        };
    }

    private static Control CreateBooleanControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);

        var checkBox = new CheckBox
        {
            Text = item.DisplayName,
            Checked = item.Value is true,
            Enabled = !item.IsReadOnly,
            AutoSize = true,
            ForeColor = ArgbToColor(colors.OnSurface),
            Font = new Font("Segoe UI", 9.5f),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 2, 6, 2),
        };

        checkBox.CheckedChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = checkBox.Checked;
        };

        // ViewModelの変更を監視
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                checkBox.Checked = item.Value is true;
        };

        panel.Controls.Add(checkBox);
        return panel;
    }

    private static Control CreateIntegerControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        if (item.Min.HasValue && item.Max.HasValue)
        {
            var trackBar = new TrackBar
            {
                Minimum = (int)item.Min.Value,
                Maximum = (int)item.Max.Value,
                Value = ClampInt(Convert.ToInt32(item.Value ?? 0), (int)item.Min.Value, (int)item.Max.Value),
                TickFrequency = (int)(item.Step ?? 1),
                SmallChange = (int)(item.Step ?? 1),
                LargeChange = (int)(item.Step ?? 1) * 5,
                Enabled = !item.IsReadOnly,
                Dock = DockStyle.Fill,
                BackColor = ArgbToColor(colors.Surface),
            };

            var valueLabel = new Label
            {
                Text = (item.Value ?? 0).ToString(),
                ForeColor = ArgbToColor(colors.Primary),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(6, 0, 6, 0),
            };

            trackBar.ValueChanged += (_, _) =>
            {
                if (!item.IsReadOnly)
                    item.Value = trackBar.Value;
                valueLabel.Text = trackBar.Value.ToString();
            };

            panel.Controls.Add(trackBar);
            panel.Controls.Add(valueLabel);
        }
        else
        {
            var numericUpDown = new NumericUpDown
            {
                Value = Convert.ToDecimal(item.Value ?? 0),
                Minimum = item.Min.HasValue ? (decimal)item.Min.Value : decimal.MinValue,
                Maximum = item.Max.HasValue ? (decimal)item.Max.Value : decimal.MaxValue,
                Increment = item.Step.HasValue ? (decimal)item.Step.Value : 1,
                DecimalPlaces = 0,
                Enabled = !item.IsReadOnly,
                Dock = DockStyle.Fill,
                BackColor = ArgbToColor(colors.SurfaceAlt),
                ForeColor = ArgbToColor(colors.OnSurface),
                Font = new Font("Segoe UI", 9.5f),
            };

            numericUpDown.ValueChanged += (_, _) =>
            {
                if (!item.IsReadOnly)
                    item.Value = (int)numericUpDown.Value;
            };

            panel.Controls.Add(numericUpDown);
        }

        return panel;
    }

    private static Control CreateFloatControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        var numericUpDown = new NumericUpDown
        {
            Value = ClampDecimal(Convert.ToDecimal(item.Value ?? 0.0),
                item.Min.HasValue ? (decimal)item.Min.Value : -9999999m,
                item.Max.HasValue ? (decimal)item.Max.Value : 9999999m),
            Minimum = item.Min.HasValue ? (decimal)item.Min.Value : -9999999m,
            Maximum = item.Max.HasValue ? (decimal)item.Max.Value : 9999999m,
            Increment = item.Step.HasValue ? (decimal)item.Step.Value : 0.1m,
            DecimalPlaces = 2,
            Enabled = !item.IsReadOnly,
            Dock = DockStyle.Fill,
            BackColor = ArgbToColor(colors.SurfaceAlt),
            ForeColor = ArgbToColor(colors.OnSurface),
            Font = new Font("Segoe UI", 9.5f),
        };

        numericUpDown.ValueChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = (double)numericUpDown.Value;
        };

        panel.Controls.Add(numericUpDown);
        return panel;
    }

    private static Control CreateStringControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        var textBox = new TextBox
        {
            Text = item.Value?.ToString() ?? string.Empty,
            ReadOnly = item.IsReadOnly,
            Dock = DockStyle.Fill,
            BackColor = ArgbToColor(colors.SurfaceAlt),
            ForeColor = ArgbToColor(colors.OnSurface),
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(6, 2, 6, 2),
        };

        textBox.TextChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = textBox.Text;
        };

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                textBox.Text = item.Value?.ToString() ?? string.Empty;
        };

        panel.Controls.Add(textBox);
        return panel;
    }

    private static Control CreateEnumControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        var comboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Enabled = !item.IsReadOnly,
            Dock = DockStyle.Fill,
            BackColor = ArgbToColor(colors.SurfaceAlt),
            ForeColor = ArgbToColor(colors.OnSurface),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
        };

        if (item.EnumNames != null)
        {
            comboBox.Items.AddRange(item.EnumNames);
            var currentValue = item.Value?.ToString() ?? string.Empty;
            var index = Array.IndexOf(item.EnumNames, currentValue);
            if (index >= 0)
                comboBox.SelectedIndex = index;
        }

        comboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!item.IsReadOnly && comboBox.SelectedItem is string selected)
                item.Value = selected;
        };

        panel.Controls.Add(comboBox);
        return panel;
    }

    private static Control CreateReadOnlyControl(OptionItemViewModel item, ThemeColors colors)
    {
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        var valueLabel = new Label
        {
            Text = item.Value?.ToString() ?? "(null)",
            ForeColor = ArgbToColor(colors.OnSurfaceMuted),
            Font = new Font("Segoe UI", 9.5f),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0),
        };

        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                valueLabel.Text = item.Value?.ToString() ?? "(null)";
        };

        panel.Controls.Add(valueLabel);
        return panel;
    }

    private static Control CreateActionButton(ActionItemViewModel action, ThemeColors colors)
    {
        var button = new Button
        {
            Text = action.Label,
            Dock = DockStyle.Top,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = ArgbToColor(colors.Primary),
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(6, 6, 6, 6),
        };

        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => action.ExecuteCommand.Execute(null);

        return button;
    }

    private static Panel CreateRowPanel(ThemeColors colors)
    {
        return new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = ArgbToColor(colors.Surface),
            Padding = new Padding(10, 4, 10, 4),
        };
    }

    private static void AddLabel(Panel panel, string text, ThemeColors colors)
    {
        var label = new Label
        {
            Text = text,
            ForeColor = ArgbToColor(colors.OnSurface),
            Font = new Font("Segoe UI", 9.5f),
            AutoSize = false,
            Width = 170,
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        panel.Controls.Add(label);
    }

    private static int ClampInt(int value, int min, int max)
        => Math.Max(min, Math.Min(max, value));

    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        => Math.Max(min, Math.Min(max, value));

    internal static Color ArgbToColor(uint argb)
        => Color.FromArgb((int)argb);
}
