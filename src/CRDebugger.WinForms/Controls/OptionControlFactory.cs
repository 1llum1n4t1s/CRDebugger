using CRDebugger.Core.Options;
using CRDebugger.Core.Theming;
using CRDebugger.Core.ViewModels;

namespace CRDebugger.WinForms.Controls;

/// <summary>
/// <see cref="OptionKind"/> に応じたWinFormsコントロールを動的に生成するファクトリクラス。
/// Boolean → CheckBox、Integer → TrackBar/NumericUpDown、Float → NumericUpDown、
/// String → TextBox、Enum → ComboBox、ReadOnly → 読み取り専用Label のように
/// オプション種別に対応したコントロールを返す。
/// モダンデザイン: 改善されたスペーシング、フォント、FlatStyleボタンを採用。
/// </summary>
public static class OptionControlFactory
{
    /// <summary>
    /// <see cref="OptionItemViewModel"/> のオプション種別に応じた WinForms コントロールを生成して返す。
    /// <see cref="ActionItemViewModel"/> の場合はボタンを生成する。
    /// </summary>
    /// <param name="item">コントロールを生成する対象のオプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>オプション種別に対応した <see cref="Control"/>。</returns>
    public static Control Create(OptionItemViewModel item, ThemeColors colors)
    {
        // オプション種別に応じてコントロールを切り替え
        return item.Kind switch
        {
            OptionKind.Boolean => CreateBooleanControl(item, colors),
            OptionKind.Integer => CreateIntegerControl(item, colors),
            OptionKind.Float => CreateFloatControl(item, colors),
            OptionKind.String => CreateStringControl(item, colors),
            OptionKind.Enum => CreateEnumControl(item, colors),
            OptionKind.ReadOnly => CreateReadOnlyControl(item, colors),
            // 未知の種別は読み取り専用コントロールにフォールバック
            _ => CreateReadOnlyControl(item, colors),
        };
    }

    /// <summary>
    /// Boolean型オプション用のチェックボックスコントロールを生成する。
    /// ViewModelの Value プロパティと双方向バインドする。
    /// </summary>
    /// <param name="item">Boolean型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>チェックボックスを含む行パネル。</returns>
    private static Control CreateBooleanControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルを生成
        var panel = CreateRowPanel(colors);

        // チェックボックスを生成してオプション設定を反映
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

