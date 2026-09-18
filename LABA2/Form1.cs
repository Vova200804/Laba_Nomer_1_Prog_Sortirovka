using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LABA2;

public partial class Form1 : Form
{
  private readonly DataGridView inputGrid = new();
  private readonly DataGridView resultsGrid = new();
  private readonly SortCanvas canvas = new();

  private readonly NumericUpDown countInput = new();
  private readonly NumericUpDown minInput = new();
  private readonly NumericUpDown maxInput = new();
  private readonly NumericUpDown delayInput = new();

  private readonly ComboBox directionInput = new();
  private readonly Label statusLabel = new();

  private readonly Dictionary<string, CheckBox> algorithmChecks = new();
  private CancellationTokenSource? cancellation;
  private static readonly HttpClient Http = new();

  public Form1()
  {
    InitializeComponent();
    Controls.Clear();

    Text = "Лабораторная работа №4 — олимпиадные сортировки";
    StartPosition = FormStartPosition.CenterScreen;
    MinimumSize = new Size(1050, 700);
    Size = new Size(1350, 850);

    CreateMenu();
    CreateInterface();
  }

  private void CreateMenu()
  {
    var menu = new MenuStrip();

    var dataMenu = new ToolStripMenuItem("Данные");
    var generateItem = new ToolStripMenuItem("Сгенерировать");
    var importExcelItem = new ToolStripMenuItem("Загрузить из Excel или CSV");
    var importGoogleItem = new ToolStripMenuItem("Загрузить из Google Таблицы");
    var clearItem = new ToolStripMenuItem("Очистить");

    generateItem.Click += (_, _) => GenerateValues();
    importExcelItem.Click += (_, _) => ImportFile();
    importGoogleItem.Click += async (_, _) => await ImportGoogleSheetAsync();
    clearItem.Click += (_, _) => ClearAll();

    dataMenu.DropDownItems.AddRange(
    [
        generateItem,
            importExcelItem,
            importGoogleItem,
            new ToolStripSeparator(),
            clearItem
    ]);

    var calculationsMenu = new ToolStripMenuItem("Вычисления");
    var runItem = new ToolStripMenuItem("Рассчитать");
    var cancelItem = new ToolStripMenuItem("Остановить");

    runItem.Click += async (_, _) => await RunSelectedAlgorithmsAsync();
    cancelItem.Click += (_, _) => cancellation?.Cancel();

    calculationsMenu.DropDownItems.AddRange([runItem, cancelItem]);

    menu.Items.AddRange([dataMenu, calculationsMenu]);
    MainMenuStrip = menu;
    Controls.Add(menu);
  }

  private void CreateInterface()
  {
    var root = new TableLayoutPanel
    {
      Dock = DockStyle.Fill,
      Padding = new Padding(8),
      ColumnCount = 1,
      RowCount = 3
    };

    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    root.RowStyles.Add(new RowStyle(SizeType.Absolute, 165));

    var settings = new FlowLayoutPanel
    {
      Dock = DockStyle.Fill,
      AutoScroll = true,
      WrapContents = true,
      Padding = new Padding(5)
    };

    AddLabel(settings, "Алгоритмы:");

    AddAlgorithmCheckBox(settings, "Пузырьковая", "Bubble", true);
    AddAlgorithmCheckBox(settings, "Вставками", "Insertion", true);
    AddAlgorithmCheckBox(settings, "Шейкерная", "Shaker", false);
    AddAlgorithmCheckBox(settings, "Быстрая", "Quick", true);
    AddAlgorithmCheckBox(settings, "BOGO", "Bogo", false);

    AddLabel(settings, "Порядок:");

    directionInput.DropDownStyle = ComboBoxStyle.DropDownList;
    directionInput.Items.AddRange(["По возрастанию", "По убыванию"]);
    directionInput.SelectedIndex = 0;
    directionInput.Width = 150;
    settings.Controls.Add(directionInput);

    ConfigureNumber(countInput, 2, 200, 20);
    ConfigureNumber(minInput, -10000, 10000, 0);
    ConfigureNumber(maxInput, -10000, 10000, 100);
    ConfigureNumber(delayInput, 0, 1000, 15);

    AddLabel(settings, "Количество:");
    settings.Controls.Add(countInput);

    AddLabel(settings, "Минимум:");
    settings.Controls.Add(minInput);

    AddLabel(settings, "Максимум:");
    settings.Controls.Add(maxInput);

    AddLabel(settings, "Задержка, мс:");
    settings.Controls.Add(delayInput);

    statusLabel.AutoSize = true;
    statusLabel.Padding = new Padding(10, 8, 0, 0);
    statusLabel.Text = "Введите данные или выполните генерацию.";
    settings.Controls.Add(statusLabel);

    ConfigureInputGrid();
    ConfigureResultsGrid();

    var split = new SplitContainer
    {
      Dock = DockStyle.Fill,
      Orientation = Orientation.Vertical
    };

    split.Panel1.Controls.Add(inputGrid);
    split.Panel2.Controls.Add(canvas);

    root.Controls.Add(settings, 0, 0);
    root.Controls.Add(split, 0, 1);
    root.Controls.Add(resultsGrid, 0, 2);

    Controls.Add(root);

    Shown += (_, _) =>
    {
      int maximumDistance =
          split.ClientSize.Width - split.SplitterWidth - 100;

      if (maximumDistance >= 100)
      {
        split.SplitterDistance = Math.Clamp(
            370,
            100,
            maximumDistance);
      }
    };
  }