        // チェック状態変更時に ViewModel の Value を更新
        checkBox.CheckedChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = checkBox.Checked;
        };

        // ViewModelの変更を監視してチェックボックスに反映（外部からの値変更に対応）
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                checkBox.Checked = item.Value is true;
        };

        panel.Controls.Add(checkBox);
        return panel;
    }

    /// <summary>
    /// Integer型オプション用のコントロールを生成する。
    /// Min/Max が指定されている場合はトラックバーと現在値ラベルを、
    /// 指定されていない場合は NumericUpDown を使用する。
    /// </summary>
    /// <param name="item">Integer型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>整数入力コントロールを含む行パネル。</returns>
    private static Control CreateIntegerControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルとラベルを生成
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        if (item.Min.HasValue && item.Max.HasValue)
        {
            // Min/Max が指定されている場合はトラックバーを使用
            var trackBar = new TrackBar
            {
                Minimum = (int)item.Min.Value,
                Maximum = (int)item.Max.Value,
                // 現在値を Min〜Max の範囲にクランプして設定
                Value = ClampInt(Convert.ToInt32(item.Value ?? 0), (int)item.Min.Value, (int)item.Max.Value),
                TickFrequency = (int)(item.Step ?? 1),
                SmallChange = (int)(item.Step ?? 1),
                LargeChange = (int)(item.Step ?? 1) * 5,
                Enabled = !item.IsReadOnly,
                Dock = DockStyle.Fill,
                BackColor = ArgbToColor(colors.Surface),
            };

            // 現在値を右端に表示するラベル
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

            // トラックバー変更時に ViewModel と表示ラベルを更新
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
            // Min/Max が未指定の場合は NumericUpDown を使用
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

            // 値変更時に ViewModel の Value を int に変換して更新
            numericUpDown.ValueChanged += (_, _) =>
            {
                if (!item.IsReadOnly)
                    item.Value = (int)numericUpDown.Value;
            };

            panel.Controls.Add(numericUpDown);
        }

        return panel;
    }

    /// <summary>
    /// Float型オプション用の数値スピナーコントロールを生成する。
    /// 小数点2桁の NumericUpDown を使用する。
    /// </summary>
    /// <param name="item">Float型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>浮動小数点入力コントロールを含む行パネル。</returns>
    private static Control CreateFloatControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルとラベルを生成
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        // 小数点2桁の NumericUpDown を生成（デフォルト範囲は ±9999999）
        var numericUpDown = new NumericUpDown
        {
            // 現在値を指定範囲内にクランプして初期値として設定
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

        // 値変更時に ViewModel の Value を double に変換して更新
        numericUpDown.ValueChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = (double)numericUpDown.Value;
        };

        panel.Controls.Add(numericUpDown);
        return panel;
    }

    /// <summary>
    /// String型オプション用のテキストボックスコントロールを生成する。
    /// ViewModelの Value プロパティと双方向バインドする。
    /// </summary>
    /// <param name="item">String型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>テキスト入力コントロールを含む行パネル。</returns>
    private static Control CreateStringControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルとラベルを生成
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

        // テキスト変更時に ViewModel の Value を更新
        textBox.TextChanged += (_, _) =>
        {
            if (!item.IsReadOnly)
                item.Value = textBox.Text;
        };

        // ViewModel 側で Value が変更された場合にテキストボックスを同期
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                textBox.Text = item.Value?.ToString() ?? string.Empty;
        };

        panel.Controls.Add(textBox);
        return panel;
    }

    /// <summary>
    /// Enum型オプション用のドロップダウンコントロールを生成する。
    /// EnumNames の文字列リストをコンボボックスの選択肢として設定する。
    /// </summary>
    /// <param name="item">Enum型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>ドロップダウンコントロールを含む行パネル。</returns>
    private static Control CreateEnumControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルとラベルを生成
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        // ドロップダウンリスト形式のコンボボックスを生成
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
            // 列挙値の文字列名をコンボボックスに追加
            comboBox.Items.AddRange(item.EnumNames);
            // 現在値に対応するインデックスを検索して選択
            var currentValue = item.Value?.ToString() ?? string.Empty;
            var index = Array.IndexOf(item.EnumNames, currentValue);
            if (index >= 0)
                comboBox.SelectedIndex = index;
        }

        // 選択変更時に ViewModel の Value を選択文字列で更新
        comboBox.SelectedIndexChanged += (_, _) =>
        {
            if (!item.IsReadOnly && comboBox.SelectedItem is string selected)
                item.Value = selected;
        };

        panel.Controls.Add(comboBox);
        return panel;
    }

    /// <summary>
    /// ReadOnly型オプション用の読み取り専用ラベルコントロールを生成する。
    /// ViewModel の Value 変更を監視して表示を自動更新する。
    /// </summary>
    /// <param name="item">ReadOnly型オプション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>読み取り専用値表示ラベルを含む行パネル。</returns>
    private static Control CreateReadOnlyControl(OptionItemViewModel item, ThemeColors colors)
    {
        // 行コンテナパネルとラベルを生成
        var panel = CreateRowPanel(colors);
        AddLabel(panel, item.DisplayName, colors);

        // 現在値を表示する読み取り専用ラベル（null の場合は "(null)" を表示）
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

        // ViewModel の Value が変更されたらラベルテキストを更新
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OptionItemViewModel.Value))
                valueLabel.Text = item.Value?.ToString() ?? "(null)";
        };

        panel.Controls.Add(valueLabel);
        return panel;
    }

    /// <summary>
    /// アクション項目用のボタンコントロールを生成する。
    /// クリック時に <see cref="ActionItemViewModel.ExecuteCommand"/> を実行する。
    /// </summary>
    /// <param name="action">アクション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>アクション実行ボタン。</returns>
    /// <summary>
    /// アクション項目用のボタンコントロールを外部から生成する公開メソッド。
    /// </summary>
    /// <param name="action">アクション項目 ViewModel。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>アクション実行ボタン。</returns>
    public static Control CreateAction(ActionItemViewModel action, ThemeColors colors)
        => CreateActionButton(action, colors);

    private static Control CreateActionButton(ActionItemViewModel action, ThemeColors colors)
    {
        // フラットスタイルのアクションボタンを生成
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

        // ボーダーを非表示にしてモダンデザインに統一
        button.FlatAppearance.BorderSize = 0;
        // クリック時にアクションコマンドを実行
        button.Click += (_, _) => action.ExecuteCommand.Execute(null);

        return button;
    }

    /// <summary>
    /// オプション行の共通コンテナパネルを生成する。
    /// 高さ40px、Dock.Top で縦積みレイアウトに対応する。
    /// </summary>
    /// <param name="colors">適用するテーマカラー情報。</param>
    /// <returns>行コンテナ用 <see cref="Panel"/>。</returns>
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

    /// <summary>
    /// オプション行の左側に表示するキー名ラベルをパネルに追加する。
    /// 幅170px 固定で Dock.Left に配置される。
    /// </summary>
    /// <param name="panel">ラベルを追加する対象パネル。</param>
    /// <param name="text">ラベルに表示するテキスト（オプション名）。</param>
    /// <param name="colors">適用するテーマカラー情報。</param>
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

    /// <summary>
    /// int 値を指定した Min〜Max の範囲内にクランプする。
    /// </summary>
    /// <param name="value">クランプ対象の値。</param>
    /// <param name="min">最小値。</param>
    /// <param name="max">最大値。</param>
    /// <returns>クランプ後の値。</returns>
    private static int ClampInt(int value, int min, int max)
        => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// decimal 値を指定した Min〜Max の範囲内にクランプする。
    /// </summary>
    /// <param name="value">クランプ対象の値。</param>
    /// <param name="min">最小値。</param>
    /// <param name="max">最大値。</param>
    /// <returns>クランプ後の値。</returns>
    private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        => Math.Max(min, Math.Min(max, value));

    /// <summary>
    /// ARGB形式の uint カラー値を <see cref="Color"/> に変換する。
    /// </summary>
    /// <param name="argb">変換する ARGB カラー値（uint）。</param>
    /// <returns>変換後の <see cref="Color"/>。</returns>
    internal static Color ArgbToColor(uint argb)
        => Color.FromArgb((int)argb);
}