  private static void AddLabel(Control parent, string text)
  {
    parent.Controls.Add(new Label
    {
      Text = text,
      AutoSize = true,
      Padding = new Padding(8, 8, 2, 0)
    });
  }

  private void AddAlgorithmCheckBox(
      Control parent,
      string caption,
      string algorithm,
      bool isChecked)
  {
    var checkBox = new CheckBox
    {
      Text = caption,
      Tag = algorithm,
      Checked = isChecked,
      AutoSize = true,
      Padding = new Padding(2, 6, 2, 0)
    };

    algorithmChecks.Add(algorithm, checkBox);
    parent.Controls.Add(checkBox);
  }

  private static void ConfigureNumber(
      NumericUpDown control,
      decimal minimum,
      decimal maximum,
      decimal value)
  {
    control.Minimum = minimum;
    control.Maximum = maximum;
    control.Value = value;
    control.Width = 75;
  }

  private void ConfigureInputGrid()
  {
    inputGrid.Dock = DockStyle.Fill;
    inputGrid.AllowUserToAddRows = true;
    inputGrid.AllowUserToDeleteRows = true;
    inputGrid.RowHeadersWidth = 55;
    inputGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    inputGrid.Columns.Add("Value", "Число");
  }

  private void ConfigureResultsGrid()
  {
    resultsGrid.Dock = DockStyle.Fill;
    resultsGrid.ReadOnly = true;
    resultsGrid.AllowUserToAddRows = false;
    resultsGrid.AllowUserToDeleteRows = false;
    resultsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    resultsGrid.RowHeadersVisible = false;

    resultsGrid.Columns.Add("Algorithm", "Алгоритм");
    resultsGrid.Columns.Add("Time", "Время");
    resultsGrid.Columns.Add("Result", "Результат");
  }

  private void GenerateValues()
  {
    if (minInput.Value > maxInput.Value)
    {
      ShowError("Минимальное значение не может быть больше максимального.");
      return;
    }

    int count = (int)countInput.Value;
    int minimum = (int)minInput.Value;
    int maximum = (int)maxInput.Value;

    var random = new Random();
    var values = new int[count];

    for (int i = 0; i < values.Length; i++)
      values[i] = random.Next(minimum, maximum + 1);

    SetInputValues(values);
    statusLabel.Text = $"Сгенерировано элементов: {values.Length}.";
  }

  private void SetInputValues(IEnumerable<int> values)
  {
    inputGrid.Rows.Clear();

    foreach (int value in values)
      inputGrid.Rows.Add(new object[] { value });
  }

  private int[] ReadInputValues()
  {
    var result = new List<int>();

    foreach (DataGridViewRow row in inputGrid.Rows)
    {
      if (row.IsNewRow)
        continue;

      string text = Convert.ToString(row.Cells[0].Value)?.Trim() ?? "";

      if (string.IsNullOrWhiteSpace(text))
        continue;

      if (!int.TryParse(text, NumberStyles.Integer,
              CultureInfo.CurrentCulture, out int value))
      {
        throw new FormatException(
            $"В строке {row.Index + 1} указано некорректное целое число.");
      }

      result.Add(value);
    }

    if (result.Count < 2)
      throw new InvalidOperationException(
          "Необходимо ввести не менее двух чисел.");

    return result.ToArray();
  }

  private List<string> GetSelectedAlgorithms()
  {
    return algorithmChecks
        .Where(item => item.Value.Checked)
        .Select(item => item.Key)
        .ToList();
  }

  private async Task RunSelectedAlgorithmsAsync()
  {
    if (cancellation is not null)
    {
      ShowError("Сортировка уже выполняется.");
      return;
    }

    try
    {
      int[] source = ReadInputValues();
      List<string> selected = GetSelectedAlgorithms();

      if (selected.Count == 0)
        throw new InvalidOperationException(
            "Выберите хотя бы один алгоритм сортировки.");

      if (selected.Contains("Bogo") && source.Length > 8)
      {
        throw new InvalidOperationException(
            "Для BOGO-сортировки допускается не более 8 элементов.");
      }

      bool ascending = directionInput.SelectedIndex == 0;
      int delay = (int)delayInput.Value;

      cancellation = new CancellationTokenSource();
      CancellationToken token = cancellation.Token;

      resultsGrid.Rows.Clear();
      canvas.Clear();

      statusLabel.Text = "Измерение времени...";
      UseWaitCursor = true;

      Task<SortStat>[] benchmarkTasks = selected
          .Select(name => Task.Run(
              () => Algorithms.Benchmark(
                  name, source, ascending, token), token))
          .ToArray();

      SortStat[] statistics = await Task.WhenAll(benchmarkTasks);

      string? fastest = statistics
          .Where(stat => stat.Success)
          .OrderBy(stat => stat.ElapsedTicks)
          .FirstOrDefault()
          ?.Algorithm;

      foreach (SortStat stat in statistics)
      {
        string fastestMark =
            stat.Algorithm == fastest ? " — самый быстрый" : "";

        resultsGrid.Rows.Add(
            AlgorithmTitle(stat.Algorithm),
            $"{stat.ElapsedMilliseconds:F6} мс",
            stat.Message + fastestMark);
      }

      statusLabel.Text = fastest is null
          ? "Ни один алгоритм не завершил сортировку."
          : $"Самый быстрый метод: {AlgorithmTitle(fastest)}. " +
            "Выполняется визуализация...";

      UseWaitCursor = false;

      foreach (string name in selected)
      {
        canvas.SetState(
            name,
            AlgorithmTitle(name),
            source,
            "Ожидание");
      }

      Task[] animations = selected.Select(name =>
          AnimateAsync(name, source, ascending, delay, token)).ToArray();

      await Task.WhenAll(animations);

      statusLabel.Text = fastest is null
          ? "Визуализация завершена."
          : $"Готово. Самый быстрый метод: {AlgorithmTitle(fastest)}.";
    }
    catch (OperationCanceledException)
    {
      statusLabel.Text = "Выполнение остановлено.";
    }
    catch (Exception exception)
    {
      ShowError(exception.Message);
      statusLabel.Text = "Выполнение завершилось с ошибкой.";
    }
    finally
    {
      UseWaitCursor = false;
      cancellation?.Dispose();
      cancellation = null;
    }
  }

  private async Task AnimateAsync(
      string name,
      int[] source,
      bool ascending,
      int delay,
      CancellationToken token)
  {
    async Task ShowStep(int[] values, string message)
    {
      token.ThrowIfCancellationRequested();

      canvas.SetState(
          name,
          AlgorithmTitle(name),
          values,
          message);

      await Task.Delay(delay, token);
    }

    bool success = await Algorithms.Animate(
        name,
        source,
        ascending,
        ShowStep,
        token);

    canvas.SetStatus(
        name,
        success ? "Сортировка завершена" : "Превышен лимит попыток");
  }

  private void ImportFile()
  {
    using var dialog = new OpenFileDialog
    {
      Title = "Загрузка данных",
      Filter =
            "Excel или CSV (*.xlsx;*.csv)|*.xlsx;*.csv|" +
            "Excel (*.xlsx)|*.xlsx|" +
            "CSV (*.csv)|*.csv"
    };

    if (dialog.ShowDialog(this) != DialogResult.OK)
      return;

    try
    {
      List<int> values = Path.GetExtension(dialog.FileName)
          .Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
          ? XlsxReader.ReadNumbers(dialog.FileName)
          : CsvReader.ReadNumbers(File.ReadAllText(dialog.FileName));

      if (values.Count == 0)
        throw new InvalidOperationException(
            "В файле не найдены целые числа.");

      SetInputValues(values);
      statusLabel.Text = $"Загружено элементов: {values.Count}.";
    }
    catch (Exception exception)
    {
      ShowError($"Не удалось загрузить файл: {exception.Message}");
    }
  }

  private async Task ImportGoogleSheetAsync()
  {
    string? link = PromptDialog.Show(
        this,
        "Google Таблица",
        "Введите общедоступную ссылку на Google Таблицу:");

    if (string.IsNullOrWhiteSpace(link))
      return;

    try
    {
      string csvUrl = ConvertGoogleLink(link);
      statusLabel.Text = "Загрузка Google Таблицы...";

      string csv = await Http.GetStringAsync(csvUrl);
      List<int> values = CsvReader.ReadNumbers(csv);

      if (values.Count == 0)
        throw new InvalidOperationException(
            "В таблице не найдены целые числа.");

      SetInputValues(values);
      statusLabel.Text = $"Загружено элементов: {values.Count}.";
    }
    catch (Exception exception)
    {
      ShowError(
          "Не удалось загрузить Google Таблицу. " +
          "Проверьте ссылку и доступ к таблице.\n\n" +
          exception.Message);
    }
  }

  private static string ConvertGoogleLink(string link)
  {
    Match idMatch = Regex.Match(
        link,
        @"/spreadsheets/d/([a-zA-Z0-9_-]+)");

    if (!idMatch.Success)
      throw new FormatException(
          "Ссылка не похожа на ссылку Google Таблицы.");

    Match gidMatch = Regex.Match(link, @"(?:[?&#]|^)gid=(\d+)");
    string gid = gidMatch.Success ? gidMatch.Groups[1].Value : "0";
    string documentId = idMatch.Groups[1].Value;

    return
        $"https://docs.google.com/spreadsheets/d/{documentId}" +
        $"/export?format=csv&gid={gid}";
  }

  private void ClearAll()
  {
    cancellation?.Cancel();
    inputGrid.Rows.Clear();
    resultsGrid.Rows.Clear();
    canvas.Clear();
    statusLabel.Text = "Данные очищены.";
  }

  private void ShowError(string message)
  {
    MessageBox.Show(
        this,
        message,
        "Ошибка",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
  }

  private static string AlgorithmTitle(string name)
  {
    return name switch
    {
      "Bubble" => "Пузырьковая сортировка",
      "Insertion" => "Сортировка вставками",
      "Shaker" => "Шейкерная сортировка",
      "Quick" => "Быстрая сортировка",
      "Bogo" => "BOGO-сортировка",
      _ => name
    };
  }
}

public sealed record SortStat(
    string Algorithm,
    long ElapsedTicks,
    double ElapsedMilliseconds,
    bool Success,
    string Message);

public static class Algorithms
{
  private const int BogoAttemptLimit = 200_000;

  public static SortStat Benchmark(
      string name,
      int[] source,
      bool ascending,
      CancellationToken token)
  {
    int[] values = (int[])source.Clone();
    bool success = true;
    string message = "Отсортировано";

    var stopwatch = Stopwatch.StartNew();

    switch (name)
    {
      case "Bubble":
        Bubble(values, ascending, token);
        break;

      case "Insertion":
        Insertion(values, ascending, token);
        break;

      case "Shaker":
        Shaker(values, ascending, token);
        break;

      case "Quick":
        Quick(values, 0, values.Length - 1, ascending, token);
        break;

      case "Bogo":
        int attempts = Bogo(
            values,
            ascending,
            token,
            BogoAttemptLimit);

        success = IsSorted(values, ascending);
        message = success
            ? $"Отсортировано, попыток: {attempts}"
            : $"Лимит {BogoAttemptLimit:N0} попыток";
        break;

      default:
        throw new ArgumentOutOfRangeException(
            nameof(name), "Неизвестный алгоритм.");
    }

    stopwatch.Stop();

    return new SortStat(
        name,
        stopwatch.ElapsedTicks,
        stopwatch.Elapsed.TotalMilliseconds,
        success,
        message);
  }

  public static Task<bool> Animate(
      string name,
      int[] source,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    return name switch
    {
      "Bubble" => AnimateBubble(
          (int[])source.Clone(), ascending, step, token),

      "Insertion" => AnimateInsertion(
          (int[])source.Clone(), ascending, step, token),

      "Shaker" => AnimateShaker(
          (int[])source.Clone(), ascending, step, token),

      "Quick" => AnimateQuick(
          (int[])source.Clone(), ascending, step, token),

      "Bogo" => AnimateBogo(
          (int[])source.Clone(), ascending, step, token),

      _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
  }

  private static bool WrongOrder(int left, int right, bool ascending)
  {
    return ascending ? left > right : left < right;
  }

  private static bool ComesBefore(int value, int other, bool ascending)
  {
    return ascending ? value < other : value > other;
  }

  private static bool IsSorted(int[] values, bool ascending)
  {
    for (int i = 1; i < values.Length; i++)
    {
      if (WrongOrder(values[i - 1], values[i], ascending))
        return false;
    }

    return true;
  }

  private static void Bubble(
      int[] values,
      bool ascending,
      CancellationToken token)
  {
    for (int end = values.Length - 1; end > 0; end--)
    {
      bool changed = false;

      for (int i = 0; i < end; i++)
      {
        token.ThrowIfCancellationRequested();

        if (!WrongOrder(values[i], values[i + 1], ascending))
          continue;

        (values[i], values[i + 1]) =
            (values[i + 1], values[i]);

        changed = true;
      }

      if (!changed)
        break;
    }
  }

  private static void Insertion(
      int[] values,
      bool ascending,
      CancellationToken token)
  {
    for (int i = 1; i < values.Length; i++)
    {
      token.ThrowIfCancellationRequested();

      int current = values[i];
      int j = i - 1;

      while (j >= 0 && ComesBefore(current, values[j], ascending))
      {
        values[j + 1] = values[j];
        j--;
      }

      values[j + 1] = current;
    }
  }

  private static void Shaker(
      int[] values,
      bool ascending,
      CancellationToken token)
  {
    int left = 0;
    int right = values.Length - 1;

    while (left < right)
    {
      bool changed = false;

      for (int i = left; i < right; i++)
      {
        token.ThrowIfCancellationRequested();

        if (!WrongOrder(values[i], values[i + 1], ascending))
          continue;

        (values[i], values[i + 1]) =
            (values[i + 1], values[i]);

        changed = true;
      }

      right--;

      for (int i = right; i > left; i--)
      {
        token.ThrowIfCancellationRequested();

        if (!WrongOrder(values[i - 1], values[i], ascending))
          continue;

        (values[i - 1], values[i]) =
            (values[i], values[i - 1]);

        changed = true;
      }

      left++;

      if (!changed)
        break;
    }
  }

  private static void Quick(
      int[] values,
      int low,
      int high,
      bool ascending,
      CancellationToken token)
  {
    token.ThrowIfCancellationRequested();

    if (low >= high)
      return;

    int i = low;
    int j = high;
    int pivot = values[low + (high - low) / 2];

    while (i <= j)
    {
      while (ComesBefore(values[i], pivot, ascending))
        i++;

      while (ComesBefore(pivot, values[j], ascending))
        j--;

      if (i <= j)
      {
        (values[i], values[j]) = (values[j], values[i]);
        i++;
        j--;
      }
    }

    if (low < j)
      Quick(values, low, j, ascending, token);

    if (i < high)
      Quick(values, i, high, ascending, token);
  }

  private static int Bogo(
      int[] values,
      bool ascending,
      CancellationToken token,
      int limit)
  {
    var random = new Random(12345);
    int attempts = 0;

    while (!IsSorted(values, ascending) && attempts < limit)
    {
      token.ThrowIfCancellationRequested();
      Shuffle(values, random);
      attempts++;
    }

    return attempts;
  }

  private static void Shuffle(int[] values, Random random)
  {
    for (int i = values.Length - 1; i > 0; i--)
    {
      int j = random.Next(i + 1);
      (values[i], values[j]) = (values[j], values[i]);
    }
  }

  private static async Task<bool> AnimateBubble(
      int[] values,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    for (int end = values.Length - 1; end > 0; end--)
    {
      bool changed = false;

      for (int i = 0; i < end; i++)
      {
        token.ThrowIfCancellationRequested();

        if (WrongOrder(values[i], values[i + 1], ascending))
        {
          (values[i], values[i + 1]) =
              (values[i + 1], values[i]);

          changed = true;
          await step(values, $"Обмен элементов {i + 1} и {i + 2}");
        }
      }

      if (!changed)
        break;
    }

    await step(values, "Готово");
    return true;
  }

  private static async Task<bool> AnimateInsertion(
      int[] values,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    for (int i = 1; i < values.Length; i++)
    {
      token.ThrowIfCancellationRequested();

      int current = values[i];
      int j = i - 1;

      while (j >= 0 && ComesBefore(current, values[j], ascending))
      {
        values[j + 1] = values[j];
        j--;
        await step(values, $"Вставка элемента {i + 1}");
      }

      values[j + 1] = current;
      await step(values, $"Элемент установлен на позицию {j + 2}");
    }

    await step(values, "Готово");
    return true;
  }

  private static async Task<bool> AnimateShaker(
      int[] values,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    int left = 0;
    int right = values.Length - 1;

    while (left < right)
    {
      bool changed = false;

      for (int i = left; i < right; i++)
      {
        token.ThrowIfCancellationRequested();

        if (WrongOrder(values[i], values[i + 1], ascending))
        {
          (values[i], values[i + 1]) =
              (values[i + 1], values[i]);

          changed = true;
          await step(values, "Проход слева направо");
        }
      }

      right--;

      for (int i = right; i > left; i--)
      {
        token.ThrowIfCancellationRequested();

        if (WrongOrder(values[i - 1], values[i], ascending))
        {
          (values[i - 1], values[i]) =
              (values[i], values[i - 1]);

          changed = true;
          await step(values, "Проход справа налево");
        }
      }

      left++;

      if (!changed)
        break;
    }

    await step(values, "Готово");
    return true;
  }

  private static async Task<bool> AnimateQuick(
      int[] values,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    await QuickPart(0, values.Length - 1);
    await step(values, "Готово");
    return true;

    async Task QuickPart(int low, int high)
    {
      token.ThrowIfCancellationRequested();

      if (low >= high)
        return;

      int i = low;
      int j = high;
      int pivot = values[low + (high - low) / 2];

      while (i <= j)
      {
        while (ComesBefore(values[i], pivot, ascending))
          i++;

        while (ComesBefore(pivot, values[j], ascending))
          j--;

        if (i <= j)
        {
          (values[i], values[j]) = (values[j], values[i]);
          await step(values, $"Опорный элемент: {pivot}");
          i++;
          j--;
        }
      }

      if (low < j)
        await QuickPart(low, j);

      if (i < high)
        await QuickPart(i, high);
    }
  }

  private static async Task<bool> AnimateBogo(
      int[] values,
      bool ascending,
      Func<int[], string, Task> step,
      CancellationToken token)
  {
    var random = new Random();
    int attempts = 0;

    while (!IsSorted(values, ascending) &&
           attempts < BogoAttemptLimit)
    {
      token.ThrowIfCancellationRequested();

      Shuffle(values, random);
      attempts++;

      // Не отображаем каждый обмен, иначе интерфейс будет слишком медленным.
      if (attempts == 1 || attempts % 250 == 0)
        await step(values, $"Случайная перестановка №{attempts}");
    }

    bool success = IsSorted(values, ascending);

    await step(
        values,
        success
            ? $"Готово за {attempts} попыток"
            : $"Лимит {BogoAttemptLimit:N0} попыток");

    return success;
  }
}

public sealed class SortCanvas : Control
{
  private readonly Dictionary<string, VisualState> states = new();

  public SortCanvas()
  {
    Dock = DockStyle.Fill;
    DoubleBuffered = true;
    BackColor = Color.White;
    ResizeRedraw = true;
  }

  public void SetState(
      string key,
      string title,
      int[] values,
      string status)
  {
    states[key] = new VisualState(
        title,
        (int[])values.Clone(),
        status);

    Invalidate();
  }

  public void SetStatus(string key, string status)
  {
    if (states.TryGetValue(key, out VisualState? state))
      states[key] = state with { Status = status };

    Invalidate();
  }

  public void Clear()
  {
    states.Clear();
    Invalidate();
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);

    e.Graphics.Clear(BackColor);
    e.Graphics.SmoothingMode =
        System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

    if (states.Count == 0)
    {
      using var emptyBrush = new SolidBrush(Color.DimGray);
      e.Graphics.DrawString(
          "Здесь будет отображаться процесс сортировки.",
          Font,
          emptyBrush,
          new PointF(15, 15));
      return;
    }

    int sectionHeight = Math.Max(100, ClientSize.Height / states.Count);
    int index = 0;

    foreach (VisualState state in states.Values)
    {
      Rectangle area = new(
          8,
          index * sectionHeight + 5,
          Math.Max(10, ClientSize.Width - 16),
          Math.Max(80, sectionHeight - 10));

      DrawState(e.Graphics, area, state);
      index++;
    }
  }

  private void DrawState(
      Graphics graphics,
      Rectangle area,
      VisualState state)
  {
    using var borderPen = new Pen(Color.LightGray);
    using var titleBrush = new SolidBrush(Color.FromArgb(35, 35, 35));
    using var statusBrush = new SolidBrush(Color.DimGray);
    using var barBrush = new SolidBrush(Color.FromArgb(45, 125, 210));

    graphics.DrawRectangle(borderPen, area);

    using var titleFont = new Font(Font, FontStyle.Bold);
    graphics.DrawString(
        state.Title,
        titleFont,
        titleBrush,
        area.Left + 8,
        area.Top + 5);

    SizeF statusSize = graphics.MeasureString(state.Status, Font);
    graphics.DrawString(
        state.Status,
        Font,
        statusBrush,
        area.Right - statusSize.Width - 8,
        area.Top + 5);

    if (state.Values.Length == 0)
      return;

    int chartTop = area.Top + 30;
    int chartBottom = area.Bottom - 22;
    int chartHeight = Math.Max(10, chartBottom - chartTop);

    int minimum = state.Values.Min();
    int maximum = state.Values.Max();
    double range = Math.Max(1.0, maximum - (double)minimum);

    float slotWidth =
        Math.Max(1f, (area.Width - 16f) / state.Values.Length);
    float barWidth = Math.Max(1f, slotWidth - 2f);

    for (int i = 0; i < state.Values.Length; i++)
    {
      double normalized =
          (state.Values[i] - (double)minimum) / range;

      float height = (float)(8 + normalized * (chartHeight - 8));
      float x = area.Left + 8 + i * slotWidth;
      float y = chartBottom - height;

      graphics.FillRectangle(
          barBrush,
          x,
          y,
          barWidth,
          height);
    }

    graphics.DrawString(
        $"min: {minimum}   max: {maximum}",
        Font,
        statusBrush,
        area.Left + 8,
        area.Bottom - 20);
  }

  private sealed record VisualState(
      string Title,
      int[] Values,
      string Status);
}

public static class CsvReader
{
  public static List<int> ReadNumbers(string csv)
  {
    var numbers = new List<int>();

    foreach (string value in ReadFields(csv))
    {
      string text = value.Trim();

      if (int.TryParse(
              text,
              NumberStyles.Integer,
              CultureInfo.CurrentCulture,
              out int current) ||
          int.TryParse(
              text,
              NumberStyles.Integer,
              CultureInfo.InvariantCulture,
              out current))
      {
        numbers.Add(current);
      }
    }

    return numbers;
  }

  private static IEnumerable<string> ReadFields(string text)
  {
    var field = new StringBuilder();
    bool quoted = false;

    for (int i = 0; i < text.Length; i++)
    {
      char current = text[i];

      if (current == '"')
      {
        if (quoted && i + 1 < text.Length && text[i + 1] == '"')
        {
          field.Append('"');
          i++;
        }
        else
        {
          quoted = !quoted;
        }
      }
      else if (!quoted && (current == ',' || current == ';' ||
                           current == '\n' || current == '\r'))
      {
        yield return field.ToString();
        field.Clear();

        if (current == '\r' &&
            i + 1 < text.Length &&
            text[i + 1] == '\n')
        {
          i++;
        }
      }
      else
      {
        field.Append(current);
      }
    }

    yield return field.ToString();
  }
}

public static class XlsxReader
{
  private static readonly XNamespace Spreadsheet =
      "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

  private static readonly XNamespace OfficeRelationships =
      "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

  private static readonly XNamespace PackageRelationships =
      "http://schemas.openxmlformats.org/package/2006/relationships";

  public static List<int> ReadNumbers(string fileName)
  {
    using ZipArchive archive = ZipFile.OpenRead(fileName);

    List<string> sharedStrings = ReadSharedStrings(archive);
    string sheetPath = FindFirstSheetPath(archive);

    ZipArchiveEntry sheetEntry =
        archive.GetEntry(sheetPath) ??
        throw new InvalidDataException(
            "Не найден первый лист Excel.");

    using Stream sheetStream = sheetEntry.Open();
    XDocument sheet = XDocument.Load(sheetStream);

    var result = new List<int>();

    foreach (XElement cell in sheet.Descendants(Spreadsheet + "c"))
    {
      string type = (string?)cell.Attribute("t") ?? "";
      string? text;

      if (type == "inlineStr")
      {
        text = string.Concat(
            cell.Descendants(Spreadsheet + "t")
                .Select(node => node.Value));
      }
      else
      {
        text = cell.Element(Spreadsheet + "v")?.Value;
      }

      if (text is null)
        continue;

      if (type == "s" &&
          int.TryParse(text, out int stringIndex) &&
          stringIndex >= 0 &&
          stringIndex < sharedStrings.Count)
      {
        text = sharedStrings[stringIndex];
      }

      if (int.TryParse(
              text,
              NumberStyles.Integer,
              CultureInfo.InvariantCulture,
              out int value) ||
          int.TryParse(
              text,
              NumberStyles.Integer,
              CultureInfo.CurrentCulture,
              out value))
      {
        result.Add(value);
      }
    }

    return result;
  }

  private static List<string> ReadSharedStrings(ZipArchive archive)
  {
    ZipArchiveEntry? entry =
        archive.GetEntry("xl/sharedStrings.xml");

    if (entry is null)
      return [];

    using Stream stream = entry.Open();
    XDocument document = XDocument.Load(stream);

    return document
        .Descendants(Spreadsheet + "si")
        .Select(item => string.Concat(
            item.Descendants(Spreadsheet + "t")
                .Select(text => text.Value)))
        .ToList();
  }

  private static string FindFirstSheetPath(ZipArchive archive)
  {
    ZipArchiveEntry workbookEntry =
        archive.GetEntry("xl/workbook.xml") ??
        throw new InvalidDataException(
            "Файл не содержит книгу Excel.");

    ZipArchiveEntry relationsEntry =
        archive.GetEntry("xl/_rels/workbook.xml.rels") ??
        throw new InvalidDataException(
            "Не найдены связи книги Excel.");

    XDocument workbook;
    XDocument relations;

    using (Stream stream = workbookEntry.Open())
      workbook = XDocument.Load(stream);

    using (Stream stream = relationsEntry.Open())
      relations = XDocument.Load(stream);

    XElement firstSheet =
        workbook.Descendants(Spreadsheet + "sheet").FirstOrDefault() ??
        throw new InvalidDataException("Книга Excel не содержит листов.");

    string relationId =
        (string?)firstSheet.Attribute(
            OfficeRelationships + "id") ??
        throw new InvalidDataException(
            "Не найдена связь с первым листом.");

    XElement relation =
        relations.Descendants(PackageRelationships + "Relationship")
            .FirstOrDefault(item =>
                (string?)item.Attribute("Id") == relationId) ??
        throw new InvalidDataException(
            "Не найден файл первого листа.");

    string target =
        (string?)relation.Attribute("Target") ??
        throw new InvalidDataException(
            "Не задан путь к первому листу.");

    target = target.Replace('\\', '/');

    if (target.StartsWith('/'))
      return target.TrimStart('/');

    while (target.StartsWith("../"))
      target = target[3..];

    return target.StartsWith("xl/")
        ? target
        : "xl/" + target;
  }
}

public sealed class PromptDialog : Form
{
  private readonly TextBox input = new();

  private PromptDialog(string title, string message)
  {
    Text = title;
    StartPosition = FormStartPosition.CenterParent;
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MinimizeBox = false;
    MaximizeBox = false;
    ClientSize = new Size(580, 145);

    var label = new Label
    {
      Text = message,
      AutoSize = true,
      Location = new Point(12, 15)
    };

    input.Location = new Point(12, 45);
    input.Width = 550;

    var okButton = new Button
    {
      Text = "OK",
      DialogResult = DialogResult.OK,
      Location = new Point(390, 95),
      Width = 80
    };

    var cancelButton = new Button
    {
      Text = "Отмена",
      DialogResult = DialogResult.Cancel,
      Location = new Point(482, 95),
      Width = 80
    };

    AcceptButton = okButton;
    CancelButton = cancelButton;

    Controls.AddRange([label, input, okButton, cancelButton]);
  }

  public static string? Show(
      IWin32Window owner,
      string title,
      string message)
  {
    using var dialog = new PromptDialog(title, message);

    return dialog.ShowDialog(owner) == DialogResult.OK
        ? dialog.input.Text.Trim()
        : null;
  }
}
